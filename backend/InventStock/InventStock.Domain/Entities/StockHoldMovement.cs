using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Append-only movement into/out of quarantine. Physical total is unchanged.</summary>
public sealed class StockHoldMovement : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    private StockHoldMovement() { }
    public Guid PartId { get; private set; }
    public Part Part { get; private set; } = null!;
    public StockHoldAction Action { get; private set; }
    public int QuantityChange { get; private set; }
    public string Reason { get; private set; } = null!;
    public bool PhysicalSeparationConfirmed { get; private set; }
    public Guid PerformedByUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;

    public static StockHoldMovement Create(Guid partId, StockHoldAction action, int quantity, string reason,
        bool physicalSeparationConfirmed, Guid userId, Guid operationId, string fingerprint)
    {
        if (partId == Guid.Empty || userId == Guid.Empty || operationId == Guid.Empty) throw new DomainException("Part, user and operation are required.");
        if (quantity <= 0) throw new DomainException("Hold quantity must be positive.");
        if (action == StockHoldAction.Hold && !physicalSeparationConfirmed)
            throw new DomainException("Physically separate or clearly tag the held goods before confirming.");
        return new StockHoldMovement {
            PartId = partId, Action = action, QuantityChange = action == StockHoldAction.Hold ? quantity : -quantity,
            Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(Reason), ReasonMaxLength),
            PhysicalSeparationConfirmed = physicalSeparationConfirmed, PerformedByUserId = userId,
            OccurredAtUtc = DateTime.UtcNow, OperationId = operationId, PayloadFingerprint = fingerprint
        };
    }
}
