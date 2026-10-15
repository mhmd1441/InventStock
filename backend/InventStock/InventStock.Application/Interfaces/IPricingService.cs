using InventStock.Application.DTOs.Sales;

namespace InventStock.Application.Interfaces;

public interface IPricingService
{
    Task<IReadOnlyList<SellingPriceSuggestionDto>> SuggestionsAsync(IReadOnlyList<Guid> partIds, CancellationToken ct = default);
    Task<PricingPolicyDto> GetPolicyAsync(CancellationToken ct = default);
    Task<PricingPolicyDto> UpdatePolicyAsync(UpdatePricingPolicyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AdminPriceCalculationDto>> CalculationsAsync(IReadOnlyList<Guid> partIds, CancellationToken ct = default);
    Task<CutHoseSellingRateDto> GetCutHoseRateAsync(Guid partId, CancellationToken ct = default);
    Task<CutHoseSellingRateDto> SetCutHoseRateAsync(Guid partId, SetCutHoseSellingRateRequest request, CancellationToken ct = default);
}
