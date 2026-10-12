using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Sales;

namespace InventStock.Application.DTOs.Customers;

public sealed record SaveCustomerRequest(Guid OperationId, string Name, string? DistinguishingLabel = null,
    string? Phone = null, string? Address = null, int ExpectedRevision = 0);
public sealed record CustomerProfileDto(Guid Id, string Name, string? DistinguishingLabel, string? Phone,
    string? Address, int Revision);
public sealed record CustomerSummaryDto(CustomerProfileDto Customer, decimal BalanceUsd);
public sealed class CustomerQuery : PagedQuery { public string? Search { get; set; } }
public sealed class CustomerStatementQuery : PagedQuery { }
public sealed record CustomerStatementEntryDto(Guid Id, string Kind, decimal AmountUsd, Guid? SaleId,
    Guid? CustomerPaymentId, Guid OperationId, Guid ActorId, DateTime RecordedAtUtc, DateTime? EffectiveAtUtc, string? Reason);
public sealed record CustomerStatementDto(CustomerSummaryDto Summary, PagedResult<CustomerStatementEntryDto> Entries);
public sealed record PostCustomerOpeningRequest(Guid OperationId, decimal BalanceUsd, DateTime AsOfUtc, string Reason);
public sealed record CustomerOpeningDto(Guid Id, Guid CustomerId, Guid OperationId, Guid ActorId,
    decimal BalanceUsd, DateTime AsOfUtc, string Reason);
public sealed record PostCustomerPaymentRequest
{
    public Guid OperationId { get; init; }
    public decimal ConfirmedUsdCredit { get; init; }
    public decimal ConfirmedExcessCreditUsd { get; init; }
    public IReadOnlyList<SaleTenderRequest> Payments { get; init; } = [];
    public IReadOnlyList<SaleTenderRequest> Change { get; init; } = [];
    public string? Note { get; init; }
}
public sealed record CustomerPaymentDto(Guid Id, Guid OperationId, Guid CustomerId, string CustomerName,
    Guid ActorId, DateTime PostedAtUtc, decimal UsdCredit, decimal ExcessCreditUsd, decimal BalanceBeforeUsd,
    decimal BalanceAfterUsd, string? Note, IReadOnlyList<SaleTenderDto> Tenders);
public sealed record CorrectCustomerPaymentRequest(Guid OperationId, string Reason, decimal ConfirmedBalanceAfterUsd);
public sealed record CustomerPaymentCorrectionDto(Guid Id, Guid OperationId, Guid CustomerId, Guid OriginalPaymentId,
    Guid ActorId, DateTime CorrectedAtUtc, string Reason, decimal RestoredDebtUsd, decimal BalanceAfterUsd);
