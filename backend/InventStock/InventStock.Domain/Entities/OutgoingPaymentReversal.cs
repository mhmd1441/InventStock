using InventStock.Domain.Common;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

public sealed class OutgoingPaymentReversal : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    public const int FingerprintMaxLength = 64;
    private OutgoingPaymentReversal() { }

    public Guid OutgoingPaymentId { get; private set; }
    public OutgoingPayment OutgoingPayment { get; private set; } = null!;
    public string Reason { get; private set; } = string.Empty;
    public DateTime ReversedAtUtc { get; private set; }
    public Guid ReversedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;
    public Guid? CashMovementId { get; private set; }

    public static OutgoingPaymentReversal Create(Guid paymentId, string reason, DateTime reversedAtUtc,
        Guid actorId, Guid operationId, string fingerprint)
    {
        if (paymentId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty) throw new DomainException("Payment, actor and operation are required.");
        return new OutgoingPaymentReversal
        {
            OutgoingPaymentId = paymentId,
            Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(Reason), ReasonMaxLength),
            ReversedAtUtc = reversedAtUtc.Kind == DateTimeKind.Utc ? reversedAtUtc : reversedAtUtc.ToUniversalTime(),
            ReversedByUserId = actorId, OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(PayloadFingerprint), FingerprintMaxLength)
        };
    }

    public void LinkMovement(Guid movementId) => CashMovementId = movementId;
}
