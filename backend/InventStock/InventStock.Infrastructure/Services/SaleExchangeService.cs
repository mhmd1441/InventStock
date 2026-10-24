using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using InventStock.Application.Services;
using InventStock.Application.Validators;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventStock.Infrastructure.Services;

/// <summary>Posts both legs of an exchange inside one database transaction.</summary>
public sealed class SaleExchangeService(AppDbContext db, ICurrentUserService user, IStockNotifier notifier,
    ILogger<SaleExchangeService> logger) : ISaleExchangeService
{
    public async Task<SaleExchangeDto> PostAsync(PostSaleExchangeRequest request, CancellationToken ct = default)
    {
        await new PostSaleExchangeRequestValidator().ValidateAndThrowAsync(request, ct);
        var fingerprint = Fingerprint(request);
        var notifications = new List<StockChangedNotification>();
        SaleExchangeDto result;

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await OperationGuard.LockAsync(db, request.OperationId, "SaleExchange", ct);
            var actor = await CounterAuthorization.RequireAsync(db, user, ct);
            var replay = await Query().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
            if (replay is not null)
            {
                if (replay.ActorId != actor || replay.PayloadFingerprint != fingerprint)
                    throw new ConflictException("This operation ID belongs to another actor or exchange payload.");
                return Map(replay);
            }

            var originalSale = await db.Sales.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.OriginalSaleId, ct)
                ?? throw new NotFoundException("Posted sale", request.OriginalSaleId);

            // Global order: operation -> customer -> original lines -> all distinct SKUs.
            Customer? customer = null;
            if (originalSale.CustomerId.HasValue)
            {
                if (db.Database.IsNpgsql())
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM \"Customers\" WHERE \"Id\" = {originalSale.CustomerId.Value} FOR UPDATE", ct);
                customer = await db.Customers.SingleOrDefaultAsync(
                    x => x.Id == originalSale.CustomerId.Value && x.IsActive, ct)
                    ?? throw new ConflictException("The original sale customer is not active. Review the exchange before posting.");
            }
            else if (request.AccountReductionUsd != 0 || request.CustomerCreditUsedUsd != 0 || request.UnpaidDebtUsd != 0)
            {
                throw new ConflictException("An anonymous exchange cannot use customer credit, reduce an account, or leave debt.");
            }

            var returnLineIds = request.ReturnLines.Select(x => x.SaleLineId).Order().ToArray();
            foreach (var id in returnLineIds)
                if (db.Database.IsNpgsql())
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM \"SaleLines\" WHERE \"Id\" = {id} FOR UPDATE", ct);
            var originals = await db.SaleLines.AsNoTracking()
                .Where(x => returnLineIds.Contains(x.Id) && x.SaleId == originalSale.Id)
                .ToDictionaryAsync(x => x.Id, ct);
            if (originals.Count != returnLineIds.Length)
                throw new ConflictException("Every exchanged return line must belong to the selected original sale.");

            var prior = await db.SaleReturnLines.AsNoTracking().Where(x => returnLineIds.Contains(x.SaleLineId))
                .GroupBy(x => x.SaleLineId)
                .Select(g => new { Id = g.Key, Quantity = g.Sum(x => x.Quantity), Refunded = g.Sum(x => x.RefundUsd) })
                .ToDictionaryAsync(x => x.Id, ct);

            var partIds = originals.Values.Select(x => x.PartId)
                .Concat(request.ReplacementLines.Select(x => x.PartId)).Distinct().Order().ToArray();
            foreach (var id in partIds)
                if (db.Database.IsNpgsql())
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {id} FOR UPDATE", ct);
            var parts = await db.Parts.Where(x => partIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            if (originals.Values.Any(x => !parts.ContainsKey(x.PartId)) ||
                request.ReplacementLines.Any(x => !parts.TryGetValue(x.PartId, out var p) || !p.IsActive))
                throw new ConflictException("Every replacement line requires an active SKU; returned SKUs must still exist.");

            (SaleReturnLineRequest Request, SaleLine Original, decimal Refund)[] allocations;
            try
            {
                allocations = request.ReturnLines.Select(line =>
                {
                    var original = originals[line.SaleLineId];
                    var before = prior.GetValueOrDefault(line.SaleLineId);
                    var refund = ReturnAllocation.AdditionalRefund(original.LineTotalUsd, original.Quantity,
                        before?.Quantity ?? 0, before?.Refunded ?? 0, line.Quantity);
                    return (line, original, refund);
                }).ToArray();
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ConflictException("An original line has insufficient unreturned quantity. Refresh before exchanging it.");
            }

            var returnTotal = allocations.Sum(x => x.Refund);
            var refundUsd = request.Refunds.Sum(x => x.ConfirmedUsdAmount);
            if (returnTotal != request.ConfirmedReturnTotalUsd ||
                returnTotal != request.ReturnAppliedUsd + refundUsd + request.AccountReductionUsd)
                throw new ConflictException("Returned value must equal value applied to the replacement plus explicit refunds and account reduction.");

            var replacementLineTotals = request.ReplacementLines
                .Select(x => decimal.Round(x.UnitSellingPriceUsd * x.Quantity, 2, MidpointRounding.AwayFromZero)).ToArray();
            var replacementTotal = replacementLineTotals.Sum();
            var payments = request.Payments.Sum(x => x.ConfirmedUsdAmount);
            var change = request.Change.Sum(x => x.ConfirmedUsdAmount);
            if (replacementTotal != request.ConfirmedReplacementTotalUsd || payments < change ||
                replacementTotal != request.ReturnAppliedUsd + payments - change +
                    request.CustomerCreditUsedUsd + request.UnpaidDebtUsd)
                throw new ConflictException("Replacement total must equal returned value applied plus payments minus change plus customer credit and unpaid debt.");

            if (customer is not null)
            {
                var balance = await db.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id)
                    .SumAsync(x => (decimal?)x.AmountUsd, ct) ?? 0;
                if (request.CustomerCreditUsedUsd > Math.Max(0, -balance))
                    throw new ConflictException("The customer does not have enough pre-existing credit.");
            }

            var outgoing = request.ReplacementLines.GroupBy(x => x.PartId)
                .ToDictionary(x => x.Key, x => checked(x.Sum(y => y.Quantity)));
            var returnedSellable = allocations.Where(x => x.Request.Condition == ReturnCondition.Sellable)
                .GroupBy(x => x.Original.PartId).ToDictionary(x => x.Key, x => checked(x.Sum(y => y.Request.Quantity)));
            var stock = await db.StockMovements.Where(x => partIds.Contains(x.PartId)).GroupBy(x => x.PartId)
                .Select(g => new { PartId = g.Key, Physical = g.Sum(x => x.QuantityChange), Counted = g.Any(x => x.CountedQuantity != null) })
                .ToDictionaryAsync(x => x.PartId, ct);
            var holds = await db.StockHoldMovements.Where(x => partIds.Contains(x.PartId)).GroupBy(x => x.PartId)
                .Select(g => new { PartId = g.Key, Quantity = g.Sum(x => x.QuantityChange) })
                .ToDictionaryAsync(x => x.PartId, ct);
            var shortages = new HashSet<Guid>();
            var availableAfter = new Dictionary<Guid, int>();
            foreach (var id in partIds)
            {
                var physical = stock.GetValueOrDefault(id)?.Physical ?? 0;
                var held = holds.GetValueOrDefault(id)?.Quantity ?? 0;
                availableAfter[id] = physical - held + returnedSellable.GetValueOrDefault(id) - outgoing.GetValueOrDefault(id);
                if (!outgoing.ContainsKey(id)) continue;
                var lines = request.ReplacementLines.Where(x => x.PartId == id).ToArray();
                if (availableAfter[id] < 0)
                {
                    if (held > 0 || parts[id].NeedsRecount ||
                        lines.Any(x => !x.PhysicalGoodsConfirmed || string.IsNullOrWhiteSpace(x.StockExceptionReason)))
                        throw new InsufficientStockException(parts[id].PartNumber,
                            physical - held + returnedSellable.GetValueOrDefault(id), outgoing[id]);
                    shortages.Add(id);
                    parts[id].RequireRecount();
                }
                if (!(stock.GetValueOrDefault(id)?.Counted ?? false) && lines.Any(x => !x.PhysicalGoodsConfirmed))
                    throw new ConflictException("Uncounted replacement stock requires physical confirmation and a reason.");
            }

            var returnOperation = DerivedOperation(request.OperationId, "return");
            var saleOperation = DerivedOperation(request.OperationId, "replacement");
            var postedReturn = SaleReturn.Create(originalSale, returnOperation, actor, fingerprint, request.Reason,
                returnTotal, request.AccountReductionUsd, request.ReturnAppliedUsd);
            var postedSale = Sale.Create(saleOperation, fingerprint, actor, customer, replacementTotal,
                payments, change, request.CustomerCreditUsedUsd, request.UnpaidDebtUsd, request.ReturnAppliedUsd);

            for (var i = 0; i < allocations.Length; i++)
            {
                var (line, original, refund) = allocations[i];
                var movement = StockMovement.StockIn(original.PartId, line.Quantity, actor,
                    "Exchange return", postedReturn.Id.ToString());
                db.StockMovements.Add(movement);
                Guid? holdId = null;
                if (line.Condition == ReturnCondition.Quarantined)
                {
                    var hold = StockHoldMovement.Create(original.PartId, StockHoldAction.Hold, line.Quantity,
                        line.IssueReason!, line.PhysicalSeparationConfirmed, actor, Guid.NewGuid(), fingerprint);
                    db.StockHoldMovements.Add(hold);
                    holdId = hold.Id;
                }
                postedReturn.Lines.Add(SaleReturnLine.Create(postedReturn.Id, original, i, line.Quantity,
                    refund, line.Condition, movement.Id, holdId, line.IssueReason));
            }

            var allTenderRequests = request.Refunds.Select(x => (Request: x, Kind: "refund"))
                .Concat(request.Payments.Select(x => (Request: x, Kind: "payment")))
                .Concat(request.Change.Select(x => (Request: x, Kind: "change"))).ToArray();
            var accountIds = allTenderRequests.Select(x => x.Request.CashAccountId).Distinct().ToArray();
            var accounts = await db.CashAccounts.AsNoTracking().Where(x => accountIds.Contains(x.Id) && x.IsActive)
                .ToDictionaryAsync(x => x.Id, ct);
            CashAccount Account(SaleTenderRequest tender)
            {
                if (!accounts.TryGetValue(tender.CashAccountId, out var account) || account.Currency != tender.Currency)
                    throw new ConflictException("Every exchange tender requires an active account matching its native currency.");
                return account;
            }

            for (var i = 0; i < request.Refunds.Count; i++)
            {
                var item = request.Refunds[i];
                var tender = SaleRefundTender.Create(postedReturn.Id, i, Account(item), item.NativeAmount, item.ConfirmedUsdAmount);
                postedReturn.Refunds.Add(tender);
                db.CashMovements.Add(CashMovement.SaleRefund(tender, actor, postedReturn.PostedAtUtc));
            }

            var outgoingPartIds = outgoing.Keys.ToArray();
            var costs = await db.SupplierCostObservations.AsNoTracking()
                .Where(x => outgoingPartIds.Contains(x.PartId) && x.Source == CostSource.ActualPurchase &&
                    x.ObservedAtUtc <= postedSale.PostedAtUtc)
                .GroupBy(x => x.PartId).Select(g => g.OrderByDescending(x => x.ObservedAtUtc)
                    .ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).First())
                .ToDictionaryAsync(x => x.PartId, ct);
            var cutHoseIds = outgoingPartIds.Where(x => parts[x].StockUnit == StockUnit.Centimeter).ToArray();
            var cutHoseRates = await db.CutHoseSellingRates.AsNoTracking().Where(x => cutHoseIds.Contains(x.PartId))
                .GroupBy(x => x.PartId).Select(g => g.OrderByDescending(x => x.Revision).First())
                .ToDictionaryAsync(x => x.PartId, ct);
            var policy = await db.PricingPolicies.AsNoTracking().SingleAsync(x => x.Id == 1, ct);
            for (var i = 0; i < request.ReplacementLines.Count; i++)
            {
                var line = request.ReplacementLines[i];
                var part = parts[line.PartId];
                var suggestion = part.StockUnit == StockUnit.Centimeter
                    ? PricingService.CalculateCutHose(part.Id, cutHoseRates.GetValueOrDefault(part.Id)).SuggestedUsd
                    : PricingService.Calculate(part.Id, part.StockUnit, costs.GetValueOrDefault(part.Id), policy).SuggestedUsd;
                var counted = stock.GetValueOrDefault(part.Id)?.Counted ?? false;
                var movement = StockMovement.StockOut(part.Id, line.Quantity, actor,
                    shortages.Contains(part.Id) ? "Exchange replacement: physical stock discrepancy; recount required" : "Exchange replacement",
                    postedSale.Id.ToString());
                db.StockMovements.Add(movement);
                postedSale.Lines.Add(SaleLine.Create(postedSale.Id, part, i, line.Quantity,
                    line.UnitSellingPriceUsd, replacementLineTotals[i], movement.Id,
                    !counted || shortages.Contains(part.Id), shortages.Contains(part.Id), line.StockExceptionReason,
                    costs.GetValueOrDefault(part.Id), suggestion));
            }

            var saleTenders = request.Payments.Select(x => (Request: x, Change: false))
                .Concat(request.Change.Select(x => (Request: x, Change: true))).ToArray();
            for (var i = 0; i < saleTenders.Length; i++)
            {
                var (item, isChange) = saleTenders[i];
                var tender = SaleTender.Create(postedSale.Id, i, Account(item), isChange,
                    item.NativeAmount, item.ConfirmedUsdAmount);
                postedSale.Tenders.Add(tender);
                db.CashMovements.Add(CashMovement.SaleTender(tender, actor, postedSale.PostedAtUtc));
            }

            db.SaleReturns.Add(postedReturn);
            db.Sales.Add(postedSale);
            var exchange = SaleExchange.Create(request.OperationId, fingerprint, actor, originalSale,
                postedReturn, postedSale, request.ReturnAppliedUsd, refundUsd, request.AccountReductionUsd);
            db.SaleExchanges.Add(exchange);
            if (customer is not null)
            {
                db.CustomerAccountEntries.Add(CustomerAccountEntry.SaleCharge(customer.Id, postedSale.Id,
                    saleOperation, actor, replacementTotal));
                if (payments > change)
                    db.CustomerAccountEntries.Add(CustomerAccountEntry.SalePayment(customer.Id, postedSale.Id,
                        saleOperation, actor, payments - change));
                var returnAccountValue = request.ReturnAppliedUsd + request.AccountReductionUsd;
                if (returnAccountValue > 0)
                    db.CustomerAccountEntries.Add(CustomerAccountEntry.ExchangeReturnValue(customer.Id,
                        exchange.Id, returnOperation, actor, returnAccountValue));
            }

            await db.SaveChangesAsync(ct);
            result = Map(exchange);
            foreach (var id in partIds)
            {
                var quantityChange = allocations.Where(x => x.Original.PartId == id).Sum(x => x.Request.Quantity) - outgoing.GetValueOrDefault(id);
                if (quantityChange == 0) continue;
                var counted = stock.GetValueOrDefault(id)?.Counted ?? false;
                notifications.Add(new StockChangedNotification
                {
                    PartId = id, PartNumber = parts[id].PartNumber, PartName = parts[id].Name,
                    QuantityOnHand = availableAfter[id], LowStockThreshold = parts[id].LowStockThreshold,
                    IsLowStock = counted && availableAfter[id] <= parts[id].LowStockThreshold,
                    StockConfidence = parts[id].NeedsRecount ? StockConfidence.NeedsRecount : counted ? StockConfidence.Counted : StockConfidence.NotCounted,
                    MovementType = quantityChange > 0 ? MovementType.In : MovementType.Out,
                    QuantityChange = quantityChange, PerformedByUserName = user.FullName ?? "Unknown",
                    OccurredAtUtc = exchange.PostedAtUtc
                });
            }
            await tx.CommitAsync(ct);
        }

        foreach (var notification in notifications)
        {
            try { await notifier.StockChangedAsync(notification, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Refresh hint failed for committed exchange {ExchangeId}.", result.Id); }
        }
        return result;
    }

    public async Task<SaleExchangeDto> GetOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var actor = await CounterAuthorization.RequireAsync(db, user, ct);
        return Map(await Query().SingleOrDefaultAsync(x => x.OperationId == operationId && x.ActorId == actor, ct)
            ?? throw new NotFoundException("Sale exchange operation", operationId));
    }

    private IQueryable<SaleExchange> Query() => db.SaleExchanges.AsNoTracking()
        .Include(x => x.SaleReturn).ThenInclude(x => x.Lines)
        .Include(x => x.SaleReturn).ThenInclude(x => x.Refunds)
        .Include(x => x.ReplacementSale).ThenInclude(x => x.Lines)
        .Include(x => x.ReplacementSale).ThenInclude(x => x.Tenders)
        .AsSplitQuery();

    private static SaleExchangeDto Map(SaleExchange e)
    {
        return new SaleExchangeDto(e.Id, e.OperationId, e.ActorId, e.OriginalSaleId, e.CustomerId,
            e.PostedAtUtc, Normalize(e.ReturnTotalUsd), Normalize(e.ReturnAppliedUsd), Normalize(e.RefundUsd),
            Normalize(e.AccountReductionUsd), Normalize(e.ReplacementTotalUsd), MapReturn(e.SaleReturn),
            MapSale(e.ReplacementSale));
    }

    private static SaleReturnDto MapReturn(SaleReturn r) => new(r.Id, r.OperationId, r.ActorId, r.SaleId,
        r.CustomerId, r.PostedAtUtc, r.Reason, Normalize(r.TotalUsd), Normalize(r.AccountReductionUsd),
        r.Lines.OrderBy(x => x.Position).Select(x => new SaleReturnLineDto(x.Id, x.SaleLineId, x.PartId,
            x.Quantity, Normalize(x.RefundUsd), x.Condition, x.IssueReason)).ToArray(),
        r.Refunds.OrderBy(x => x.Position).Select(x => new SaleRefundTenderDto(x.Id, x.CashAccountId,
            x.AccountName, x.Currency, Normalize(x.NativeAmount), Normalize(x.ConfirmedUsdAmount))).ToArray());

    private static SaleDto MapSale(Sale s) => new(s.Id, s.ReceiptNumber, s.OperationId, s.ActorId, s.PostedAtUtc,
        s.CustomerId, s.CustomerName, Normalize(s.TotalUsd), Normalize(s.PaymentsUsd), Normalize(s.ChangeUsd),
        Normalize(s.ExchangeCreditUsd), Normalize(s.CustomerCreditUsedUsd), Normalize(s.UnpaidDebtUsd),
        s.Lines.OrderBy(x => x.Position).Select(x => new SaleLineDto(x.Id, x.PartId, x.Position,
            x.PartNumber, x.Name, x.Brand, x.Variant, x.StockUnit, x.Quantity,
            Normalize(x.UnitSellingPriceUsd), Normalize(x.LineTotalUsd), x.StockException, x.StockShortage,
            x.StockExceptionReason, x.SuggestedUnitPriceUsd is null ? null : Normalize(x.SuggestedUnitPriceUsd.Value),
            x.PriceOverridden)).ToArray(),
        s.Tenders.OrderBy(x => x.Position).Select(x => new SaleTenderDto(x.Id, x.CashAccountId,
            x.AccountName, x.Currency, x.IsChange, Normalize(x.NativeAmount),
            Normalize(x.ConfirmedUsdAmount))).ToArray());

    private static Guid DerivedOperation(Guid operationId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:N}:{purpose}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        return new Guid(guid);
    }

    private static decimal Normalize(decimal value) => decimal.Parse(Number(value), CultureInfo.InvariantCulture);
    private static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    private static string Fingerprint(PostSaleExchangeRequest r)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            r.OriginalSaleId, Reason = r.Reason.Trim(), ReturnTotal = Number(r.ConfirmedReturnTotalUsd),
            Applied = Number(r.ReturnAppliedUsd), Account = Number(r.AccountReductionUsd),
            ReturnLines = r.ReturnLines.Select(x => new { x.SaleLineId, x.Quantity, x.Condition,
                x.PhysicalSeparationConfirmed, Reason = x.IssueReason?.Trim() }),
            Refunds = r.Refunds.Select(Tender), ReplacementTotal = Number(r.ConfirmedReplacementTotalUsd),
            Credit = Number(r.CustomerCreditUsedUsd), Debt = Number(r.UnpaidDebtUsd),
            ReplacementLines = r.ReplacementLines.Select(x => new { x.PartId, x.Quantity,
                Price = Number(x.UnitSellingPriceUsd), x.PhysicalGoodsConfirmed,
                Reason = x.StockExceptionReason?.Trim() }),
            Payments = r.Payments.Select(Tender), Change = r.Change.Select(Tender)
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static object Tender(SaleTenderRequest x) => new
    {
        x.CashAccountId, x.Currency, Native = Number(x.NativeAmount), Usd = Number(x.ConfirmedUsdAmount)
    };
}
