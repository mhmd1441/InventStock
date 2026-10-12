using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

public sealed class OutgoingPayment : AuditableEntity
{
    public const int TextMaxLength = 300;
    public const int ReceiptReferenceMaxLength = 120;
    public const int FingerprintMaxLength = 64;
    private OutgoingPayment() { }

    public Guid ExpenseCategoryId { get; private set; }
    public ExpenseCategory ExpenseCategory { get; private set; } = null!;
    public PaymentFundingSource FundingSource { get; private set; }
    public Guid? CashAccountId { get; private set; }
    public CashAccount? CashAccount { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public MoneyCurrency Currency { get; private set; }
    public decimal AgreedUsdValue { get; private set; }
    public DateOnly PaidOn { get; private set; }
    public string? Payee { get; private set; }
    public string? Description { get; private set; }
    public string? ForPeriod { get; private set; }
    public string? ReceiptReference { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;
    public Guid? CashMovementId { get; private set; }

    public static OutgoingPayment Create(Guid categoryId, PaymentFundingSource fundingSource, Guid? accountId,
        decimal originalAmount, MoneyCurrency currency, decimal agreedUsdValue, DateOnly paidOn,
        string? payee, string? description, string? forPeriod, string? receiptReference,
        Guid actorId, Guid operationId, string fingerprint)
    {
        if (categoryId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty) throw new DomainException("Category, actor and operation are required.");
        if (originalAmount <= 0 || agreedUsdValue <= 0) throw new DomainException("Payment amount and agreed USD value must be positive.");
        if (fundingSource == PaymentFundingSource.ShopAccount && !accountId.HasValue) throw new DomainException("A shop-funded payment requires an account.");
        if (fundingSource == PaymentFundingSource.PersonalPocket && accountId.HasValue) throw new DomainException("A personally funded payment must not reduce a shop account.");
        return new OutgoingPayment
        {
            ExpenseCategoryId = categoryId, FundingSource = fundingSource, CashAccountId = accountId,
            OriginalAmount = originalAmount, Currency = currency, AgreedUsdValue = agreedUsdValue,
            PaidOn = paidOn,
            Payee = Guard.AgainstTooLong(payee, nameof(Payee), TextMaxLength),
            Description = Guard.AgainstTooLong(description, nameof(Description), TextMaxLength),
            ForPeriod = Guard.AgainstTooLong(forPeriod, nameof(ForPeriod), TextMaxLength),
            ReceiptReference = Guard.AgainstTooLong(receiptReference, nameof(ReceiptReference), ReceiptReferenceMaxLength),
            RecordedByUserId = actorId, OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(PayloadFingerprint), FingerprintMaxLength)
        };
    }

    public void LinkMovement(Guid movementId) => CashMovementId = movementId;
}
