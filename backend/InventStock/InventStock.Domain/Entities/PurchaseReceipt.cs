using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>
/// Immutable record of goods that physically arrived from a supplier. Posting a receipt does not
/// mean the supplier was paid; supplier settlement is a separate money operation.
/// </summary>
public sealed class PurchaseReceipt : AuditableEntity
{
    public const int ReferenceMaxLength = 100;
    public const int NotesMaxLength = 500;
    public const int PayloadFingerprintMaxLength = 64;

    private readonly List<PurchaseReceiptLine> _lines = new();
    private PurchaseReceipt() { }

    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public string? SupplierReference { get; private set; }
    public string? Notes { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public Guid ReceivedByUserId { get; private set; }
    public IReadOnlyCollection<PurchaseReceiptLine> Lines => _lines.AsReadOnly();

    public static PurchaseReceipt Create(Guid supplierId, Guid operationId, string payloadFingerprint,
        string? supplierReference, string? notes, DateTime receivedAtUtc, Guid receivedByUserId)
    {
        if (supplierId == Guid.Empty) throw new DomainException("A supplier is required.");
        if (operationId == Guid.Empty) throw new DomainException("A receiving operation ID is required.");
        if (receivedByUserId == Guid.Empty) throw new DomainException("The receiving user is required.");
        if (receivedAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new DomainException("Received time cannot be in the future.");

        return new PurchaseReceipt
        {
            SupplierId = supplierId,
            OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(payloadFingerprint, nameof(PayloadFingerprint), PayloadFingerprintMaxLength),
            SupplierReference = Guard.AgainstTooLong(supplierReference?.Trim(), nameof(SupplierReference), ReferenceMaxLength),
            Notes = Guard.AgainstTooLong(notes?.Trim(), nameof(Notes), NotesMaxLength),
            ReceivedAtUtc = receivedAtUtc.Kind == DateTimeKind.Utc ? receivedAtUtc : receivedAtUtc.ToUniversalTime(),
            ReceivedByUserId = receivedByUserId
        };
    }

    public PurchaseReceiptLine AddLine(Guid partId, int packageCount, int baseUnitsPerPackage,
        decimal? costPerPackage, MoneyCurrency? currency, TaxTreatment taxTreatment,
        string? supplierCode, string? notes)
    {
        var line = PurchaseReceiptLine.Create(Id, partId, packageCount, baseUnitsPerPackage,
            costPerPackage, currency, taxTreatment, supplierCode, notes);
        _lines.Add(line);
        return line;
    }
}

public sealed class PurchaseReceiptLine : AuditableEntity
{
    public const int SupplierCodeMaxLength = 100;
    public const int NotesMaxLength = 300;
    private PurchaseReceiptLine() { }

    public Guid PurchaseReceiptId { get; private set; }
    public PurchaseReceipt PurchaseReceipt { get; private set; } = null!;
    public Guid PartId { get; private set; }
    public Part Part { get; private set; } = null!;
    public int PackageCount { get; private set; }
    public int BaseUnitsPerPackage { get; private set; }
    public int BaseQuantity { get; private set; }
    public decimal? CostPerPackage { get; private set; }
    public MoneyCurrency? Currency { get; private set; }
    public TaxTreatment TaxTreatment { get; private set; }
    public string? SupplierCode { get; private set; }
    public string? Notes { get; private set; }
    public Guid StockMovementId { get; private set; }
    public Guid? SupplierCostObservationId { get; private set; }

    public void LinkArtifacts(Guid stockMovementId, Guid? supplierCostObservationId)
    {
        if (stockMovementId == Guid.Empty) throw new DomainException("A receipt line must link to its stock movement.");
        StockMovementId = stockMovementId;
        SupplierCostObservationId = supplierCostObservationId;
    }

    internal static PurchaseReceiptLine Create(Guid receiptId, Guid partId, int packageCount,
        int baseUnitsPerPackage, decimal? costPerPackage, MoneyCurrency? currency,
        TaxTreatment taxTreatment, string? supplierCode, string? notes)
    {
        if (receiptId == Guid.Empty || partId == Guid.Empty) throw new DomainException("Receipt and part are required.");
        if (packageCount <= 0 || baseUnitsPerPackage <= 0) throw new DomainException("Package quantities must be greater than zero.");
        int baseQuantity;
        try { baseQuantity = checked(packageCount * baseUnitsPerPackage); }
        catch (OverflowException) { throw new DomainException("Received quantity is too large."); }
        if (costPerPackage is < 0) throw new DomainException("Purchase cost cannot be negative.");
        if (costPerPackage.HasValue != currency.HasValue)
            throw new DomainException("Currency is required exactly when a purchase cost is entered.");
        if (currency.HasValue && !Enum.IsDefined(currency.Value)) throw new DomainException("Invalid currency.");
        if (!Enum.IsDefined(taxTreatment)) throw new DomainException("Invalid tax treatment.");

        return new PurchaseReceiptLine
        {
            PurchaseReceiptId = receiptId,
            PartId = partId,
            PackageCount = packageCount,
            BaseUnitsPerPackage = baseUnitsPerPackage,
            BaseQuantity = baseQuantity,
            CostPerPackage = costPerPackage,
            Currency = currency,
            TaxTreatment = taxTreatment,
            SupplierCode = Guard.AgainstTooLong(supplierCode?.Trim(), nameof(SupplierCode), SupplierCodeMaxLength),
            Notes = Guard.AgainstTooLong(notes?.Trim(), nameof(Notes), NotesMaxLength)
        };
    }
}
