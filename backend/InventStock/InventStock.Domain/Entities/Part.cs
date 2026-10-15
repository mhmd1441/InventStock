using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

public class Part : AuditableEntity
{
    public const int PartNumberMaxLength = 64;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;
    public const int BarcodeMaxLength = 64;

    private readonly List<StockMovement> _movements = new();

    private Part()
    {
    }

    /// <summary>Stored uppercase so lookups and uniqueness are case-insensitive.</summary>
    public string PartNumber { get; private set; } = null!;

    // Generated search keys preserve the original displayed identifiers.
    public string SearchNumber { get; private set; } = null!;

    public string SearchName { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string Brand { get; private set; } = string.Empty;

    public Guid BrandId { get; private set; } = ProductBrand.UnknownId;

    public ProductBrand BrandRecord { get; private set; } = null!;

    public string Variant { get; private set; } = string.Empty;

    public ICollection<SkuReference> References { get; private set; } = new List<SkuReference>();

    public void SetIdentity(string? brand, string? variant)
    {
        Brand = Guard.AgainstTooLong(brand?.Trim().ToUpperInvariant(), nameof(Brand), 80) ?? string.Empty;
        Variant = Guard.AgainstTooLong(variant?.Trim().ToUpperInvariant(), nameof(Variant), 120) ?? string.Empty;
    }

    public void SetBrand(ProductBrand? brand)
    {
        BrandId = brand?.Id ?? ProductBrand.UnknownId;
        Brand = brand is null || brand.Id == ProductBrand.UnknownId ? string.Empty : brand.NormalizedName;
    }

    public string? Description { get; private set; }

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public decimal UnitPrice { get; private set; }

    public int LowStockThreshold { get; private set; }

    public string? Barcode { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>A confirmed sale exceeded book stock; only a physical count clears this warning.</summary>
    public bool NeedsRecount { get; private set; }

    public void RequireRecount() => NeedsRecount = true;
    public void ClearRecountAfterPhysicalCount() => NeedsRecount = false;

    /// <summary>The indivisible base unit written to the stock ledger.</summary>
    public StockUnit StockUnit { get; private set; } = StockUnit.Piece;

    public IReadOnlyCollection<StockMovement> Movements => _movements.AsReadOnly();

    public static Part Create(
        string partNumber,
        string name,
        string? description,
        Guid categoryId,
        Guid? supplierId,
        decimal unitPrice,
        int lowStockThreshold,
        string? barcode)
    {
        return new Part
        {
            PartNumber = NormalizePartNumber(partNumber),
            Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength),
            Description = Guard.AgainstTooLong(description, nameof(Description), DescriptionMaxLength),
            CategoryId = categoryId,
            SupplierId = supplierId,
            UnitPrice = Guard.AgainstNegative(unitPrice, nameof(UnitPrice)),
            LowStockThreshold = Guard.AgainstNegative(lowStockThreshold, nameof(LowStockThreshold)),
            Barcode = Guard.AgainstTooLong(barcode, nameof(Barcode), BarcodeMaxLength)
        };
    }

    public void Update(
        string partNumber,
        string name,
        string? description,
        Guid categoryId,
        Guid? supplierId,
        decimal unitPrice,
        int lowStockThreshold,
        string? barcode)
    {
        PartNumber = NormalizePartNumber(partNumber);
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength);
        Description = Guard.AgainstTooLong(description, nameof(Description), DescriptionMaxLength);
        CategoryId = categoryId;
        SupplierId = supplierId;
        UnitPrice = Guard.AgainstNegative(unitPrice, nameof(UnitPrice));
        LowStockThreshold = Guard.AgainstNegative(lowStockThreshold, nameof(LowStockThreshold));
        Barcode = Guard.AgainstTooLong(barcode, nameof(Barcode), BarcodeMaxLength);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public void SetStockUnit(StockUnit unit, bool hasStockHistory)
    {
        if (!Enum.IsDefined(unit)) throw new DomainException("A valid stock unit is required.");
        if (hasStockHistory && StockUnit != unit)
            throw new DomainException("The stock unit cannot change after stock history exists. Create an audited conversion instead.");
        StockUnit = unit;
    }

    public static string NormalizePartNumber(string partNumber) =>
        Guard.AgainstNullOrWhiteSpace(partNumber, nameof(PartNumber), PartNumberMaxLength).ToUpperInvariant();
}
