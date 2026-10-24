using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Immutable daily cash-count snapshot. It never changes ledger movements or an earlier close.</summary>
public sealed class CashReconciliation : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    public const int FingerprintMaxLength = 64;
    private CashReconciliation() { }

    public Guid CashAccountId { get; private set; }
    public CashAccount CashAccount { get; private set; } = null!;
    public DateOnly BusinessDate { get; private set; }
    public decimal OpeningAmount { get; private set; }
    public CashReconciliationOpeningSource OpeningSource { get; private set; }
    public decimal ExpectedClosingAmount { get; private set; }
    public decimal CountedAmount { get; private set; }
    public decimal DifferenceAmount { get; private set; }
    public string? DifferenceReason { get; private set; }
    public DateTime CountedAtUtc { get; private set; }
    public DateTime ClosedAtUtc { get; private set; }
    public Guid CountedByUserId { get; private set; }
    public bool IsPartialDay { get; private set; }
    /// <summary>Links an end-of-day close to the immutable in-day partial snapshot it supersedes.</summary>
    public Guid? SupersedesCashReconciliationId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;

    public static CashReconciliation Create(Guid accountId, DateOnly businessDate, decimal openingAmount,
        CashReconciliationOpeningSource openingSource, decimal expectedClosingAmount, decimal countedAmount,
        string? differenceReason, DateTime countedAtUtc, DateTime closedAtUtc, Guid actorId,
        bool isPartialDay, Guid? supersedesCashReconciliationId, Guid operationId, string payloadFingerprint)
    {
        if (accountId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty)
            throw new DomainException("Cash account, actor and operation are required.");
        if (openingAmount < 0 || countedAmount < 0)
            throw new DomainException("Cash opening and counted amounts cannot be negative.");

        var difference = countedAmount - expectedClosingAmount;
        if (difference != 0 && string.IsNullOrWhiteSpace(differenceReason))
            throw new DomainException("A reason is required when the physical count differs from the expected amount.");

        return new CashReconciliation
        {
            CashAccountId = accountId,
            BusinessDate = businessDate,
            OpeningAmount = openingAmount,
            OpeningSource = openingSource,
            ExpectedClosingAmount = expectedClosingAmount,
            CountedAmount = countedAmount,
            DifferenceAmount = difference,
            DifferenceReason = difference == 0 ? null : Guard.AgainstNullOrWhiteSpace(differenceReason, nameof(DifferenceReason), ReasonMaxLength),
            CountedAtUtc = ToUtc(countedAtUtc),
            ClosedAtUtc = ToUtc(closedAtUtc),
            CountedByUserId = actorId,
            IsPartialDay = isPartialDay,
            SupersedesCashReconciliationId = supersedesCashReconciliationId,
            OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(payloadFingerprint, nameof(PayloadFingerprint), FingerprintMaxLength)
        };
    }

    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
