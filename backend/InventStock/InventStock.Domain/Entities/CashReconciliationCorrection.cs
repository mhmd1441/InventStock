using InventStock.Domain.Common;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Append-only replacement count for a close that was entered early or incorrectly.</summary>
public sealed class CashReconciliationCorrection : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    public const int FingerprintMaxLength = 64;
    private CashReconciliationCorrection() { }

    public Guid CashReconciliationId { get; private set; }
    public CashReconciliation CashReconciliation { get; private set; } = null!;
    public decimal ExpectedClosingAmount { get; private set; }
    public decimal CountedAmount { get; private set; }
    public decimal DifferenceAmount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime CountedAtUtc { get; private set; }
    public DateTime CorrectedAtUtc { get; private set; }
    public Guid CorrectedByUserId { get; private set; }
    public bool IsPartialDay { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;

    public static CashReconciliationCorrection Create(Guid reconciliationId, decimal expectedClosingAmount,
        decimal countedAmount, string reason, DateTime countedAtUtc, DateTime correctedAtUtc, Guid actorId,
        bool isPartialDay, Guid operationId, string payloadFingerprint)
    {
        if (reconciliationId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty)
            throw new DomainException("Reconciliation, actor and operation are required.");
        if (countedAmount < 0)
            throw new DomainException("The corrected physical count cannot be negative.");

        return new CashReconciliationCorrection
        {
            CashReconciliationId = reconciliationId,
            ExpectedClosingAmount = expectedClosingAmount,
            CountedAmount = countedAmount,
            DifferenceAmount = countedAmount - expectedClosingAmount,
            Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(Reason), ReasonMaxLength),
            CountedAtUtc = ToUtc(countedAtUtc),
            CorrectedAtUtc = ToUtc(correctedAtUtc),
            CorrectedByUserId = actorId,
            IsPartialDay = isPartialDay,
            OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(payloadFingerprint, nameof(PayloadFingerprint), FingerprintMaxLength)
        };
    }

    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
