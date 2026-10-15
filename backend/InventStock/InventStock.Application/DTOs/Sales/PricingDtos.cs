using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Sales;

// The counter response deliberately has no cost, supplier, or exchange-rate fields.
public sealed record SellingPriceSuggestionDto(Guid PartId, decimal? SuggestedUsd);

public sealed record PricingPolicyDto(decimal MarkupPercent, decimal TvaPercent, decimal? EurUsdRate,
    decimal? LbpPerUsdRate, int Revision, Guid? UpdatedById, DateTime? UpdatedAtUtc);

public sealed record UpdatePricingPolicyRequest(decimal MarkupPercent, decimal TvaPercent,
    decimal? EurUsdRate, decimal? LbpPerUsdRate, int ExpectedRevision);

public sealed record AdminPriceCalculationDto(Guid PartId, decimal? SuggestedUsd, decimal? LatestActualPackageCost,
    MoneyCurrency? CostCurrency, TaxTreatment? TaxTreatment, int? BaseUnitsPerPackage,
    DateTime? PurchasedAtUtc, decimal? BaseUnitCostUsd, string? UnavailableReason,
    decimal? DirectSellingRateUsdPerMeter = null);

public sealed record CutHoseSellingRateDto(Guid PartId, Guid? CurrentRateId, int Revision,
    decimal? UsdPerMeter, DateTime? UpdatedAtUtc, Guid? UpdatedById);
public sealed record SetCutHoseSellingRateRequest(Guid? ExpectedCurrentRateId, decimal? UsdPerMeter);
