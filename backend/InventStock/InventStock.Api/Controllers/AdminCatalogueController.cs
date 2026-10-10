using InventStock.Application.Common;
using InventStock.Application.Interfaces;
using InventStock.Application.DTOs.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/admin/parts")]
[Authorize(Roles = RoleNames.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCatalogueController(IPartService parts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] PartQuery query, CancellationToken cancellationToken) =>
        Ok(await parts.SearchAsync(query, cancellationToken));

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock([FromQuery] int limit = 20, CancellationToken cancellationToken = default) =>
        Ok(await parts.GetLowStockAsync(limit, cancellationToken));

    [HttpGet("by-number/{number}")]
    public async Task<IActionResult> ByNumber(string number, CancellationToken cancellationToken) =>
        Ok(await parts.GetByPartNumberAsync(number, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await parts.GetAsync(id, cancellationToken));
}
