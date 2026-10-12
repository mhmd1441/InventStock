using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Imports;

public sealed record StageSupplierImportRequest
{
    public Guid SupplierId { get; init; }
    public string SourceFileName { get; init; } = string.Empty;
    public string SourceSha256 { get; init; } = string.Empty;
    public IReadOnlyList<StageSupplierImportRowRequest> Rows { get; init; } = [];
}

public sealed record StageSupplierImportRowRequest
{
    public int SourceRowNumber { get; init; }
    public string PartNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? Variant { get; init; }
    public string? SupplierCode { get; init; }
    public decimal? CatalogueCost { get; init; }
    public MoneyCurrency? Currency { get; init; }
    public TaxTreatment TaxTreatment { get; init; } = TaxTreatment.Unknown;
}

public sealed record ReviewSupplierImportRowRequest
{
    public SupplierImportDecision Decision { get; init; }
    public Guid? TargetPartId { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? BrandId { get; init; }
    public StockUnit StockUnit { get; init; } = StockUnit.Piece;
}

public sealed record SupplierImportBatchDto(Guid Id, Guid SupplierId, string SupplierName,
    string SourceFileName, string SourceSha256, SupplierImportStatus Status, DateTime CreatedAtUtc,
    DateTime? CommittedAtUtc, IReadOnlyList<SupplierImportRowDto> Rows);

public sealed record SupplierImportRowDto(Guid Id, int SourceRowNumber, string PartNumber, string Name,
    string Brand, string Variant, string? SupplierCode, decimal? CatalogueCost, MoneyCurrency? Currency,
    TaxTreatment TaxTreatment, SupplierImportDecision Decision, Guid? TargetPartId, Guid? CategoryId,
    Guid? BrandId, StockUnit StockUnit, DateTime? ReviewedAtUtc);

public sealed record CommitSupplierImportResult(Guid BatchId, int CreatedParts, int LinkedParts,
    int SkippedRows, int CostObservations, IReadOnlyList<Guid> ResultPartIds);
