using InventStock.Application.Common;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Staff)]
public sealed class PricingController(IPricingService pricing) : ControllerBase
{
    [HttpGet("api/pricing/suggestions")]
    public async Task<ActionResult<IReadOnlyList<SellingPriceSuggestionDto>>> Suggestions([FromQuery] Guid[] partIds,
        CancellationToken ct) => Ok(await pricing.SuggestionsAsync(partIds, ct));

    [HttpGet("api/admin/pricing/policy")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<PricingPolicyDto>> Policy(CancellationToken ct) => Ok(await pricing.GetPolicyAsync(ct));

    [HttpPut("api/admin/pricing/policy")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<PricingPolicyDto>> Update(UpdatePricingPolicyRequest request, CancellationToken ct) =>
        Ok(await pricing.UpdatePolicyAsync(request, ct));

    [HttpGet("api/admin/pricing/calculations")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<IReadOnlyList<AdminPriceCalculationDto>>> Calculations([FromQuery] Guid[] partIds,
        CancellationToken ct) => Ok(await pricing.CalculationsAsync(partIds, ct));

    [HttpGet("api/admin/pricing/cut-hose/{partId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CutHoseSellingRateDto>> CutHoseRate(Guid partId, CancellationToken ct) =>
        Ok(await pricing.GetCutHoseRateAsync(partId, ct));

    [HttpPut("api/admin/pricing/cut-hose/{partId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CutHoseSellingRateDto>> SetCutHoseRate(Guid partId,
        SetCutHoseSellingRateRequest request, CancellationToken ct) =>
        Ok(await pricing.SetCutHoseRateAsync(partId, request, ct));
}
