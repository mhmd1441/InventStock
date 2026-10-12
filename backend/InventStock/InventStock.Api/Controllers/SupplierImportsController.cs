using InventStock.Application.Common;
using InventStock.Application.DTOs.Imports;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/admin/supplier-imports")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class SupplierImportsController(ISupplierImportService imports) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SupplierImportBatchDto>> Stage(StageSupplierImportRequest request, CancellationToken cancellationToken)
    {
        var batch = await imports.StageAsync(request, cancellationToken);
        return Created($"/api/admin/supplier-imports/{batch.Id}", batch);
    }

    [HttpGet("{batchId:guid}")]
    public async Task<ActionResult<SupplierImportBatchDto>> Get(Guid batchId, CancellationToken cancellationToken) => Ok(await imports.GetAsync(batchId, cancellationToken));

    [HttpPut("{batchId:guid}/rows/{rowId:guid}/review")]
    public async Task<ActionResult<SupplierImportBatchDto>> Review(Guid batchId, Guid rowId,
        ReviewSupplierImportRowRequest request, CancellationToken cancellationToken) =>
        Ok(await imports.ReviewRowAsync(batchId, rowId, request, cancellationToken));

    [HttpPost("{batchId:guid}/commit")]
    public async Task<ActionResult<CommitSupplierImportResult>> Commit(Guid batchId, CancellationToken cancellationToken) =>
        Ok(await imports.CommitAsync(batchId, cancellationToken));
}
