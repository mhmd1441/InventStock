using InventStock.Application.Common;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Staff)]
[Route("api/sale-returns")]
public sealed class SaleReturnsController(ISaleReturnService returns) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SaleReturnDto>> Post(PostSaleReturnRequest request, CancellationToken ct)
    {
        var result = await returns.PostAsync(request, ct);
        return Created($"/api/sale-returns/operations/{result.OperationId}", result);
    }

    [HttpGet("operations/{operationId:guid}")]
    public async Task<ActionResult<SaleReturnDto>> Operation(Guid operationId, CancellationToken ct) =>
        Ok(await returns.GetOperationAsync(operationId, ct));

    [HttpGet("sales/{saleId:guid}/availability")]
    public async Task<ActionResult<ReturnableSaleDto>> Availability(Guid saleId, CancellationToken ct) =>
        Ok(await returns.GetAvailableAsync(saleId, ct));

    [HttpPost("legacy")]
    public async Task<ActionResult<LegacySaleReturnDto>> PostLegacy(PostLegacySaleReturnRequest request,
        CancellationToken ct)
    {
        var result = await returns.PostLegacyAsync(request, ct);
        return Created($"/api/sale-returns/legacy/operations/{result.OperationId}", result);
    }

    [HttpGet("legacy/operations/{operationId:guid}")]
    public async Task<ActionResult<LegacySaleReturnDto>> LegacyOperation(Guid operationId,
        CancellationToken ct) => Ok(await returns.GetLegacyOperationAsync(operationId, ct));
}
