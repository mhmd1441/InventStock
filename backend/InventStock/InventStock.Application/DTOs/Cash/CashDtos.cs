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

public sealed record PostCashReconciliationRequest
{
    public Guid OperationId { get; init; }
    public DateOnly BusinessDate { get; init; }
    /// <summary>Required only when no prior close exists for this account.</summary>
    public decimal? OpeningAmount { get; init; }
    public decimal CountedAmount { get; init; }
    public string? DifferenceReason { get; init; }
    public DateTime CountedAtUtc { get; init; }
}

public sealed record PostCashReconciliationCorrectionRequest
{
    public Guid OperationId { get; init; }
    public decimal CountedAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime CountedAtUtc { get; init; }
}

public sealed record CashReconciliationCorrectionDto(Guid Id, Guid CashReconciliationId,
    decimal ExpectedClosingAmount, decimal CountedAmount, decimal DifferenceAmount, string Reason,
    DateTime CountedAtUtc, DateTime CorrectedAtUtc, Guid CorrectedByUserId, bool IsPartialDay,
    Guid OperationId, DateTime RecordedAtUtc);

public sealed record CashReconciliationDto(Guid Id, Guid CashAccountId, DateOnly BusinessDate,
    decimal OpeningAmount, CashReconciliationOpeningSource OpeningSource, decimal ExpectedClosingAmount,
    decimal CountedAmount, decimal DifferenceAmount, string? DifferenceReason, DateTime CountedAtUtc,
    DateTime ClosedAtUtc, Guid CountedByUserId, bool IsPartialDay, Guid OperationId, DateTime RecordedAtUtc,
    CashReconciliationCorrectionDto? LatestCorrection, Guid? SupersedesReconciliationId);

public sealed record CashReconciliationAccountDto(Guid CashAccountId, string AccountCode, string AccountName,
    MoneyCurrency Currency, CashAccountKind Kind, decimal? OpeningAmount,
    CashReconciliationOpeningSource? OpeningSource, decimal? ExpectedAmount,
    decimal? LastCountedAmount, CashReconciliationDto? Reconciliation,
    decimal PostCloseNetChange, int PostCloseMovementCount, bool RequiresExplicitCorrection);

public sealed record CashReconciliationDayDto(DateOnly BusinessDate,
    IReadOnlyList<CashReconciliationAccountDto> Accounts);

public sealed record SupplierPaymentLineRequest
{
    public SupplierPaymentLineKind Kind { get; init; }
    public decimal OriginalAmount { get; init; }
    public MoneyCurrency Currency { get; init; }
    public decimal AgreedUsdValue { get; init; }
}

public sealed record PostSupplierPaymentRequest
{
    public Guid OperationId { get; init; }
    public Guid SupplierId { get; init; }
    public Guid CashAccountId { get; init; }
    public DateOnly PaidOn { get; init; }
    public string? Note { get; init; }
    public string? ReceiptReference { get; init; }
    public IReadOnlyList<SupplierPaymentLineRequest> Lines { get; init; } = Array.Empty<SupplierPaymentLineRequest>();
}

public sealed record ReverseSupplierPaymentRequest
{
    public Guid OperationId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime ReversedAtUtc { get; init; }
}

public sealed record SupplierPaymentLineDto(Guid Id, SupplierPaymentLineKind Kind, decimal OriginalAmount,
    MoneyCurrency Currency, decimal AgreedUsdValue);

public sealed record SupplierPaymentDto(Guid Id, Guid OperationId, Guid SupplierId, string SupplierName,
    Guid CashAccountId, string CashAccountName, DateOnly PaidOn, string? Note, string? ReceiptReference,
    DateTime RecordedAtUtc, Guid RecordedByUserId, IReadOnlyList<SupplierPaymentLineDto> Lines,
    bool IsReversed, Guid? ReversalId, string? ReversalReason, DateTime? ReversedAtUtc);

public sealed record PostCashTransferRequest
{
    public Guid OperationId { get; init; }
    public Guid SourceCashAccountId { get; init; }
    public Guid DestinationCashAccountId { get; init; }
    public DateOnly TransferredOn { get; init; }
    public decimal SourceAmount { get; init; }
    public MoneyCurrency SourceCurrency { get; init; }
    public decimal DestinationAmount { get; init; }
    public MoneyCurrency DestinationCurrency { get; init; }
    public decimal AgreedRate { get; init; }
    public Guid? FeeCashAccountId { get; init; }
    public decimal? FeeAmount { get; init; }
    public MoneyCurrency? FeeCurrency { get; init; }
    public string? Note { get; init; }
}

public sealed record ReverseCashTransferRequest
{
    public Guid OperationId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime ReversedAtUtc { get; init; }
}

public sealed record CashTransferDto(Guid Id, Guid OperationId, Guid SourceCashAccountId, string SourceAccountName,
    Guid DestinationCashAccountId, string DestinationAccountName, DateOnly TransferredOn, decimal SourceAmount,
    MoneyCurrency SourceCurrency, decimal DestinationAmount, MoneyCurrency DestinationCurrency, decimal AgreedRate,
    Guid? FeeCashAccountId, string? FeeAccountName, decimal? FeeAmount, MoneyCurrency? FeeCurrency, string? Note,
    DateTime RecordedAtUtc, Guid RecordedByUserId, bool IsReversed, Guid? ReversalId, string? ReversalReason,
    DateTime? ReversedAtUtc);
