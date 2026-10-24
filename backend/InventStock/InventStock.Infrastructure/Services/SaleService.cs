using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using InventStock.Application.Common;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using InventStock.Application.Common.Models;
using InventStock.Application.Validators;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventStock.Infrastructure.Services;

public sealed class SaleService(AppDbContext db, ICurrentUserService currentUser, IStockNotifier notifier,
    ILogger<SaleService> logger) : ISaleService
{
    public Task<SaleDto> PostAsync(PostSaleRequest request, CancellationToken cancellationToken = default) =>
        PostCoreAsync(request, null, cancellationToken);

    public async Task<SaleDto> PostPaperRecoveryAsync(PostPaperRecoverySaleRequest request, CancellationToken cancellationToken = default)
    {
        await new PostPaperRecoverySaleRequestValidator().ValidateAndThrowAsync(request, cancellationToken);
        return await PostCoreAsync(request.Sale, new PaperRecoveryContext(request.OriginalOccurredAtUtc,
            request.PaperReference.Trim(), request.Reason.Trim(), request.OriginalSellerNote?.Trim()), cancellationToken);
    }

    private async Task<SaleDto> PostCoreAsync(PostSaleRequest request, PaperRecoveryContext? paperRecovery,
        CancellationToken cancellationToken)
    {
        await new PostSaleRequestValidator().ValidateAndThrowAsync(request, cancellationToken);
        var fingerprint = paperRecovery is null ? Fingerprint(request) : PaperRecoveryFingerprint(request, paperRecovery);
        SaleDto result;
        var notifications = new List<StockChangedNotification>();
        // This service owns the entire transaction. Do not call other independently committing services.
        await using (var tx = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await OperationGuard.LockAsync(db, request.OperationId, "Sale", cancellationToken);
            var actor = paperRecovery is null
                ? await AuthorizedActor(cancellationToken)
                : await CounterAuthorization.RequireAsync(db, currentUser, cancellationToken, adminOnly: true);
            var replay = await Query().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
            if (replay is not null)
            {
                if (replay.ActorId != actor || replay.PayloadFingerprint != fingerprint)
                    throw new ConflictException("This operation ID belongs to another actor or sale payload.");
                result = Map(replay);
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            if (paperRecovery is not null && await db.PaperRecoveryEntries.AnyAsync(
                x => x.PaperReference == paperRecovery.PaperReference, cancellationToken))
                throw new ConflictException("This paper reference was already recovered.");

            // Global resource order: operation -> customer -> distinct part IDs in Guid order.
            Customer? customer = null;
            if (request.CustomerId.HasValue)
            {
                if (db.Database.IsNpgsql())
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Customers\" WHERE \"Id\" = {request.CustomerId.Value} FOR UPDATE", cancellationToken);
                customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == request.CustomerId && x.IsActive, cancellationToken)
                    ?? throw new NotFoundException("Active customer", request.CustomerId.Value);
                var balance = await db.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id)
                    .SumAsync(x => (decimal?)x.AmountUsd, cancellationToken) ?? 0;
                if (request.CustomerCreditUsedUsd > Math.Max(0, -balance))
                    throw new ConflictException("The customer does not have enough available credit.");
            }

            var quantities = request.Lines.GroupBy(x => x.PartId).ToDictionary(x => x.Key, x => checked(x.Sum(y => y.Quantity)));
            var partIds = quantities.Keys.Order().ToArray();
            foreach (var id in partIds)
                if (db.Database.IsNpgsql())
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {id} FOR UPDATE", cancellationToken);
            var parts = await db.Parts.Where(x => partIds.Contains(x.Id) && x.IsActive)
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            // Load balances in bounded aggregate queries after taking every lock, not four reads per cart line.
            var stock = await db.StockMovements.Where(x => partIds.Contains(x.PartId)).GroupBy(x => x.PartId)
                .Select(g => new { PartId = g.Key, Physical = g.Sum(x => x.QuantityChange), Counted = g.Any(x => x.CountedQuantity != null) })
                .ToDictionaryAsync(x => x.PartId, cancellationToken);
            var holds = await db.StockHoldMovements.Where(x => partIds.Contains(x.PartId)).GroupBy(x => x.PartId)
                .Select(g => new { PartId = g.Key, Quantity = g.Sum(x => x.QuantityChange) })
                .ToDictionaryAsync(x => x.PartId, cancellationToken);
            var availableAfter = new Dictionary<Guid, int>();
            var counted = new Dictionary<Guid, bool>();
            var shortages = new HashSet<Guid>();
            foreach (var id in partIds)
            {
                if (!parts.TryGetValue(id, out var part)) throw new NotFoundException("Active part", id);
                var physical = stock.GetValueOrDefault(id)?.Physical ?? 0;
                var held = holds.GetValueOrDefault(id)?.Quantity ?? 0;
                counted[id] = stock.GetValueOrDefault(id)?.Counted ?? false;
                availableAfter[id] = physical - held - quantities[id];
                if (availableAfter[id] < 0)
                {
                    // This exception records a real item in the employee's hands, not a supplier promise.
                    // Never create stock from that claim: the negative book balance remains visible for a recount.
                    // When anything is on quality hold, require a separate physical count/receipt before selling.
                    // A counter exception must never be mistaken for permission to pick held goods.
                    var lines = request.Lines.Where(x => x.PartId == id).ToArray();
                    if (held > 0 || part.NeedsRecount || lines.Any(x => !x.PhysicalGoodsConfirmed || string.IsNullOrWhiteSpace(x.StockExceptionReason)))
                        throw new InsufficientStockException(part.PartNumber, physical - held, quantities[id]);
                    shortages.Add(id);
                    part.RequireRecount();
                }
                if (!counted[id] && request.Lines.Where(x => x.PartId == id).Any(x => !x.PhysicalGoodsConfirmed))
                    throw new ConflictException("Uncounted stock requires physical confirmation and a reason on every affected line.");
            }

            var lineTotals = request.Lines.Select(x => decimal.Round(x.UnitSellingPriceUsd * x.Quantity, 2, MidpointRounding.AwayFromZero)).ToArray();
            var total = lineTotals.Sum();
            var payments = request.Payments.Sum(x => x.ConfirmedUsdAmount);
            var change = request.Change.Sum(x => x.ConfirmedUsdAmount);
            if (total != request.ConfirmedTotalUsd || payments < change ||
                total != payments - change + request.CustomerCreditUsedUsd + request.UnpaidDebtUsd)
                throw new ConflictException("Sale total must equal confirmed payments minus change plus customer credit used plus unpaid debt.");

            var sale = Sale.Create(request.OperationId, fingerprint, actor, customer, total, payments, change,
                request.CustomerCreditUsedUsd, request.UnpaidDebtUsd, postedAtUtc: paperRecovery?.OriginalOccurredAtUtc);
            if (!db.Database.IsNpgsql())
            {
                var nextReceipt = (await db.Sales.Select(x => (long?)x.ReceiptNumber).MaxAsync(cancellationToken) ?? 99_999L) + 1;
                sale.AssignReceiptNumberForNonPostgres(nextReceipt);
            }
            // One query for latest actual cost per SKU. Quotes and unknown costs never become a fabricated cost basis.
            var costs = await db.SupplierCostObservations.AsNoTracking()
                .Where(x => partIds.Contains(x.PartId) && x.Source == CostSource.ActualPurchase && x.ObservedAtUtc <= sale.PostedAtUtc)
                .GroupBy(x => x.PartId).Select(g => g.OrderByDescending(x => x.ObservedAtUtc).ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).First())
                .ToDictionaryAsync(x => x.PartId, cancellationToken);
            var cutHoseIds = partIds.Where(x => parts[x].StockUnit == StockUnit.Centimeter).ToArray();
            var cutHoseRates = await db.CutHoseSellingRates.AsNoTracking()
                .Where(x => cutHoseIds.Contains(x.PartId))
                .GroupBy(x => x.PartId).Select(g => g.OrderByDescending(x => x.Revision).First())
                .ToDictionaryAsync(x => x.PartId, cancellationToken);
            // Capture the server's current suggestion with the posted price. Never trust a browser-supplied suggestion.
            var pricingPolicy = await db.PricingPolicies.AsNoTracking().SingleAsync(x => x.Id == 1, cancellationToken);
            for (var i = 0; i < request.Lines.Count; i++)
            {
                var line = request.Lines[i];
                var suggestion = parts[line.PartId].StockUnit == StockUnit.Centimeter
                    ? PricingService.CalculateCutHose(line.PartId, cutHoseRates.GetValueOrDefault(line.PartId)).SuggestedUsd
                    : PricingService.Calculate(line.PartId, parts[line.PartId].StockUnit,
                        costs.GetValueOrDefault(line.PartId), pricingPolicy).SuggestedUsd;
                var exception = !counted[line.PartId] || shortages.Contains(line.PartId);
                var movement = StockMovement.StockOut(line.PartId, line.Quantity, actor,
                    shortages.Contains(line.PartId) ? "Counter sale: physical stock discrepancy; recount required" : "Counter sale",
                    sale.Id.ToString());
                db.StockMovements.Add(movement);
                sale.Lines.Add(SaleLine.Create(sale.Id, parts[line.PartId], i, line.Quantity, line.UnitSellingPriceUsd,
                    lineTotals[i], movement.Id, exception, shortages.Contains(line.PartId), line.StockExceptionReason,
                    costs.GetValueOrDefault(line.PartId), suggestion));
            }

            var tenders = request.Payments.Select(x => (Request: x, Change: false))
                .Concat(request.Change.Select(x => (Request: x, Change: true))).ToArray();
            var accountIds = tenders.Select(x => x.Request.CashAccountId).Distinct().ToArray();
            var accounts = await db.CashAccounts.AsNoTracking().Where(x => accountIds.Contains(x.Id) && x.IsActive)
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            for (var i = 0; i < tenders.Length; i++)
            {
                var (tender, isChange) = tenders[i];
                if (!accounts.TryGetValue(tender.CashAccountId, out var account) || account.Currency != tender.Currency)
                    throw new ConflictException("Each tender requires an active account matching its native currency.");
                var postedTender = SaleTender.Create(sale.Id, i, account, isChange, tender.NativeAmount, tender.ConfirmedUsdAmount);
                sale.Tenders.Add(postedTender);
                db.CashMovements.Add(CashMovement.SaleTender(postedTender, actor, sale.PostedAtUtc));
            }
            if (customer is not null)
            {
                db.CustomerAccountEntries.Add(CustomerAccountEntry.SaleCharge(customer.Id, sale.Id, request.OperationId, actor, total));
                if (payments > change)
                    db.CustomerAccountEntries.Add(CustomerAccountEntry.SalePayment(customer.Id, sale.Id, request.OperationId, actor, payments - change));
                // Credit already exists as a negative balance: charging the sale consumes it once.
                // Do not add a second debit for CustomerCreditUsedUsd.
            }
            db.Sales.Add(sale);
            if (paperRecovery is not null)
                db.PaperRecoveryEntries.Add(PaperRecoveryEntry.ForSale(paperRecovery.PaperReference,
                    sale.PostedAtUtc, paperRecovery.Reason, paperRecovery.OriginalSellerNote, sale.Id, actor, DateTime.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
            result = Map(sale); // Immutable outcome built before commit; no fallible result lookup afterward.
            foreach (var part in parts.Values)
                notifications.Add(new StockChangedNotification
                {
                    PartId = part.Id, PartNumber = part.PartNumber, PartName = part.Name,
                    QuantityOnHand = availableAfter[part.Id], LowStockThreshold = part.LowStockThreshold,
                    IsLowStock = counted[part.Id] && availableAfter[part.Id] <= part.LowStockThreshold,
                    StockConfidence = part.NeedsRecount ? StockConfidence.NeedsRecount : counted[part.Id] ? StockConfidence.Counted : StockConfidence.NotCounted,
                    MovementType = MovementType.Out, QuantityChange = -quantities[part.Id],
                    PerformedByUserName = currentUser.FullName ?? "Unknown", OccurredAtUtc = sale.PostedAtUtc
                });
            await tx.CommitAsync(cancellationToken);
        }
        foreach (var notification in notifications)
        {
            try { await notifier.StockChangedAsync(notification, cancellationToken); }
            catch (Exception ex) { logger.LogWarning(ex, "Refresh hint failed for committed sale {SaleId}.", result.Id); }
        }
        return result;
    }

    public async Task<SaleOperationDto> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var actor = await AuthorizedActor(cancellationToken);
        var sale = await Query().SingleOrDefaultAsync(x => x.OperationId == operationId && x.ActorId == actor, cancellationToken)
            ?? throw new NotFoundException("Sale operation", operationId);
        return new("Committed", Map(sale));
    }

    public async Task<SaleTenderAccountCorrectionDto> CorrectTenderAccountAsync(Guid tenderId,
        CorrectSaleTenderAccountRequest request, CancellationToken cancellationToken = default)
    {
        await new CorrectSaleTenderAccountRequestValidator().ValidateAndThrowAsync(request, cancellationToken);
        var fingerprint = CorrectionFingerprint(tenderId, request);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await OperationGuard.LockAsync(db, request.OperationId, "SaleTenderAccountCorrection", cancellationToken);
        var actor = await CounterAuthorization.RequireAsync(db, currentUser, cancellationToken, adminOnly: true);
        var replay = await db.SaleTenderAccountCorrections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.CorrectedByUserId != actor || replay.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID belongs to another actor or correction payload.");
            await tx.CommitAsync(cancellationToken);
            return Map(replay);
        }

        // Tender row first, then its two account rows. This never edits the sale or customer ledger.
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"SaleTenders\" WHERE \"Id\" = {tenderId} FOR UPDATE", cancellationToken);
        var original = await db.SaleTenders.SingleOrDefaultAsync(x => x.Id == tenderId, cancellationToken)
            ?? throw new NotFoundException("Sale tender", tenderId);
        if (await db.SaleTenderAccountCorrections.AnyAsync(x => x.OriginalSaleTenderId == tenderId, cancellationToken))
            throw new ConflictException("This sale tender already has an account correction.");
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"CashAccounts\" WHERE \"Id\" IN ({original.CashAccountId}, {request.CorrectCashAccountId}) ORDER BY \"Id\" FOR UPDATE", cancellationToken);
        var replacement = await db.CashAccounts.SingleOrDefaultAsync(x => x.Id == request.CorrectCashAccountId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Active corrected cash account", request.CorrectCashAccountId);
        var correction = SaleTenderAccountCorrection.Create(original, replacement, request.Reason, actor,
            request.OperationId, fingerprint, DateTime.UtcNow);
        db.SaleTenderAccountCorrections.Add(correction);
        // Dated now: a historical close is never rewritten. Reconciliation will show this as post-close where applicable.
        db.CashMovements.Add(CashMovement.SaleTenderAccountCorrection(original.CashAccountId, -original.NativeAmount,
            false, actor, correction.CorrectedAtUtc, correction.Id));
        db.CashMovements.Add(CashMovement.SaleTenderAccountCorrection(replacement.Id, original.NativeAmount,
            true, actor, correction.CorrectedAtUtc, correction.Id));
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(correction);
    }

    public async Task<SaleTenderAccountCorrectionDto> GetTenderAccountCorrectionOperationAsync(Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var actor = await CounterAuthorization.RequireAsync(db, currentUser, cancellationToken, adminOnly: true);
        var correction = await db.SaleTenderAccountCorrections.AsNoTracking().SingleOrDefaultAsync(
            x => x.OperationId == operationId && x.CorrectedByUserId == actor, cancellationToken)
            ?? throw new NotFoundException("Sale tender account correction operation", operationId);
        return Map(correction);
    }

    public async Task<SaleTenderAccountCorrectionReversalDto> ReverseTenderAccountCorrectionAsync(Guid correctionId,
        ReverseSaleTenderAccountCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        await new ReverseSaleTenderAccountCorrectionRequestValidator().ValidateAndThrowAsync(request, cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { correctionId, Reason = request.Reason.Trim() }))));
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await OperationGuard.LockAsync(db, request.OperationId, "SaleTenderAccountCorrectionReversal", cancellationToken);
        var actor = await CounterAuthorization.RequireAsync(db, currentUser, cancellationToken, adminOnly: true);
        var replay = await db.SaleTenderAccountCorrectionReversals.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.ReversedByUserId != actor || replay.PayloadFingerprint != fingerprint) throw new ConflictException("This operation ID belongs to another actor or reversal payload.");
            await tx.CommitAsync(cancellationToken); return Map(replay);
        }
        var correction = await db.SaleTenderAccountCorrections.SingleOrDefaultAsync(x => x.Id == correctionId, cancellationToken)
            ?? throw new NotFoundException("Sale tender account correction", correctionId);
        if (await db.SaleTenderAccountCorrectionReversals.AnyAsync(x => x.SaleTenderAccountCorrectionId == correctionId, cancellationToken))
            throw new ConflictException("This tender-account correction already has a reversal.");
        var reversal = SaleTenderAccountCorrectionReversal.Create(correctionId, request.Reason, actor, request.OperationId, fingerprint, DateTime.UtcNow);
        db.SaleTenderAccountCorrectionReversals.Add(reversal);
        db.CashMovements.Add(CashMovement.SaleTenderAccountCorrectionReversal(correction.CorrectCashAccountId, -correction.NativeAmount, actor, reversal.ReversedAtUtc, reversal.Id));
        db.CashMovements.Add(CashMovement.SaleTenderAccountCorrectionReversal(correction.OriginalCashAccountId, correction.NativeAmount, actor, reversal.ReversedAtUtc, reversal.Id));
        await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return Map(reversal);
    }

    public async Task<IReadOnlyList<SalePaymentAccountDto>> GetPaymentAccountsAsync(CancellationToken cancellationToken = default)
    {
        await AuthorizedActor(cancellationToken);
        return await db.CashAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new SalePaymentAccountDto(x.Id, x.Name, x.Currency, x.Kind)).ToListAsync(cancellationToken);
    }

    public async Task<SaleDto> GetAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        await AuthorizedActor(cancellationToken);
        var sale = await Query().SingleOrDefaultAsync(x => x.Id == saleId, cancellationToken)
            ?? throw new NotFoundException("Posted sale", saleId);
        return Map(sale);
    }

    public async Task<IReadOnlyList<SaleSummaryDto>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        await AuthorizedActor(cancellationToken);
        return await db.Sales.AsNoTracking().OrderByDescending(x => x.PostedAtUtc).ThenByDescending(x => x.Id)
            .Take(30).Select(x => new SaleSummaryDto(x.Id, x.PostedAtUtc, x.CustomerName,
                x.TotalUsd, x.Lines.Count)).ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<SaleLookupDto>> SearchAsync(SaleSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        await AuthorizedActor(cancellationToken);
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc >= query.ToUtc)
            throw new ValidationException("The sale search end must be after its start.");
        if (query.AmountUsd is < 0)
            throw new ValidationException("The sale amount cannot be negative.");

        IQueryable<Sale> source = db.Sales.AsNoTracking();
        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value.UtcDateTime;
            source = source.Where(x => x.PostedAtUtc >= from);
        }
        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value.UtcDateTime;
            source = source.Where(x => x.PostedAtUtc < to);
        }
        if (query.AmountUsd.HasValue)
        {
            var amount = decimal.Round(query.AmountUsd.Value, 2, MidpointRounding.AwayFromZero);
            source = source.Where(x => x.TotalUsd == amount);
        }
        if (!string.IsNullOrWhiteSpace(query.Customer))
        {
            var pattern = LiteralContains(query.Customer);
            var customerIds = db.Customers.Where(x => EF.Functions.ILike(x.Name, pattern, "\\") ||
                (x.DistinguishingLabel != null && EF.Functions.ILike(x.DistinguishingLabel, pattern, "\\")) ||
                (x.Phone != null && EF.Functions.ILike(x.Phone, pattern, "\\"))).Select(x => x.Id);
            source = source.Where(x => (x.CustomerName != null && EF.Functions.ILike(x.CustomerName, pattern, "\\")) ||
                (x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value)));
        }
        if (!string.IsNullOrWhiteSpace(query.Part))
        {
            var rawPattern = LiteralContains(query.Part);
            var partText = query.Part.Trim().ToUpperInvariant();
            var number = partText.Replace(" ", "").Replace("-", "");
            var partIds = db.Parts.Where(x => x.SearchNumber.Contains(number) || x.SearchName.Contains(partText))
                .Select(x => x.Id);
            source = source.Where(x => x.Lines.Any(line => partIds.Contains(line.PartId) ||
                EF.Functions.ILike(line.PartNumber, rawPattern, "\\") || EF.Functions.ILike(line.Name, rawPattern, "\\")));
        }

        var count = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(x => x.PostedAtUtc).ThenByDescending(x => x.Id)
            .Skip(query.Skip).Take(query.PageSize)
            .Select(x => new SaleLookupDto(x.Id, x.PostedAtUtc, x.CustomerId, x.CustomerName, x.TotalUsd,
                x.Lines.OrderBy(line => line.Position).Select(line => new SaleLookupLineDto(line.PartId,
                    line.PartNumber, line.Name, line.Brand, line.Variant, line.StockUnit, line.Quantity)).ToArray()))
            .ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    private Task<Guid> AuthorizedActor(CancellationToken ct) => CounterAuthorization.RequireAsync(db, currentUser, ct);
    private IQueryable<Sale> Query() => db.Sales.AsNoTracking().Include(x => x.Lines).Include(x => x.Tenders).AsSplitQuery();
    private static SaleDto Map(Sale s) => new(s.Id, s.ReceiptNumber, s.OperationId, s.ActorId, s.PostedAtUtc, s.CustomerId, s.CustomerName,
        Normalize(s.TotalUsd), Normalize(s.PaymentsUsd), Normalize(s.ChangeUsd), Normalize(s.ExchangeCreditUsd), Normalize(s.CustomerCreditUsedUsd), Normalize(s.UnpaidDebtUsd),
        s.Lines.OrderBy(x => x.Position).Select(x => new SaleLineDto(x.Id, x.PartId, x.Position, x.PartNumber, x.Name,
            x.Brand, x.Variant, x.StockUnit, x.Quantity, Normalize(x.UnitSellingPriceUsd), Normalize(x.LineTotalUsd), x.StockException, x.StockShortage, x.StockExceptionReason,
            x.SuggestedUnitPriceUsd is null ? null : Normalize(x.SuggestedUnitPriceUsd.Value), x.PriceOverridden)).ToArray(),
        s.Tenders.OrderBy(x => x.Position).Select(x => new SaleTenderDto(x.Id, x.CashAccountId, x.AccountName,
            x.Currency, x.IsChange, Normalize(x.NativeAmount), Normalize(x.ConfirmedUsdAmount))).ToArray());
    private static decimal Normalize(decimal x) => decimal.Parse(Number(x), CultureInfo.InvariantCulture);
    private static string LiteralContains(string value) => "%" + value.Trim().Replace("\\", "\\\\")
        .Replace("%", "\\%").Replace("_", "\\_") + "%";
    private static string Number(decimal x) => x.ToString("G29", CultureInfo.InvariantCulture);
    private static string CorrectionFingerprint(Guid tenderId, CorrectSaleTenderAccountRequest request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { tenderId, request.CorrectCashAccountId, Reason = request.Reason.Trim() }))));
    private static SaleTenderAccountCorrectionDto Map(SaleTenderAccountCorrection correction) => new(correction.Id,
        correction.OperationId, correction.OriginalSaleTenderId, correction.OriginalCashAccountId,
        correction.CorrectCashAccountId, correction.Currency, Normalize(correction.NativeAmount),
        Normalize(correction.ConfirmedUsdAmount), correction.Reason, correction.CorrectedAtUtc, correction.CorrectedByUserId);
    private static SaleTenderAccountCorrectionReversalDto Map(SaleTenderAccountCorrectionReversal reversal) => new(reversal.Id,
        reversal.OperationId, reversal.SaleTenderAccountCorrectionId, reversal.Reason, reversal.ReversedAtUtc, reversal.ReversedByUserId);
    private static string Fingerprint(PostSaleRequest r)
    {
        // Structured serialization avoids ambiguous delimiter-based fingerprints in free text.
        var canonical = JsonSerializer.Serialize(new
        {
            r.CustomerId, Total = Number(r.ConfirmedTotalUsd), Credit = Number(r.CustomerCreditUsedUsd), Debt = Number(r.UnpaidDebtUsd),
            Lines = r.Lines.Select(x => new { x.PartId, x.Quantity, Price = Number(x.UnitSellingPriceUsd), x.PhysicalGoodsConfirmed, Reason = x.StockExceptionReason?.Trim() }),
            Payments = r.Payments.Select(x => new { x.CashAccountId, x.Currency, Amount = Number(x.NativeAmount), Usd = Number(x.ConfirmedUsdAmount) }),
            Change = r.Change.Select(x => new { x.CashAccountId, x.Currency, Amount = Number(x.NativeAmount), Usd = Number(x.ConfirmedUsdAmount) })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string PaperRecoveryFingerprint(PostSaleRequest request, PaperRecoveryContext recovery) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Sale = Fingerprint(request), recovery.OriginalOccurredAtUtc, recovery.PaperReference, recovery.Reason,
            recovery.OriginalSellerNote
        }))));

    private sealed record PaperRecoveryContext(DateTime OriginalOccurredAtUtc, string PaperReference,
        string Reason, string? OriginalSellerNote);
}
