using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Cash;

public sealed record CashAccountDto(Guid Id, string Code, string Name, MoneyCurrency Currency,
    CashAccountKind Kind, bool IsActive, CashOpeningBalanceDto? OpeningBalance);

public sealed record CashOpeningBalanceDto(Guid Id, Guid CashAccountId, decimal CountedAmount,
    DateTime CountedAtUtc, Guid CountedByUserId, DateTime RecordedAtUtc, Guid OperationId);

public sealed record PostCashOpeningBalanceRequest
{
    public Guid OperationId { get; init; }
    public decimal CountedAmount { get; init; }
    public DateTime CountedAtUtc { get; init; }
}

public sealed record ExpenseCategoryDto(Guid Id, string Name, bool IsActive);
public sealed record SaveExpenseCategoryRequest(string Name);
public sealed record SetExpenseCategoryActiveRequest(bool IsActive);

public sealed record PostOutgoingPaymentRequest
{
    public Guid OperationId { get; init; }
    public Guid ExpenseCategoryId { get; init; }
    public PaymentFundingSource FundingSource { get; init; }
    public Guid? CashAccountId { get; init; }
    public decimal OriginalAmount { get; init; }
    public MoneyCurrency Currency { get; init; }
    public decimal AgreedUsdValue { get; init; }
    public DateOnly PaidOn { get; init; }
    public string? Payee { get; init; }
    public string? Description { get; init; }
    public string? ForPeriod { get; init; }
    public string? ReceiptReference { get; init; }
}

public sealed record ReverseOutgoingPaymentRequest
{
    public Guid OperationId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime ReversedAtUtc { get; init; }
}

public sealed record OutgoingPaymentDto(Guid Id, Guid OperationId, Guid ExpenseCategoryId,
    string CategoryName, PaymentFundingSource FundingSource, Guid? CashAccountId, string? CashAccountName,
    decimal OriginalAmount, MoneyCurrency Currency, decimal AgreedUsdValue, DateOnly PaidOn,
    DateTime RecordedAtUtc, Guid RecordedByUserId, string? Payee, string? Description,
    string? ForPeriod, string? ReceiptReference, bool IsReversed, Guid? ReversalId,
    string? ReversalReason, DateTime? ReversedAtUtc);

public sealed record MonthlyOutgoingTotalDto(Guid ExpenseCategoryId, string CategoryName,
    decimal AgreedUsdTotal, int PaymentCount);

public sealed record OutgoingPaymentListDto(int Year, int Month,
    IReadOnlyList<OutgoingPaymentDto> Payments, IReadOnlyList<MonthlyOutgoingTotalDto> Totals);
