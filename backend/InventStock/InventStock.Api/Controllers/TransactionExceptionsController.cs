using InventStock.Application.Common;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Exceptions;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/transaction-exceptions")]
public sealed class TransactionExceptionsController(ITransactionExceptionService exceptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TransactionExceptionDto>>> List(
        [FromQuery] TransactionExceptionQuery query, CancellationToken ct) =>
        Ok(await exceptions.ListAsync(query, ct));
}
