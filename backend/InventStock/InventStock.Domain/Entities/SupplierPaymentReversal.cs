using InventStock.Domain.Common;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

public sealed class SupplierPaymentReversal : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    public const int FingerprintMaxLength = 64;
    private SupplierPaymentReversal() { }
    public Guid SupplierPaymentId { get; private set; }
    public SupplierPayment SupplierPayment { get; private set; } = null!;
    public string Reason { get; private set; } = string.Empty;
    public DateTime ReversedAtUtc { get; private set; }
    public Guid ReversedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;

    public static SupplierPaymentReversal Create(Guid paymentId, string reason, DateTime reversedAtUtc,
        Guid actorId, Guid operationId, string fingerprint) => new()
    {
        SupplierPaymentId = paymentId,
        Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(Reason), ReasonMaxLength),
        ReversedAtUtc = reversedAtUtc.Kind == DateTimeKind.Utc ? reversedAtUtc : reversedAtUtc.ToUniversalTime(),
        ReversedByUserId = actorId,
        OperationId = operationId,
        PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(PayloadFingerprint), FingerprintMaxLength)
    };
}
