using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Reviewable catalogue evidence extracted from one supplier file. It never represents shop stock.</summary>
public sealed class SupplierImportBatch : AuditableEntity
{
    public const int FileNameMaxLength = 255;
    public const int HashMaxLength = 64;
    private readonly List<SupplierImportRow> _rows = new();
    private SupplierImportBatch() { }

    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public string SourceFileName { get; private set; } = null!;
    public string SourceSha256 { get; private set; } = null!;
    public SupplierImportStatus Status { get; private set; } = SupplierImportStatus.Staged;
    public DateTime? CommittedAtUtc { get; private set; }
    public Guid? CommittedByUserId { get; private set; }
    public IReadOnlyCollection<SupplierImportRow> Rows => _rows.AsReadOnly();

    public static SupplierImportBatch Create(Guid supplierId, string fileName, string sha256)
    {
        if (supplierId == Guid.Empty) throw new DomainException("A supplier is required.");
        var normalizedHash = Guard.AgainstNullOrWhiteSpace(sha256, nameof(SourceSha256), HashMaxLength).ToUpperInvariant();
        if (normalizedHash.Length != HashMaxLength || normalizedHash.Any(c => !Uri.IsHexDigit(c)))
            throw new DomainException("Source SHA-256 must contain exactly 64 hexadecimal characters.");
        return new SupplierImportBatch
        {
            SupplierId = supplierId,
            SourceFileName = Guard.AgainstNullOrWhiteSpace(Path.GetFileName(fileName), nameof(SourceFileName), FileNameMaxLength),
            SourceSha256 = normalizedHash
        };
    }

    public SupplierImportRow AddRow(int sourceRowNumber, string partNumber, string name, string? brand,
        string? variant, string? supplierCode, decimal? catalogueCost, MoneyCurrency? currency,
        TaxTreatment taxTreatment)
    {
        if (Status != SupplierImportStatus.Staged) throw new DomainException("A committed import cannot be changed.");
        var row = SupplierImportRow.Create(Id, sourceRowNumber, partNumber, name, brand, variant,
            supplierCode, catalogueCost, currency, taxTreatment);
        _rows.Add(row);
        return row;
    }

    public void Commit(Guid actorId)
    {
        if (Status != SupplierImportStatus.Staged) throw new DomainException("This import is already committed.");
        if (_rows.Count == 0 || _rows.Any(x => x.Decision == SupplierImportDecision.Pending))
            throw new DomainException("Every import row must be reviewed before commit.");
        Status = SupplierImportStatus.Committed;
        CommittedAtUtc = DateTime.UtcNow;
        CommittedByUserId = actorId;
    }
}

public sealed class SupplierImportRow : AuditableEntity
{
    public const int BrandMaxLength = 100;
    public const int VariantMaxLength = 120;
    public const int SupplierCodeMaxLength = 100;
    private SupplierImportRow() { }

    public Guid SupplierImportBatchId { get; private set; }
    public SupplierImportBatch SupplierImportBatch { get; private set; } = null!;
    public int SourceRowNumber { get; private set; }
    public string PartNumber { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Brand { get; private set; } = string.Empty;
    public string Variant { get; private set; } = string.Empty;
    public string? SupplierCode { get; private set; }
    public decimal? CatalogueCost { get; private set; }
    public MoneyCurrency? Currency { get; private set; }
    public TaxTreatment TaxTreatment { get; private set; }
    public SupplierImportDecision Decision { get; private set; } = SupplierImportDecision.Pending;
    public Guid? TargetPartId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? BrandId { get; private set; }
    public StockUnit StockUnit { get; private set; } = StockUnit.Piece;
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }

    internal static SupplierImportRow Create(Guid batchId, int sourceRowNumber, string partNumber,
        string name, string? brand, string? variant, string? supplierCode, decimal? catalogueCost,
        MoneyCurrency? currency, TaxTreatment taxTreatment)
    {
        if (sourceRowNumber <= 0) throw new DomainException("Source row number must be positive.");
        if (catalogueCost is < 0) throw new DomainException("Catalogue cost cannot be negative.");
        if (catalogueCost.HasValue != currency.HasValue)
            throw new DomainException("Currency is required exactly when catalogue cost is present.");
        return new SupplierImportRow
        {
            SupplierImportBatchId = batchId,
            SourceRowNumber = sourceRowNumber,
            PartNumber = Part.NormalizePartNumber(partNumber),
            Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), Part.NameMaxLength),
            Brand = Guard.AgainstTooLong(brand?.Trim(), nameof(Brand), BrandMaxLength) ?? string.Empty,
            Variant = Guard.AgainstTooLong(variant?.Trim(), nameof(Variant), VariantMaxLength) ?? string.Empty,
            SupplierCode = Guard.AgainstTooLong(supplierCode?.Trim(), nameof(SupplierCode), SupplierCodeMaxLength),
            CatalogueCost = catalogueCost,
            Currency = currency,
            TaxTreatment = taxTreatment
        };
    }

    public void Review(SupplierImportDecision decision, Guid? targetPartId, Guid? categoryId,
        Guid? brandId, StockUnit stockUnit, Guid actorId)
    {
        if (decision == SupplierImportDecision.Pending) throw new DomainException("Choose a final review decision.");
        if (decision == SupplierImportDecision.LinkExisting && !targetPartId.HasValue)
            throw new DomainException("Link existing requires a target part.");
        if (decision == SupplierImportDecision.CreateNew && !categoryId.HasValue)
            throw new DomainException("Create new requires a category.");
        if (decision != SupplierImportDecision.LinkExisting) targetPartId = null;
        if (decision != SupplierImportDecision.CreateNew) { categoryId = null; brandId = null; }
        Decision = decision;
        TargetPartId = targetPartId;
        CategoryId = categoryId;
        BrandId = decision == SupplierImportDecision.CreateNew ? brandId ?? ProductBrand.UnknownId : null;
        StockUnit = stockUnit;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewedByUserId = actorId;
    }
}
