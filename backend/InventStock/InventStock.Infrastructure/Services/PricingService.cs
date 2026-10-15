using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.Interfaces;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Services;

public sealed class PricingService(AppDbContext db, ICurrentUserService currentUser) : IPricingService
{
    public async Task<CutHoseSellingRateDto> GetCutHoseRateAsync(Guid partId, CancellationToken ct = default)
    {
        await CounterAuthorization.RequireAsync(db, currentUser, ct, adminOnly: true);
        await RequireCutHoseAsync(partId, ct);
        var latest = await db.CutHoseSellingRates.AsNoTracking().Where(x => x.PartId == partId)
            .OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        return MapRate(partId, latest);
    }

    public async Task<CutHoseSellingRateDto> SetCutHoseRateAsync(Guid partId,
        SetCutHoseSellingRateRequest request, CancellationToken ct = default)
    {
        var actor = await CounterAuthorization.RequireAsync(db, currentUser, ct, adminOnly: true);
        if (request.UsdPerMeter is <= 0 or > 999999.99m ||
            (request.UsdPerMeter.HasValue && decimal.Round(request.UsdPerMeter.Value, 2) != request.UsdPerMeter.Value))
            throw new ConflictException("Enter a positive USD/metre selling rate with at most two decimals, or clear it.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {partId} FOR UPDATE", ct);
        await RequireCutHoseAsync(partId, ct);
        var latest = await db.CutHoseSellingRates.AsNoTracking().Where(x => x.PartId == partId)
            .OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        if (latest?.Id != request.ExpectedCurrentRateId)
            throw new ConflictException("The cut-hose rate changed. Reload it before saving.");
        var next = CutHoseSellingRate.Create(partId, (latest?.Revision ?? 0) + 1, request.UsdPerMeter, actor);
        db.CutHoseSellingRates.Add(next);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MapRate(partId, next);
    }

    private async Task RequireCutHoseAsync(Guid partId, CancellationToken ct)
    {
        if (!await db.Parts.AnyAsync(x => x.Id == partId && x.IsActive && x.StockUnit == StockUnit.Centimeter, ct))
            throw new NotFoundException("Active cut-hose part", partId);
    }

    private static CutHoseSellingRateDto MapRate(Guid partId, CutHoseSellingRate? rate) =>
        new(partId, rate?.Id, rate?.Revision ?? 0, rate?.UsdPerMeter, rate?.CreatedAtUtc, rate?.ActorId);

    public async Task<IReadOnlyList<SellingPriceSuggestionDto>> SuggestionsAsync(IReadOnlyList<Guid> partIds, CancellationToken ct = default)
    {
        await CounterAuthorization.RequireAsync(db, currentUser, ct);
        var calculations = await CalculateAsync(partIds, ct);
        return calculations.Select(x => new SellingPriceSuggestionDto(x.PartId, x.SuggestedUsd)).ToArray();
    }

    public async Task<IReadOnlyList<AdminPriceCalculationDto>> CalculationsAsync(IReadOnlyList<Guid> partIds, CancellationToken ct = default)
    {
        await CounterAuthorization.RequireAsync(db, currentUser, ct, adminOnly: true);
        return await CalculateAsync(partIds, ct);
    }

    public async Task<PricingPolicyDto> GetPolicyAsync(CancellationToken ct = default)
    {
        await CounterAuthorization.RequireAsync(db, currentUser, ct, adminOnly: true);
        return Map(await db.PricingPolicies.AsNoTracking().SingleAsync(x => x.Id == 1, ct));
    }

    public async Task<PricingPolicyDto> UpdatePolicyAsync(UpdatePricingPolicyRequest request, CancellationToken ct = default)
    {
        var actor = await CounterAuthorization.RequireAsync(db, currentUser, ct, adminOnly: true);
        if (request.MarkupPercent is < 0 or > 1000 || request.TvaPercent is < 0 or > 100 ||
            request.EurUsdRate is <= 0 or > 1000 || request.LbpPerUsdRate is <= 0 or > 1000000000)
            throw new ConflictException("Pricing percentages or exchange rates are outside the allowed range.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"PricingPolicies\" WHERE \"Id\" = 1 FOR UPDATE", ct);
        var policy = await db.PricingPolicies.SingleAsync(x => x.Id == 1, ct);
        if (policy.Revision != request.ExpectedRevision)
            throw new ConflictException("Pricing settings changed. Reload them before saving.");
        policy.Update(request.MarkupPercent, request.TvaPercent, request.EurUsdRate, request.LbpPerUsdRate, actor);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(policy);
    }

