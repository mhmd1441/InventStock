using InventStock.Application.Common;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Authorize]
public sealed class StockController : ControllerBase
{
    private readonly IStockMovementService _movements;

    public StockController(IStockMovementService movements)
    {
        _movements = movements;
    }

    [HttpGet("api/stock-operations/{operationId:guid}")]
    [ProducesResponseType(typeof(StockOperationStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockOperationStatusDto>> OperationStatus(Guid operationId, CancellationToken cancellationToken) =>
        Ok(await _movements.GetOperationStatusAsync(operationId, cancellationToken));

    [HttpPost("api/parts/{partId:guid}/stock/in")]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementDto>> StockIn(
        Guid partId,
        StockInRequest request,
        CancellationToken cancellationToken)
    {
        var movement = await _movements.StockInAsync(partId, request, cancellationToken);
        return Created($"/api/parts/{partId}/movements", movement);
    }

    [HttpPost("api/parts/{partId:guid}/stock/out")]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDto>> StockOut(
        Guid partId,
        StockOutRequest request,
        CancellationToken cancellationToken)
    {
        var movement = await _movements.StockOutAsync(partId, request, cancellationToken);
        return Created($"/api/parts/{partId}/movements", movement);
    }

    /// <summary>
    /// Adjustments are restricted to administrators: they are the only movement that can write stock
    /// off without a corresponding issue, so they are the obvious target for concealing shrinkage.
    /// </summary>
    [HttpPost("api/parts/{partId:guid}/stock/adjust")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDto>> Adjust(
        Guid partId,
        StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var movement = await _movements.AdjustAsync(partId, request, cancellationToken);
        return Created($"/api/parts/{partId}/movements", movement);
    }

    [HttpPost("api/parts/{partId:guid}/stock/count")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDto>> Count(
        Guid partId,
        StockCountRequest request,
        CancellationToken cancellationToken)
    {
        var movement = await _movements.CountAsync(partId, request, cancellationToken);
        return Created($"/api/parts/{partId}/movements", movement);
    }

    [HttpGet("api/stock-movements")]
    [ProducesResponseType(typeof(PagedResult<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> Search(
        [FromQuery] StockMovementQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _movements.SearchAsync(query, cancellationToken));
}
