using InventStock.Application.Common;
using InventStock.Application.DTOs.Receiving;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/admin/receipts")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class ReceivingController(IReceivingService receiving) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PurchaseReceiptDto>> Post(PostPurchaseReceiptRequest request, CancellationToken cancellationToken)
    {
        var receipt = await receiving.PostAsync(request, cancellationToken);
        return Created($"/api/admin/receipts/{receipt.Id}", receipt);
    }

    [HttpGet("{receiptId:guid}")]
    public async Task<ActionResult<PurchaseReceiptDto>> Get(Guid receiptId, CancellationToken cancellationToken) =>
        Ok(await receiving.GetAsync(receiptId, cancellationToken));

    [HttpGet("operations/{operationId:guid}")]
    public async Task<ActionResult<ReceivingOperationStatusDto>> Status(Guid operationId, CancellationToken cancellationToken) =>
        Ok(await receiving.GetOperationStatusAsync(operationId, cancellationToken));
}
