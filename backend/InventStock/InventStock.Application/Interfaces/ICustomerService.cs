using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Customers;

namespace InventStock.Application.Interfaces;

public interface ICustomerService
{
    Task<CustomerProfileDto> CreateAsync(SaveCustomerRequest request, CancellationToken ct = default);
    Task<CustomerProfileDto> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken ct = default);
    Task<PagedResult<CustomerSummaryDto>> SearchAsync(CustomerQuery query, CancellationToken ct = default);
    Task<CustomerStatementDto> StatementAsync(Guid id, CustomerStatementQuery query, CancellationToken ct = default);
    Task<CustomerProfileDto> GetProfileOperationAsync(Guid operationId, CancellationToken ct = default);
    Task<CustomerOpeningDto> OpenAccountAsync(Guid id, PostCustomerOpeningRequest request, CancellationToken ct = default);
    Task<CustomerOpeningDto> GetOpeningOperationAsync(Guid operationId, CancellationToken ct = default);
    Task<CustomerPaymentDto> PayAsync(Guid id, PostCustomerPaymentRequest request, CancellationToken ct = default);
    Task<CustomerPaymentDto> GetPaymentOperationAsync(Guid operationId, CancellationToken ct = default);
    Task<CustomerPaymentCorrectionDto> CorrectPaymentAsync(Guid paymentId, CorrectCustomerPaymentRequest request, CancellationToken ct = default);
    Task<CustomerPaymentCorrectionDto> GetCorrectionOperationAsync(Guid operationId, CancellationToken ct = default);
}
