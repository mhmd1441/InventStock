using InventStock.Domain.Common;
using InventStock.Domain.Enums;

namespace InventStock.Domain.Entities;

public sealed class CustomerPayment : AuditableEntity
{
    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; } = null!;
    public Guid OperationId { get; private set; }
    public Guid ActorId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public DateTime PostedAtUtc { get; private set; }
    public decimal UsdCredit { get; private set; }
    public decimal ExcessCreditUsd { get; private set; }
    public decimal BalanceBeforeUsd { get; private set; }
    public decimal BalanceAfterUsd { get; private set; }
    public string? Note { get; private set; }
    public List<CustomerPaymentTender> Tenders { get; private set; } = [];
    public static CustomerPayment Create(Customer customer, Guid operation, Guid actor, string fingerprint,
        decimal usd, decimal excessCredit, decimal balance, string? note, DateTime postedAt) => new()
    {
        CustomerId = customer.Id, CustomerName = customer.Name, OperationId = operation, ActorId = actor,
        PayloadFingerprint = fingerprint, PostedAtUtc = postedAt, UsdCredit = usd, ExcessCreditUsd = excessCredit,
        BalanceBeforeUsd = balance, BalanceAfterUsd = balance - usd, Note = note?.Trim()
    };
}

public sealed class CustomerPaymentTender : AuditableEntity
{
    public Guid CustomerPaymentId { get; private set; }
    public int Position { get; private set; }
    public Guid CashAccountId { get; private set; }
    public string AccountName { get; private set; } = null!;
    public MoneyCurrency Currency { get; private set; }
    public bool IsChange { get; private set; }
    public decimal NativeAmount { get; private set; }
    public decimal ConfirmedUsdAmount { get; private set; }
    public static CustomerPaymentTender Create(Guid payment, int position, CashAccount account, bool change, decimal amount, decimal usd) => new()
    {
        CustomerPaymentId = payment, Position = position, CashAccountId = account.Id, AccountName = account.Name,
        Currency = account.Currency, IsChange = change, NativeAmount = amount, ConfirmedUsdAmount = usd
    };
}

/// <summary>Corrects an erroneous recording. Actual refunds remain a different future workflow.</summary>
public sealed class CustomerPaymentCorrection : AuditableEntity
{
    public Guid CustomerId { get; private set; }
    public Guid OriginalPaymentId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid ActorId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public DateTime CorrectedAtUtc { get; private set; }
    public string Reason { get; private set; } = null!;
    public decimal RestoredDebtUsd { get; private set; }
    public decimal BalanceAfterUsd { get; private set; }
    public static CustomerPaymentCorrection Create(CustomerPayment payment, Guid operation, Guid actor, string fingerprint,
        string reason, decimal balanceAfter, DateTime time) => new()
    {
        CustomerId = payment.CustomerId, OriginalPaymentId = payment.Id, OperationId = operation, ActorId = actor,
        PayloadFingerprint = fingerprint, Reason = reason.Trim(), RestoredDebtUsd = payment.UsdCredit,
        BalanceAfterUsd = balanceAfter, CorrectedAtUtc = time
    };
}
