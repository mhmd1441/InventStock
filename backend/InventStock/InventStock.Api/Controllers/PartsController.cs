using InventStock.Application.Common;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.DTOs;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/parts")]
[Authorize]
public sealed class PartsController : ControllerBase
{
    private readonly IPartService _parts;
    private readonly IStockMovementService _movements;

    public PartsController(IPartService parts, IStockMovementService movements)
    {
        _parts = parts;
        _movements = movements;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CounterPartDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CounterPartDto>>> Search(
        [FromQuery] PartQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _parts.SearchCounterAsync(query, cancellationToken));

    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<CounterPartDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CounterPartDto>>> LowStock(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _parts.GetCounterLowStockAsync(limit, cancellationToken));

    [HttpGet("by-number/{partNumber}")]
    [ProducesResponseType(typeof(CounterPartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CounterPartDto>> GetByNumber(string partNumber, CancellationToken cancellationToken) =>
        Ok(await _parts.GetCounterByNumberAsync(partNumber, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CounterPartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CounterPartDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _parts.GetCounterAsync(id, cancellationToken));

    [HttpGet("{id:guid}/movements")]
    [ProducesResponseType(typeof(PagedResult<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> Movements(
        Guid id,
        [FromQuery] StockMovementQuery query,
        CancellationToken cancellationToken)
    {
        query.PartId = id;
        return Ok(await _movements.SearchAsync(query, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(PartDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PartDto>> Create(SavePartRequest request, CancellationToken cancellationToken)
    {
        var created = await _parts.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(PartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartDto>> Update(Guid id, SavePartRequest request, CancellationToken cancellationToken) =>
        Ok(await _parts.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/active")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActive(Guid id, SetActiveRequest request, CancellationToken cancellationToken)
    {
        await _parts.SetActiveAsync(id, request.IsActive, cancellationToken);
        return NoContent();
    }
}
