using InventStock.Domain.Common;
using InventStock.Domain.Enums;

namespace InventStock.Domain.Entities;

/// <summary>Append-only private supplier cost evidence. A newer row never overwrites an older price.</summary>
public sealed class SupplierCostObservation : AuditableEntity
{
    public const int SupplierCodeMaxLength = 100;
    public const int NotesMaxLength = 500;
    private SupplierCostObservation() { }

    public Guid PartId { get; private set; }
    public Part Part { get; private set; } = null!;
    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public decimal UnitCost { get; private set; }
    public MoneyCurrency Currency { get; private set; }
    public TaxTreatment TaxTreatment { get; private set; }
    public CostSource Source { get; private set; }
    public int PackQuantity { get; private set; }
    public string? SupplierCode { get; private set; }
    public string? Notes { get; private set; }
    public DateTime ObservedAtUtc { get; private set; }
    public Guid? PurchaseReceiptLineId { get; private set; }

    public void LinkToPurchaseReceiptLine(Guid receiptLineId)
    {
        if (receiptLineId == Guid.Empty) throw new ArgumentException("A receipt line ID is required.", nameof(receiptLineId));
        PurchaseReceiptLineId = receiptLineId;
    }

    public static SupplierCostObservation Create(Guid partId, Guid supplierId, decimal unitCost,
        MoneyCurrency currency, TaxTreatment taxTreatment, CostSource source, int packQuantity,
        string? supplierCode, string? notes, DateTime observedAtUtc)
    {
        if (partId == Guid.Empty || supplierId == Guid.Empty) throw new ArgumentException("Part and supplier are required.");
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost));
        if (packQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(packQuantity));
        if (!Enum.IsDefined(currency) || !Enum.IsDefined(taxTreatment) || !Enum.IsDefined(source)) throw new ArgumentException("Invalid cost classification.");
        return new SupplierCostObservation {
            PartId = partId, SupplierId = supplierId, UnitCost = unitCost, Currency = currency,
            TaxTreatment = taxTreatment, Source = source, PackQuantity = packQuantity,
            SupplierCode = Guard.AgainstTooLong(supplierCode?.Trim(), nameof(SupplierCode), SupplierCodeMaxLength),
            Notes = Guard.AgainstTooLong(notes?.Trim(), nameof(Notes), NotesMaxLength),
            ObservedAtUtc = observedAtUtc.Kind == DateTimeKind.Utc ? observedAtUtc : observedAtUtc.ToUniversalTime()
        };
    }
}
