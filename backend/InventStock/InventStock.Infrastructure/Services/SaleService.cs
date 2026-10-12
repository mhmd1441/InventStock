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
    public async Task<SaleDto> PostAsync(PostSaleRequest request, CancellationToken cancellationToken = default)
    {
        await new PostSaleRequestValidator().ValidateAndThrowAsync(request, cancellationToken);
        var fingerprint = Fingerprint(request);
        SaleDto result;
        var notifications = new List<StockChangedNotification>();
        // This service owns the entire transaction. Do not call other independently committing services.
        await using (var tx = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await OperationGuard.LockAsync(db, request.OperationId, "Sale", cancellationToken);
            var actor = await AuthorizedActor(cancellationToken);
            var replay = await Query().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
            if (replay is not null)
            {
                if (replay.ActorId != actor || replay.PayloadFingerprint != fingerprint)
                    throw new ConflictException("This operation ID belongs to another actor or sale payload.");
                result = Map(replay);
                await tx.CommitAsync(cancellationToken);
                return result;
            }

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
            var parts = await db.Parts.AsNoTracking().Where(x => partIds.Contains(x.Id) && x.IsActive)
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
            foreach (var id in partIds)
            {
                if (!parts.TryGetValue(id, out var part)) throw new NotFoundException("Active part", id);
                var physical = stock.GetValueOrDefault(id)?.Physical ?? 0;
                var held = holds.GetValueOrDefault(id)?.Quantity ?? 0;
                counted[id] = stock.GetValueOrDefault(id)?.Counted ?? false;
                availableAfter[id] = physical - held - quantities[id];
                // Insufficient/negative-stock exception posting is deliberately deferred. A promise
                // from a supplier cannot produce stock, and physical confirmation cannot release holds.
                if (availableAfter[id] < 0)
                    throw new InsufficientStockException(part.PartNumber, physical - held, quantities[id]);
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
                request.CustomerCreditUsedUsd, request.UnpaidDebtUsd);
            // One query for latest actual cost per SKU. Quotes and unknown costs never become a fabricated cost basis.
            var costs = await db.SupplierCostObservations.AsNoTracking()
                .Where(x => partIds.Contains(x.PartId) && x.Source == CostSource.ActualPurchase && x.ObservedAtUtc <= sale.PostedAtUtc)
                .GroupBy(x => x.PartId).Select(g => g.OrderByDescending(x => x.ObservedAtUtc).ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).First())
                .ToDictionaryAsync(x => x.PartId, cancellationToken);
            for (var i = 0; i < request.Lines.Count; i++)
            {
                var line = request.Lines[i];
                var movement = StockMovement.StockOut(line.PartId, line.Quantity, actor, "Counter sale", sale.Id.ToString());
                db.StockMovements.Add(movement);
                sale.Lines.Add(SaleLine.Create(sale.Id, parts[line.PartId], i, line.Quantity, line.UnitSellingPriceUsd,
                    lineTotals[i], movement.Id, !counted[line.PartId], line.StockExceptionReason, costs.GetValueOrDefault(line.PartId)));
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
            await db.SaveChangesAsync(cancellationToken);
            result = Map(sale); // Immutable outcome built before commit; no fallible result lookup afterward.
            foreach (var part in parts.Values)
                notifications.Add(new StockChangedNotification
                {
                    PartId = part.Id, PartNumber = part.PartNumber, PartName = part.Name,
                    QuantityOnHand = availableAfter[part.Id], LowStockThreshold = part.LowStockThreshold,
                    IsLowStock = counted[part.Id] && availableAfter[part.Id] <= part.LowStockThreshold,
                    StockConfidence = counted[part.Id] ? StockConfidence.Counted : StockConfidence.NotCounted,
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

    public async Task<IReadOnlyList<SalePaymentAccountDto>> GetPaymentAccountsAsync(CancellationToken cancellationToken = default)
    {
        await AuthorizedActor(cancellationToken);
        return await db.CashAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new SalePaymentAccountDto(x.Id, x.Name, x.Currency, x.Kind)).ToListAsync(cancellationToken);
    }

    private Task<Guid> AuthorizedActor(CancellationToken ct) => CounterAuthorization.RequireAsync(db, currentUser, ct);
    private IQueryable<Sale> Query() => db.Sales.AsNoTracking().Include(x => x.Lines).Include(x => x.Tenders).AsSplitQuery();
    private static SaleDto Map(Sale s) => new(s.Id, s.OperationId, s.ActorId, s.PostedAtUtc, s.CustomerId, s.CustomerName,
        Normalize(s.TotalUsd), Normalize(s.PaymentsUsd), Normalize(s.ChangeUsd), Normalize(s.CustomerCreditUsedUsd), Normalize(s.UnpaidDebtUsd),
        s.Lines.OrderBy(x => x.Position).Select(x => new SaleLineDto(x.Id, x.PartId, x.Position, x.PartNumber, x.Name,
            x.Brand, x.Variant, x.StockUnit, x.Quantity, Normalize(x.UnitSellingPriceUsd), Normalize(x.LineTotalUsd), x.StockException, x.StockExceptionReason)).ToArray(),
        s.Tenders.OrderBy(x => x.Position).Select(x => new SaleTenderDto(x.Id, x.CashAccountId, x.AccountName,
            x.Currency, x.IsChange, Normalize(x.NativeAmount), Normalize(x.ConfirmedUsdAmount))).ToArray());
    private static decimal Normalize(decimal x) => decimal.Parse(Number(x), CultureInfo.InvariantCulture);
    private static string Number(decimal x) => x.ToString("G29", CultureInfo.InvariantCulture);
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
}
