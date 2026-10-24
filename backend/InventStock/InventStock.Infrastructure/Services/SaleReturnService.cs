using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.Interfaces;
using InventStock.Application.Services;
using InventStock.Application.Validators;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Services;

public sealed class SaleReturnService(AppDbContext db, ICurrentUserService user) : ISaleReturnService
{
    public async Task<LegacySaleReturnDto> PostLegacyAsync(PostLegacySaleReturnRequest request,
        CancellationToken ct = default)
    {
        await new PostLegacySaleReturnRequestValidator().ValidateAndThrowAsync(request, ct);
        var fingerprint = Fingerprint(request);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationGuard.LockAsync(db, request.OperationId, "LegacySaleReturn", ct);
        var actor = await CounterAuthorization.RequireAsync(db, user, ct);
        var replay = await LegacyQuery().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
        if (replay is not null)
        {
            if (replay.ActorId != actor || replay.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID belongs to another actor or sold-before-InventStock return payload.");
            return Map(replay);
        }

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Customers\" WHERE \"Id\" = {request.CustomerId.Value} FOR UPDATE", ct);
            customer = await db.Customers.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.CustomerId.Value && x.IsActive, ct)
                ?? throw new ConflictException("The selected customer is not active. Review the return before posting.");
        }

        var partIds = request.Lines.Select(x => x.PartId).Order().ToArray();
        foreach (var id in partIds)
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        var parts = await db.Parts.Where(x => partIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (parts.Count != partIds.Length)
            throw new ConflictException("Every sold-before-InventStock return line must reference a known SKU.");

        var total = request.Lines.Sum(x => x.RefundUsd);
        var paidOut = request.Refunds.Sum(x => x.ConfirmedUsdAmount);
        if (total != request.ConfirmedTotalUsd || total != paidOut + request.AccountReductionUsd ||
            (customer is null && request.AccountReductionUsd != 0))
            throw new ConflictException("Entered return value must equal explicit refunds plus account reduction; an account reduction requires a named customer.");

        var accountIds = request.Refunds.Select(x => x.CashAccountId).Distinct().ToArray();
        var accounts = await db.CashAccounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        var posted = LegacySaleReturn.Create(request.OperationId, actor, fingerprint, customer,
            request.OriginalSaleDate, request.OriginalReference, request.Reason, total,
            request.AccountReductionUsd);

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            var part = parts[line.PartId];
            var movement = StockMovement.StockIn(part.Id, line.Quantity, actor,
                "Sold before InventStock return", posted.Id.ToString());
            db.StockMovements.Add(movement);
            Guid? holdId = null;
            if (line.Condition == ReturnCondition.Quarantined)
            {
                var hold = StockHoldMovement.Create(part.Id, StockHoldAction.Hold, line.Quantity,
                    line.IssueReason!, line.PhysicalSeparationConfirmed, actor, Guid.NewGuid(), fingerprint);
                db.StockHoldMovements.Add(hold);
                holdId = hold.Id;
            }
            posted.Lines.Add(LegacySaleReturnLine.Create(posted.Id, part, i, line.Quantity,
                line.RefundUsd, line.Condition, movement.Id, holdId, line.IssueReason));
        }

        for (var i = 0; i < request.Refunds.Count; i++)
        {
            var refund = request.Refunds[i];
            if (!accounts.TryGetValue(refund.CashAccountId, out var account) || account.Currency != refund.Currency)
                throw new ConflictException("Each refund requires an active account matching its native currency.");
            var tender = LegacySaleRefundTender.Create(posted.Id, i, account, refund.NativeAmount,
                refund.ConfirmedUsdAmount);
            posted.Refunds.Add(tender);
            db.CashMovements.Add(CashMovement.LegacySaleRefund(tender, actor, posted.PostedAtUtc));
        }
        if (customer is not null && request.AccountReductionUsd > 0)
            db.CustomerAccountEntries.Add(CustomerAccountEntry.LegacyReturnReduction(customer.Id,
                posted.Id, request.OperationId, actor, request.AccountReductionUsd));

        db.LegacySaleReturns.Add(posted);
        await db.SaveChangesAsync(ct);
        var result = Map(posted);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<LegacySaleReturnDto> GetLegacyOperationAsync(Guid operationId,
        CancellationToken ct = default)
    {
        var actor = await CounterAuthorization.RequireAsync(db, user, ct);
        return Map(await LegacyQuery().SingleOrDefaultAsync(
            x => x.OperationId == operationId && x.ActorId == actor, ct)
            ?? throw new NotFoundException("Sold-before-InventStock return operation", operationId));
    }

    public async Task<SaleReturnDto> PostAsync(PostSaleReturnRequest request, CancellationToken ct = default)
    {
        await new PostSaleReturnRequestValidator().ValidateAndThrowAsync(request, ct);
        var fingerprint = Fingerprint(request);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationGuard.LockAsync(db, request.OperationId, "SaleReturn", ct);
        var actor = await CounterAuthorization.RequireAsync(db, user, ct);
        var replay = await Query().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
        if (replay is not null)
        {
            if (replay.ActorId != actor || replay.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID belongs to another actor or return payload.");
            return Map(replay);
        }

        var sale = await db.Sales.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.SaleId, ct)
            ?? throw new NotFoundException("Posted sale", request.SaleId);
        // Global order: operation -> customer -> sorted original sale lines -> sorted SKUs.
        if (sale.CustomerId.HasValue && db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Customers\" WHERE \"Id\" = {sale.CustomerId.Value} FOR UPDATE", ct);
        if (sale.CustomerId.HasValue && !await db.Customers.AnyAsync(x => x.Id == sale.CustomerId && x.IsActive, ct))
            throw new ConflictException("The sale customer is not active. Review the return before posting.");
        var lineIds = request.Lines.Select(x => x.SaleLineId).Order().ToArray();
        foreach (var id in lineIds)
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"SaleLines\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        var originals = await db.SaleLines.AsNoTracking().Where(x => lineIds.Contains(x.Id) && x.SaleId == sale.Id)
            .ToDictionaryAsync(x => x.Id, ct);
        if (originals.Count != lineIds.Length) throw new ConflictException("Every returned line must belong to the selected posted sale.");
        var prior = await db.SaleReturnLines.AsNoTracking().Where(x => lineIds.Contains(x.SaleLineId))
            .GroupBy(x => x.SaleLineId).Select(g => new { Id = g.Key, Quantity = g.Sum(x => x.Quantity), Refunded = g.Sum(x => x.RefundUsd) })
            .ToDictionaryAsync(x => x.Id, ct);
        var partIds = originals.Values.Select(x => x.PartId).Distinct().Order().ToArray();
        foreach (var id in partIds)
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        var parts = await db.Parts.Where(x => partIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        (SaleReturnLineRequest Request, SaleLine Original, decimal Refund)[] allocations;
        try
        {
            allocations = request.Lines.Select(line =>
            {
                var original = originals[line.SaleLineId];
                var before = prior.GetValueOrDefault(line.SaleLineId);
                var refund = ReturnAllocation.AdditionalRefund(original.LineTotalUsd, original.Quantity,
                    before?.Quantity ?? 0, before?.Refunded ?? 0, line.Quantity);
                return (Request: line, Original: original, Refund: refund);
            }).ToArray();
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ConflictException("The sale line has insufficient unreturned quantity. Refresh the sale before returning it.");
        }
        var total = allocations.Sum(x => x.Refund);
        var paidOut = request.Refunds.Sum(x => x.ConfirmedUsdAmount);
        if (total <= 0 || total != request.ConfirmedTotalUsd || total != paidOut + request.AccountReductionUsd ||
            (sale.CustomerId is null && request.AccountReductionUsd != 0))
            throw new ConflictException("Return total must equal explicit refunds plus account reduction; anonymous returns require a full refund.");

        var accountIds = request.Refunds.Select(x => x.CashAccountId).Distinct().ToArray();
        var accounts = await db.CashAccounts.AsNoTracking().Where(x => accountIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, ct);
        var posted = SaleReturn.Create(sale, request.OperationId, actor, fingerprint, request.Reason, total,
            request.AccountReductionUsd);
        for (var i = 0; i < allocations.Length; i++)
        {
            var (line, original, refund) = allocations[i];
            var movement = StockMovement.StockIn(original.PartId, line.Quantity, actor,
                "Linked customer return", posted.Id.ToString());
            db.StockMovements.Add(movement);
            Guid? holdId = null;
            if (line.Condition == ReturnCondition.Quarantined)
            {
                var hold = StockHoldMovement.Create(original.PartId, StockHoldAction.Hold, line.Quantity,
                    line.IssueReason!, line.PhysicalSeparationConfirmed, actor, Guid.NewGuid(), fingerprint);
                db.StockHoldMovements.Add(hold);
                holdId = hold.Id;
            }
            posted.Lines.Add(SaleReturnLine.Create(posted.Id, original, i, line.Quantity, refund,
                line.Condition, movement.Id, holdId, line.IssueReason));
        }
        for (var i = 0; i < request.Refunds.Count; i++)
        {
            var refund = request.Refunds[i];
            if (!accounts.TryGetValue(refund.CashAccountId, out var account) || account.Currency != refund.Currency)
                throw new ConflictException("Each refund requires an active account matching its native currency.");
            var tender = SaleRefundTender.Create(posted.Id, i, account, refund.NativeAmount, refund.ConfirmedUsdAmount);
            posted.Refunds.Add(tender);
            db.CashMovements.Add(CashMovement.SaleRefund(tender, actor, posted.PostedAtUtc));
        }
        if (sale.CustomerId.HasValue && request.AccountReductionUsd > 0)
            db.CustomerAccountEntries.Add(CustomerAccountEntry.ReturnReduction(sale.CustomerId.Value,
                posted.Id, request.OperationId, actor, request.AccountReductionUsd));
        db.SaleReturns.Add(posted);
        await db.SaveChangesAsync(ct);
        var result = Map(posted);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<SaleReturnDto> GetOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var actor = await CounterAuthorization.RequireAsync(db, user, ct);
        return Map(await Query().SingleOrDefaultAsync(x => x.OperationId == operationId && x.ActorId == actor, ct)
            ?? throw new NotFoundException("Sale return operation", operationId));
    }

    public async Task<ReturnableSaleDto> GetAvailableAsync(Guid saleId, CancellationToken ct = default)
    {
        await CounterAuthorization.RequireAsync(db, user, ct);
        var sale = await db.Sales.AsNoTracking().Include(x => x.Lines).Include(x => x.Tenders)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == saleId, ct)
            ?? throw new NotFoundException("Posted sale", saleId);
        var lineIds = sale.Lines.Select(x => x.Id).ToArray();
        var prior = await db.SaleReturnLines.AsNoTracking().Where(x => lineIds.Contains(x.SaleLineId))
            .GroupBy(x => x.SaleLineId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(x => x.Quantity), Refunded = g.Sum(x => x.RefundUsd) })
            .ToDictionaryAsync(x => x.Id, ct);
        var lines = sale.Lines.OrderBy(x => x.Position).Select(x =>
        {
            var returned = prior.GetValueOrDefault(x.Id);
            return new ReturnableSaleLineDto(x.Id, returned?.Quantity ?? 0,
                x.Quantity - (returned?.Quantity ?? 0), Normalize(returned?.Refunded ?? 0),
                Normalize(x.LineTotalUsd - (returned?.Refunded ?? 0)));
        }).ToArray();
        // The sale endpoint uses the same counter-only projection; neither response includes cost or supplier.
        var saleDto = new SaleDto(sale.Id, sale.ReceiptNumber, sale.OperationId, sale.ActorId, sale.PostedAtUtc,
            sale.CustomerId, sale.CustomerName, Normalize(sale.TotalUsd), Normalize(sale.PaymentsUsd),
            Normalize(sale.ChangeUsd), Normalize(sale.ExchangeCreditUsd), Normalize(sale.CustomerCreditUsedUsd), Normalize(sale.UnpaidDebtUsd),
            sale.Lines.OrderBy(x => x.Position).Select(x => new SaleLineDto(x.Id, x.PartId, x.Position,
                x.PartNumber, x.Name, x.Brand, x.Variant, x.StockUnit, x.Quantity,
                Normalize(x.UnitSellingPriceUsd), Normalize(x.LineTotalUsd), x.StockException,
                x.StockShortage, x.StockExceptionReason,
                x.SuggestedUnitPriceUsd is null ? null : Normalize(x.SuggestedUnitPriceUsd.Value),
                x.PriceOverridden)).ToArray(),
            sale.Tenders.OrderBy(x => x.Position).Select(x => new SaleTenderDto(x.Id, x.CashAccountId,
                x.AccountName, x.Currency, x.IsChange, Normalize(x.NativeAmount),
                Normalize(x.ConfirmedUsdAmount))).ToArray());
        return new ReturnableSaleDto(saleDto, lines);
    }

    private IQueryable<SaleReturn> Query() => db.SaleReturns.AsNoTracking().Include(x => x.Lines).Include(x => x.Refunds).AsSplitQuery();
    private IQueryable<LegacySaleReturn> LegacyQuery() => db.LegacySaleReturns.AsNoTracking()
        .Include(x => x.Lines).Include(x => x.Refunds).AsSplitQuery();
    private static SaleReturnDto Map(SaleReturn r) => new(r.Id, r.OperationId, r.ActorId, r.SaleId, r.CustomerId,
        r.PostedAtUtc, r.Reason, Normalize(r.TotalUsd), Normalize(r.AccountReductionUsd),
        r.Lines.OrderBy(x => x.Position).Select(x => new SaleReturnLineDto(x.Id, x.SaleLineId, x.PartId,
            x.Quantity, Normalize(x.RefundUsd), x.Condition, x.IssueReason)).ToArray(),
        r.Refunds.OrderBy(x => x.Position).Select(x => new SaleRefundTenderDto(x.Id, x.CashAccountId,
            x.AccountName, x.Currency, Normalize(x.NativeAmount), Normalize(x.ConfirmedUsdAmount))).ToArray());
    private static LegacySaleReturnDto Map(LegacySaleReturn r) => new(r.Id, r.OperationId, r.ActorId,
        r.CustomerId, r.CustomerName, r.OriginalSaleDate, r.OriginalReference, r.PostedAtUtc, r.Reason,
        Normalize(r.TotalUsd), Normalize(r.AccountReductionUsd),
        r.Lines.OrderBy(x => x.Position).Select(x => new LegacySaleReturnLineDto(x.Id, x.PartId,
            x.Position, x.PartNumber, x.Name, x.Brand, x.Variant, x.StockUnit, x.Quantity,
            Normalize(x.RefundUsd), x.Condition, x.IssueReason)).ToArray(),
        r.Refunds.OrderBy(x => x.Position).Select(x => new LegacySaleRefundTenderDto(x.Id,
            x.CashAccountId, x.AccountName, x.Currency, Normalize(x.NativeAmount),
            Normalize(x.ConfirmedUsdAmount))).ToArray());
    private static decimal Normalize(decimal x) => decimal.Parse(Number(x), CultureInfo.InvariantCulture);
    private static string Number(decimal x) => x.ToString("G29", CultureInfo.InvariantCulture);
    private static string Fingerprint(PostSaleReturnRequest r)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            r.SaleId, Reason = r.Reason.Trim(), Total = Number(r.ConfirmedTotalUsd),
            Account = Number(r.AccountReductionUsd),
            Lines = r.Lines.Select(x => new { x.SaleLineId, x.Quantity, x.Condition,
                x.PhysicalSeparationConfirmed, Reason = x.IssueReason?.Trim() }),
            Refunds = r.Refunds.Select(x => new { x.CashAccountId, x.Currency,
                Native = Number(x.NativeAmount), Usd = Number(x.ConfirmedUsdAmount) })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    private static string Fingerprint(PostLegacySaleReturnRequest r)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            r.CustomerId, r.OriginalSaleDate, Reference = r.OriginalReference?.Trim(),
            Reason = r.Reason.Trim(), Total = Number(r.ConfirmedTotalUsd),
            Account = Number(r.AccountReductionUsd),
            Lines = r.Lines.Select(x => new { x.PartId, x.Quantity, Refund = Number(x.RefundUsd),
                x.Condition, x.PhysicalSeparationConfirmed, Reason = x.IssueReason?.Trim() }),
            Refunds = r.Refunds.Select(x => new { x.CashAccountId, x.Currency,
                Native = Number(x.NativeAmount), Usd = Number(x.ConfirmedUsdAmount) })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
