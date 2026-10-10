using InventStock.Application.Common;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

// These are public catalogue identifiers only. Supplier-specific codes belong in private offers.
[ApiController]
[Authorize]
[Route("api/parts/{partId:guid}/references")]
public sealed class ReferencesController(IReferenceService references) : ControllerBase
{
    [HttpGet("all")]
    [Authorize(Roles = RoleNames.Admin)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ListAll(Guid partId, CancellationToken cancellationToken) =>
        Ok(await references.ListAllAsync(partId, cancellationToken));

    [HttpPatch("{referenceId:guid}/state")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> SetState(Guid partId, Guid referenceId, SetReferenceStateRequest request, CancellationToken cancellationToken)
    {
        await references.SetStateAsync(partId, referenceId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{referenceId:guid}/history")]
    [Authorize(Roles = RoleNames.Admin)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> History(Guid partId, Guid referenceId, CancellationToken cancellationToken) =>
        Ok(await references.HistoryAsync(partId, referenceId, cancellationToken));
    [HttpGet]
    public async Task<IActionResult> List(Guid partId, CancellationToken cancellationToken) =>
        Ok(await references.ListAsync(partId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Add(Guid partId, AddReferenceRequest request, CancellationToken cancellationToken) =>
        Ok(await references.AddAsync(partId, request, cancellationToken));
}
