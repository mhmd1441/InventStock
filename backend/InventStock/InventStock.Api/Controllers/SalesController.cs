using InventStock.Application.Common;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Staff)]
[Route("api/sales")]
public sealed class SalesController(ISaleService sales) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SaleDto>> Post(PostSaleRequest request, CancellationToken cancellationToken)
    {
        var sale = await sales.PostAsync(request, cancellationToken);
        return Created($"/api/sales/operations/{sale.OperationId}", sale);
    }

    [HttpPost("paper-recovery")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<SaleDto>> PostPaperRecovery(PostPaperRecoverySaleRequest request, CancellationToken cancellationToken)
    {
        var sale = await sales.PostPaperRecoveryAsync(request, cancellationToken);
        return Created($"/api/sales/operations/{sale.OperationId}", sale);
    }

    [HttpGet("operations/{operationId:guid}")]
    public async Task<ActionResult<SaleOperationDto>> Operation(Guid operationId, CancellationToken cancellationToken) =>
        Ok(await sales.GetOperationAsync(operationId, cancellationToken));

    [HttpGet("payment-accounts")]
    public async Task<ActionResult<IReadOnlyList<SalePaymentAccountDto>>> PaymentAccounts(CancellationToken cancellationToken) =>
        Ok(await sales.GetPaymentAccountsAsync(cancellationToken));

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<SaleSummaryDto>>> Recent(CancellationToken cancellationToken) =>
        Ok(await sales.GetRecentAsync(cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<SaleLookupDto>>> Search([FromQuery] SaleSearchQuery query,
        CancellationToken cancellationToken) => Ok(await sales.SearchAsync(query, cancellationToken));

    [HttpGet("{saleId:guid}")]
    public async Task<ActionResult<SaleDto>> Get(Guid saleId, CancellationToken cancellationToken) =>
        Ok(await sales.GetAsync(saleId, cancellationToken));

    [HttpPost("tenders/{tenderId:guid}/account-corrections")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<SaleTenderAccountCorrectionDto>> CorrectTenderAccount(Guid tenderId,
        CorrectSaleTenderAccountRequest request, CancellationToken cancellationToken) =>
        Created($"/api/sales/tender-account-corrections/operations/{request.OperationId}",
            await sales.CorrectTenderAccountAsync(tenderId, request, cancellationToken));

    [HttpGet("tender-account-corrections/operations/{operationId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<SaleTenderAccountCorrectionDto>> TenderAccountCorrectionOperation(Guid operationId,
        CancellationToken cancellationToken) => Ok(await sales.GetTenderAccountCorrectionOperationAsync(operationId, cancellationToken));

    [HttpPost("tender-account-corrections/{correctionId:guid}/reversal")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<SaleTenderAccountCorrectionReversalDto>> ReverseTenderAccountCorrection(Guid correctionId,
        ReverseSaleTenderAccountCorrectionRequest request, CancellationToken cancellationToken) =>
        Ok(await sales.ReverseTenderAccountCorrectionAsync(correctionId, request, cancellationToken));
}