    private async Task<IReadOnlyList<AdminPriceCalculationDto>> CalculateAsync(IReadOnlyList<Guid> partIds, CancellationToken ct)
    {
        var ids = partIds.Distinct().ToArray();
        if (ids.Length > 50 || ids.Contains(Guid.Empty))
            throw new ConflictException("Request between one and fifty valid part IDs.");
        if (ids.Length == 0) return [];
        var policy = await db.PricingPolicies.AsNoTracking().SingleAsync(x => x.Id == 1, ct);
        var active = await db.Parts.AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsActive)
            .Select(x => new { x.Id, x.StockUnit }).ToArrayAsync(ct);
        var pieceIds = active.Where(x => x.StockUnit == StockUnit.Piece).Select(x => x.Id).ToArray();
        var cutHoseIds = active.Where(x => x.StockUnit == StockUnit.Centimeter).Select(x => x.Id).ToArray();
        var costs = await db.SupplierCostObservations.AsNoTracking()
            .Where(x => pieceIds.Contains(x.PartId) && x.Source == CostSource.ActualPurchase && x.ObservedAtUtc <= DateTime.UtcNow)
            .GroupBy(x => x.PartId)
            .Select(g => g.OrderByDescending(x => x.ObservedAtUtc).ThenByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id).First())
            .ToDictionaryAsync(x => x.PartId, ct);
        var rates = await db.CutHoseSellingRates.AsNoTracking().Where(x => cutHoseIds.Contains(x.PartId))
            .GroupBy(x => x.PartId).Select(g => g.OrderByDescending(x => x.Revision).First())
            .ToDictionaryAsync(x => x.PartId, ct);
        return active.Select(part => part.StockUnit == StockUnit.Centimeter
            ? CalculateCutHose(part.Id, rates.GetValueOrDefault(part.Id))
            : Calculate(part.Id, part.StockUnit, costs.GetValueOrDefault(part.Id), policy)).ToArray();
    }

    internal static AdminPriceCalculationDto CalculateCutHose(Guid partId, CutHoseSellingRate? rate) =>
        new(partId, rate?.UsdPerMeter / 100m, null, null, null, null, null, null,
            rate?.UsdPerMeter is null ? "No owner-entered cut-hose selling rate" : null,
            rate?.UsdPerMeter);

    internal static AdminPriceCalculationDto Calculate(Guid partId, StockUnit stockUnit, SupplierCostObservation? cost, PricingPolicy policy)
    {
        if (cost is null)
            return new(partId, null, null, null, null, null, null, null, "No actual purchase cost recorded");
        // A whole-dollar ceiling is meaningful per piece, not per centimetre. Never turn $13/m into $1/cm.
        string? unavailable = stockUnit == StockUnit.Centimeter ? "Cut hose needs a per-metre pricing rule" :
            cost.UnitCost <= 0 ? "No positive actual purchase cost recorded" :
            cost.TaxTreatment == TaxTreatment.Unknown ? "Purchase TVA treatment is unknown" : null;
        var baseCost = cost.UnitCost / cost.PackQuantity;
        decimal? usd = cost.Currency switch
        {
            MoneyCurrency.USD => baseCost,
            MoneyCurrency.EUR when policy.EurUsdRate.HasValue => baseCost * policy.EurUsdRate.Value,
            MoneyCurrency.LBP when policy.LbpPerUsdRate.HasValue => baseCost / policy.LbpPerUsdRate.Value,
            _ => null
        };
        if (usd is null) unavailable ??= $"No owner-entered {cost.Currency} conversion rate";
        // Rounding occurs once, after currency conversion, TVA and markup. Never round the cost basis first.
        decimal? suggestion = unavailable is null && usd.HasValue
            ? decimal.Ceiling(usd.Value * (cost.TaxTreatment == TaxTreatment.Excluded ? 1 + policy.TvaPercent / 100 : 1)
                * (1 + policy.MarkupPercent / 100)) : null;
        return new(partId, suggestion, cost.UnitCost, cost.Currency, cost.TaxTreatment,
            cost.PackQuantity, cost.ObservedAtUtc, usd, unavailable);
    }

    private static PricingPolicyDto Map(PricingPolicy policy) => new(policy.MarkupPercent, policy.TvaPercent,
        policy.EurUsdRate, policy.LbpPerUsdRate, policy.Revision, policy.UpdatedById, policy.UpdatedAtUtc);
}
