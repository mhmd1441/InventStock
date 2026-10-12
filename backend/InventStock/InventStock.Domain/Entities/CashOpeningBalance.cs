using InventStock.Domain.Common;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

public sealed class CashOpeningBalance : AuditableEntity
{
    public const int FingerprintMaxLength = 64;
    private CashOpeningBalance() { }

    public Guid CashAccountId { get; private set; }
    public CashAccount CashAccount { get; private set; } = null!;
    public decimal CountedAmount { get; private set; }
    public DateTime CountedAtUtc { get; private set; }
    public Guid CountedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;
    public Guid CashMovementId { get; private set; }

    public static CashOpeningBalance Create(Guid accountId, decimal amount, DateTime countedAtUtc, Guid actorId,
        Guid operationId, string fingerprint)
    {
        if (accountId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty) throw new DomainException("Account, actor and operation are required.");
        if (amount < 0) throw new DomainException("Opening count cannot be negative.");
        return new CashOpeningBalance
        {
            CashAccountId = accountId, CountedAmount = amount,
            CountedAtUtc = countedAtUtc.Kind == DateTimeKind.Utc ? countedAtUtc : countedAtUtc.ToUniversalTime(),
            CountedByUserId = actorId, OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(PayloadFingerprint), FingerprintMaxLength)
        };
    }

    public void LinkMovement(Guid movementId) => CashMovementId = movementId;
}
