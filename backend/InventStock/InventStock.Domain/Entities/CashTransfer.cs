using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Immutable transfer between shop-held cash accounts. A fee is explicitly separate from the transfer legs.</summary>
public sealed class CashTransfer : AuditableEntity
{
    public const int NoteMaxLength = 500;
    public const int FingerprintMaxLength = 64;
    private CashTransfer() { }

    public Guid SourceCashAccountId { get; private set; }
    public Guid DestinationCashAccountId { get; private set; }
    public Guid? FeeCashAccountId { get; private set; }
    public DateOnly TransferredOn { get; private set; }
    public decimal SourceAmount { get; private set; }
    public MoneyCurrency SourceCurrency { get; private set; }
    public decimal DestinationAmount { get; private set; }
    public MoneyCurrency DestinationCurrency { get; private set; }
    public decimal AgreedRate { get; private set; }
    public decimal? FeeAmount { get; private set; }
    public MoneyCurrency? FeeCurrency { get; private set; }
    public string? Note { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;

    public static CashTransfer Create(Guid sourceCashAccountId, Guid destinationCashAccountId, Guid? feeCashAccountId,
        DateOnly transferredOn, decimal sourceAmount, MoneyCurrency sourceCurrency, decimal destinationAmount,
        MoneyCurrency destinationCurrency, decimal agreedRate, decimal? feeAmount, MoneyCurrency? feeCurrency,
        string? note, Guid actorId, Guid operationId, string fingerprint)
    {
        if (sourceCashAccountId == Guid.Empty || destinationCashAccountId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty)
            throw new DomainException("Source, destination, actor and operation are required.");
        if (sourceCashAccountId == destinationCashAccountId) throw new DomainException("Source and destination accounts must differ.");
        if (sourceAmount <= 0 || destinationAmount <= 0 || agreedRate <= 0) throw new DomainException("Transfer amounts and agreed rate must be positive.");
        if ((feeAmount.HasValue && (!feeCurrency.HasValue || !feeCashAccountId.HasValue || feeAmount <= 0)) ||
            (!feeAmount.HasValue && (feeCurrency.HasValue || feeCashAccountId.HasValue)))
            throw new DomainException("A transfer fee requires its account, amount and currency together.");
        return new CashTransfer
        {
            SourceCashAccountId = sourceCashAccountId, DestinationCashAccountId = destinationCashAccountId,
            FeeCashAccountId = feeCashAccountId, TransferredOn = transferredOn, SourceAmount = sourceAmount,
            SourceCurrency = sourceCurrency, DestinationAmount = destinationAmount, DestinationCurrency = destinationCurrency,
            AgreedRate = agreedRate, FeeAmount = feeAmount, FeeCurrency = feeCurrency,
            Note = Guard.AgainstTooLong(note, nameof(Note), NoteMaxLength), RecordedByUserId = actorId,
            OperationId = operationId, PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(PayloadFingerprint), FingerprintMaxLength)
        };
    }
}

public sealed class CashTransferReversal : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    public const int FingerprintMaxLength = 64;
    private CashTransferReversal() { }
    public Guid CashTransferId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime ReversedAtUtc { get; private set; }
    public Guid ReversedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;

    public static CashTransferReversal Create(Guid cashTransferId, string reason, DateTime reversedAtUtc, Guid actorId, Guid operationId, string fingerprint)
    {
        if (cashTransferId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty) throw new DomainException("Transfer, actor and operation are required.");
        return new CashTransferReversal { CashTransferId = cashTransferId, Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(reason), ReasonMaxLength),
            ReversedAtUtc = reversedAtUtc.ToUniversalTime(), ReversedByUserId = actorId, OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(fingerprint), FingerprintMaxLength) };
    }
}
