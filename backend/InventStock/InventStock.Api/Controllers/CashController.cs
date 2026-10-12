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
