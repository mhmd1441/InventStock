using InventStock.Application.Common;
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

    [HttpGet("operations/{operationId:guid}")]
    public async Task<ActionResult<SaleOperationDto>> Operation(Guid operationId, CancellationToken cancellationToken) =>
        Ok(await sales.GetOperationAsync(operationId, cancellationToken));

    [HttpGet("payment-accounts")]
    public async Task<ActionResult<IReadOnlyList<SalePaymentAccountDto>>> PaymentAccounts(CancellationToken cancellationToken) =>
        Ok(await sales.GetPaymentAccountsAsync(cancellationToken));

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<SaleSummaryDto>>> Recent(CancellationToken cancellationToken) =>
        Ok(await sales.GetRecentAsync(cancellationToken));

    [HttpGet("{saleId:guid}")]
    public async Task<ActionResult<SaleDto>> Get(Guid saleId, CancellationToken cancellationToken) =>
        Ok(await sales.GetAsync(saleId, cancellationToken));
}
