using InventStock.Application.Common;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Staff)]
[Route("api/sale-exchanges")]
public sealed class SaleExchangesController(ISaleExchangeService exchanges) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SaleExchangeDto>> Post(PostSaleExchangeRequest request, CancellationToken ct)
    {
        var result = await exchanges.PostAsync(request, ct);
        return Created($"/api/sale-exchanges/operations/{result.OperationId}", result);
    }

    [HttpGet("operations/{operationId:guid}")]
    public async Task<ActionResult<SaleExchangeDto>> Operation(Guid operationId, CancellationToken ct) =>
        Ok(await exchanges.GetOperationAsync(operationId, ct));
}
