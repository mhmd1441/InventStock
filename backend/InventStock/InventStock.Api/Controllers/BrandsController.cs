using InventStock.Application.Common;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.DTOs;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/brands")]
[Authorize]
public sealed class BrandsController(IBrandService brands) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await brands.ListAsync(includeInactive && User.IsInRole(RoleNames.Admin), cancellationToken));

    [HttpGet("candidates")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> Candidates([FromQuery] string name, CancellationToken cancellationToken) =>
        Ok(await brands.FindCandidatesAsync(name, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<BrandDto>> Create(SaveBrandRequest request, CancellationToken cancellationToken)
    {
        var created = await brands.CreateAsync(request, cancellationToken);
        return Created($"/api/brands/{created.Id}", created);
    }

    [HttpPost("{id:guid}/aliases")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<BrandDto>> AddAlias(Guid id, SaveBrandAliasRequest request, CancellationToken cancellationToken) =>
        Ok(await brands.AddAliasAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/active")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> SetActive(Guid id, SetActiveRequest request, CancellationToken cancellationToken)
    {
        await brands.SetActiveAsync(id, request.IsActive, cancellationToken);
        return NoContent();
    }
}
