using InventStock.Application.Common;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Customers;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Staff)]
[Route("api/customers")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerSummaryDto>>> Search([FromQuery] CustomerQuery query, CancellationToken ct) => Ok(await customers.SearchAsync(query, ct));
    [HttpPost]
    public async Task<ActionResult<CustomerProfileDto>> Create(SaveCustomerRequest request, CancellationToken ct)
    {
        var customer = await customers.CreateAsync(request, ct);
        return Created($"/api/customers/{customer.Id}/statement", customer);
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerProfileDto>> Update(Guid id, SaveCustomerRequest request, CancellationToken ct) => Ok(await customers.UpdateAsync(id, request, ct));
    [HttpGet("{id:guid}/statement")]
    public async Task<ActionResult<CustomerStatementDto>> Statement(Guid id, [FromQuery] CustomerStatementQuery query, CancellationToken ct) => Ok(await customers.StatementAsync(id, query, ct));
    [HttpPost("{id:guid}/opening-balance")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CustomerOpeningDto>> Opening(Guid id, PostCustomerOpeningRequest request, CancellationToken ct) => Ok(await customers.OpenAccountAsync(id, request, ct));
    [HttpGet("{id:guid}/opening-balance")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CustomerOpeningStatusDto>> OpeningStatus(Guid id, CancellationToken ct)
    {
        var status = await customers.OpeningStatusAsync(id, ct);
        return status is null ? NoContent() : Ok(status);
    }
    [HttpPost("opening-balances/{openingEntryId:guid}/corrections")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CustomerOpeningCorrectionDto>> CorrectOpening(Guid openingEntryId,
        CorrectCustomerOpeningRequest request, CancellationToken ct) => Ok(await customers.CorrectOpeningAsync(openingEntryId, request, ct));
    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<CustomerPaymentDto>> Payment(Guid id, PostCustomerPaymentRequest request, CancellationToken ct)
    {
        var payment = await customers.PayAsync(id, request, ct);
        return Created($"/api/customers/operations/payments/{payment.OperationId}", payment);
    }
    [HttpPost("payments/{paymentId:guid}/corrections")]
    public async Task<ActionResult<CustomerPaymentCorrectionDto>> Correct(Guid paymentId, CorrectCustomerPaymentRequest request, CancellationToken ct) => Ok(await customers.CorrectPaymentAsync(paymentId, request, ct));
    [HttpGet("operations/profiles/{operationId:guid}")]
    public async Task<ActionResult<CustomerProfileDto>> ProfileOperation(Guid operationId, CancellationToken ct) => Ok(await customers.GetProfileOperationAsync(operationId, ct));
    [HttpGet("operations/openings/{operationId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CustomerOpeningDto>> OpeningOperation(Guid operationId, CancellationToken ct) => Ok(await customers.GetOpeningOperationAsync(operationId, ct));
    [HttpGet("operations/opening-corrections/{operationId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CustomerOpeningCorrectionDto>> OpeningCorrectionOperation(Guid operationId,
        CancellationToken ct) => Ok(await customers.GetOpeningCorrectionOperationAsync(operationId, ct));
    [HttpGet("operations/payments/{operationId:guid}")]
    public async Task<ActionResult<CustomerPaymentDto>> PaymentOperation(Guid operationId, CancellationToken ct) => Ok(await customers.GetPaymentOperationAsync(operationId, ct));
    [HttpGet("operations/corrections/{operationId:guid}")]
    public async Task<ActionResult<CustomerPaymentCorrectionDto>> CorrectionOperation(Guid operationId, CancellationToken ct) => Ok(await customers.GetCorrectionOperationAsync(operationId, ct));
}
