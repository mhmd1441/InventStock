using InventStock.Application.Common;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController, Authorize]
[Route("api/parts/{partId:guid}/stock")]
public sealed class StockHoldsController(IStockHoldService holds) : ControllerBase
{
    [HttpGet("/api/stock-hold-operations/{operationId:guid}")]
    public async Task<ActionResult<StockHoldMovementDto>> Operation(Guid operationId, CancellationToken cancellationToken) =>
        Ok(await holds.GetOperationAsync(operationId, cancellationToken));

    [HttpPost("hold")]
    public async Task<ActionResult<StockHoldMovementDto>> Hold(Guid partId, StockHoldRequest request, CancellationToken cancellationToken) =>
        Ok(await holds.HoldAsync(partId, request, cancellationToken));

    [HttpPost("release")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<StockHoldMovementDto>> Release(Guid partId, StockReleaseRequest request, CancellationToken cancellationToken) =>
        Ok(await holds.ReleaseAsync(partId, request, cancellationToken));
}
