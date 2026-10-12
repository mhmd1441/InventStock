using InventStock.Application.Common;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/admin/parts/{partId:guid}/supplier-costs")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class SupplierCostsController(ISupplierCostService costs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierCostObservationDto>>> List(Guid partId, CancellationToken cancellationToken) =>
        Ok(await costs.ListAsync(partId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<SupplierCostObservationDto>> Add(Guid partId, AddSupplierCostObservationRequest request, CancellationToken cancellationToken)
    {
        var created = await costs.AddAsync(partId, request, cancellationToken);
        return Created($"/api/admin/parts/{partId}/supplier-costs", created);
    }
}
