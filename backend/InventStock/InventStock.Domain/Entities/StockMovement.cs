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
    public const int PayloadFingerprintMaxLength = 64;

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

    public Guid? OperationId { get; private set; }

    public string? PayloadFingerprint { get; private set; }

    /// <summary>Present only for a physical-count checkpoint.</summary>
    public int? CountedQuantity { get; private set; }

    /// <summary>Ledger balance observed under the part lock immediately before the count adjustment.</summary>
    public int? BookQuantityBeforeCount { get; private set; }

    /// <summary>Links stock added by receiving to its immutable source line.</summary>
    public Guid? PurchaseReceiptLineId { get; private set; }

    public void LinkToPurchaseReceiptLine(Guid receiptLineId)
    {
        if (receiptLineId == Guid.Empty) throw new DomainException("A receipt line ID is required.");
        PurchaseReceiptLineId = receiptLineId;
    }

    public void SetOperation(Guid operationId, string payloadFingerprint)
    {
        if (operationId == Guid.Empty) throw new DomainException("A stock operation ID is required.");
        OperationId = operationId;
        PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(payloadFingerprint, nameof(PayloadFingerprint), PayloadFingerprintMaxLength);
    }

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
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required for raw stock-out. This movement does not record a sale or payment.");
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

    public static StockMovement PhysicalCount(Guid partId, int countedQuantity, int bookQuantityBefore, Guid performedByUserId, string? reason)
    {
        if (countedQuantity < 0) throw new DomainException("A physical count cannot be negative.");
        var movement = Create(partId, MovementType.PhysicalCount, countedQuantity - bookQuantityBefore,
            performedByUserId, string.IsNullOrWhiteSpace(reason) ? "Physical count" : reason, null);
        movement.CountedQuantity = countedQuantity;
        movement.BookQuantityBeforeCount = bookQuantityBefore;
        return movement;
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
