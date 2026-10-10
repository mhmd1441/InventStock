using InventStock.Application.Common;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize(Roles = RoleNames.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers)
    {
        _suppliers = suppliers;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> List(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken) =>
        Ok(await _suppliers.ListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _suppliers.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierDto>> Create(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var created = await _suppliers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> Update(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken) =>
        Ok(await _suppliers.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/active")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        await _suppliers.SetActiveAsync(id, isActive, cancellationToken);
        return NoContent();
    }
}
