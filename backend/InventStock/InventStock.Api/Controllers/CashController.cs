using InventStock.Application.Common;
using InventStock.Application.DTOs.Cash;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/admin/cash")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class CashController(ICashService cash) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<ActionResult<IReadOnlyList<CashAccountDto>>> Accounts(CancellationToken cancellationToken) =>
        Ok(await cash.ListAccountsAsync(cancellationToken));

    [HttpPost("accounts/{accountId:guid}/opening-balance")]
    public async Task<ActionResult<CashOpeningBalanceDto>> Opening(Guid accountId, PostCashOpeningBalanceRequest request,
        CancellationToken cancellationToken) =>
        Ok(await cash.SetOpeningBalanceAsync(accountId, request, cancellationToken));

    [HttpGet("reconciliation")]
    public async Task<ActionResult<CashReconciliationDayDto>> Reconciliation([FromQuery] DateOnly businessDate,
        CancellationToken cancellationToken) => Ok(await cash.GetReconciliationDayAsync(businessDate, cancellationToken));

    [HttpPost("accounts/{accountId:guid}/reconciliation")]
    public async Task<ActionResult<CashReconciliationDto>> Reconcile(Guid accountId, PostCashReconciliationRequest request,
        CancellationToken cancellationToken) => Ok(await cash.ReconcileAsync(accountId, request, cancellationToken));

    [HttpPost("reconciliations/{reconciliationId:guid}/correction")]
    public async Task<ActionResult<CashReconciliationCorrectionDto>> CorrectReconciliation(Guid reconciliationId,
        PostCashReconciliationCorrectionRequest request, CancellationToken cancellationToken) =>
        Ok(await cash.CorrectReconciliationAsync(reconciliationId, request, cancellationToken));

    [HttpGet("supplier-payments")]
    public async Task<ActionResult<IReadOnlyList<SupplierPaymentDto>>> SupplierPayments([FromQuery] int year, [FromQuery] int month,
        CancellationToken cancellationToken) => Ok(await cash.ListSupplierPaymentsAsync(year, month, cancellationToken));

    [HttpPost("supplier-payments")]
    public async Task<ActionResult<SupplierPaymentDto>> SupplierPayment(PostSupplierPaymentRequest request,
        CancellationToken cancellationToken) => Ok(await cash.PostSupplierPaymentAsync(request, cancellationToken));

    [HttpPost("supplier-payments/{paymentId:guid}/reverse")]
    public async Task<ActionResult<SupplierPaymentDto>> ReverseSupplierPayment(Guid paymentId, ReverseSupplierPaymentRequest request,
        CancellationToken cancellationToken) => Ok(await cash.ReverseSupplierPaymentAsync(paymentId, request, cancellationToken));

    [HttpGet("transfers")]
    public async Task<ActionResult<IReadOnlyList<CashTransferDto>>> Transfers([FromQuery] int year, [FromQuery] int month,
        CancellationToken cancellationToken) => Ok(await cash.ListTransfersAsync(year, month, cancellationToken));

    [HttpPost("transfers")]
    public async Task<ActionResult<CashTransferDto>> Transfer(PostCashTransferRequest request,
        CancellationToken cancellationToken) => Ok(await cash.PostTransferAsync(request, cancellationToken));

    [HttpPost("transfers/{transferId:guid}/reverse")]
    public async Task<ActionResult<CashTransferDto>> ReverseTransfer(Guid transferId, ReverseCashTransferRequest request,
        CancellationToken cancellationToken) => Ok(await cash.ReverseTransferAsync(transferId, request, cancellationToken));

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<ExpenseCategoryDto>>> Categories([FromQuery] bool includeInactive,
        CancellationToken cancellationToken) => Ok(await cash.ListCategoriesAsync(includeInactive, cancellationToken));

    [HttpPost("categories")]
    public async Task<ActionResult<ExpenseCategoryDto>> CreateCategory(SaveExpenseCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var created = await cash.CreateCategoryAsync(request, cancellationToken);
        return Created($"/api/admin/cash/categories/{created.Id}", created);
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult<ExpenseCategoryDto>> RenameCategory(Guid id, SaveExpenseCategoryRequest request,
        CancellationToken cancellationToken) => Ok(await cash.RenameCategoryAsync(id, request, cancellationToken));

    [HttpPatch("categories/{id:guid}/active")]
    public async Task<IActionResult> SetCategoryActive(Guid id, SetExpenseCategoryActiveRequest request,
        CancellationToken cancellationToken)
    {
        await cash.SetCategoryActiveAsync(id, request.IsActive, cancellationToken);
        return NoContent();
    }

    [HttpGet("outgoings")]
    public async Task<ActionResult<OutgoingPaymentListDto>> Outgoings([FromQuery] int year, [FromQuery] int month,
        CancellationToken cancellationToken) => Ok(await cash.ListOutgoingsAsync(year, month, cancellationToken));

    [HttpPost("outgoings")]
    public async Task<ActionResult<OutgoingPaymentDto>> PostOutgoing(PostOutgoingPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var posted = await cash.PostOutgoingAsync(request, cancellationToken);
        return Created($"/api/admin/cash/outgoings/{posted.Id}", posted);
    }

    [HttpPost("outgoings/{paymentId:guid}/reverse")]
    public async Task<ActionResult<OutgoingPaymentDto>> Reverse(Guid paymentId, ReverseOutgoingPaymentRequest request,
        CancellationToken cancellationToken) => Ok(await cash.ReverseOutgoingAsync(paymentId, request, cancellationToken));
}
