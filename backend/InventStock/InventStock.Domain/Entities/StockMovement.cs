using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>
/// Append-only ledger row. Quantity is signed, so on-hand stock for a part is SUM(QuantityChange).
/// Rows are never updated or deleted; a mistake is corrected by writing a compensating Adjustment.
/// </summary>
public class StockMovement : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    public const int ReferenceMaxLength = 64;

    private StockMovement()
    {
    }

    public Guid PartId { get; private set; }

    public Part Part { get; private set; } = null!;

    public MovementType MovementType { get; private set; }

    /// <summary>Signed: positive adds stock, negative removes it.</summary>
    public int QuantityChange { get; private set; }

    public string? Reason { get; private set; }

    public string? Reference { get; private set; }

    public Guid PerformedByUserId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public static StockMovement StockIn(Guid partId, int quantity, Guid performedByUserId, string? reason, string? reference)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Stock-in quantity must be greater than zero.");
        }

        return Create(partId, MovementType.In, quantity, performedByUserId, reason, reference);
    }

    public static StockMovement StockOut(Guid partId, int quantity, Guid performedByUserId, string? reason, string? reference)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Stock-out quantity must be greater than zero.");
        }

        return Create(partId, MovementType.Out, -quantity, performedByUserId, reason, reference);
    }

    public static StockMovement Adjustment(Guid partId, int quantityChange, Guid performedByUserId, string reason, string? reference)
    {
        if (quantityChange == 0)
        {
            throw new DomainException("Adjustment quantity must not be zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A reason is required for stock adjustments.");
        }

        return Create(partId, MovementType.Adjustment, quantityChange, performedByUserId, reason, reference);
    }

    private static StockMovement Create(
        Guid partId,
        MovementType movementType,
        int quantityChange,
        Guid performedByUserId,
        string? reason,
        string? reference)
    {
        if (partId == Guid.Empty)
        {
            throw new DomainException("A stock movement must reference a part.");
        }

        if (performedByUserId == Guid.Empty)
        {
            throw new DomainException("A stock movement must record the user who performed it.");
        }

        return new StockMovement
        {
            PartId = partId,
            MovementType = movementType,
            QuantityChange = quantityChange,
            Reason = Guard.AgainstTooLong(reason, nameof(Reason), ReasonMaxLength),
            Reference = Guard.AgainstTooLong(reference, nameof(Reference), ReferenceMaxLength),
            PerformedByUserId = performedByUserId,
            OccurredAtUtc = DateTime.UtcNow
        };
    }
}
