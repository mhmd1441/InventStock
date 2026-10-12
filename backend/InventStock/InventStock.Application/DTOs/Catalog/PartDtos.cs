using InventStock.Application.Common.Models;
using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Catalog;

public sealed record PartDto
{
    public Guid Id { get; init; }

    public string PartNumber { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Brand { get; init; } = string.Empty;

    public Guid BrandId { get; init; }

    public string Variant { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public Guid? SupplierId { get; init; }

    public string? SupplierName { get; init; }

    public decimal UnitPrice { get; init; }

    public int LowStockThreshold { get; init; }

    public string? Barcode { get; init; }

    public bool IsActive { get; init; }

    public int QuantityOnHand { get; init; }
    public int PhysicalQuantityOnHand { get; init; }
    public int QuarantinedQuantity { get; init; }

    public StockConfidence StockConfidence { get; init; }

    public StockUnit StockUnit { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public bool IsLowStock => StockConfidence == StockConfidence.Counted && QuantityOnHand <= LowStockThreshold;

    public decimal? StockValue => StockConfidence == StockConfidence.Counted ? QuantityOnHand * UnitPrice : null;
}

public sealed record SavePartRequest
{
    public Guid? BrandId { get; init; }

    // Kept temporarily for old clients. New writes must select BrandId.
    public string? Brand { get; init; }

    public string? Variant { get; init; }

    public string PartNumber { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid CategoryId { get; init; }

    public Guid? SupplierId { get; init; }

    public decimal UnitPrice { get; init; }

    public int LowStockThreshold { get; init; }

    public string? Barcode { get; init; }

    public StockUnit StockUnit { get; init; } = StockUnit.Piece;
}

public enum PartSortField
{
    PartNumber = 0,
    Name = 1,
    UnitPrice = 2,
    QuantityOnHand = 3,
    CreatedAtUtc = 4
}

public sealed class PartQuery : PagedQuery
{
    public string? Search { get; set; }

    public PartSearchMode SearchMode { get; set; } = PartSearchMode.Auto;

    public Guid? CategoryId { get; set; }

    public Guid? SupplierId { get; set; }

    public bool LowStockOnly { get; set; }

    public bool IncludeInactive { get; set; }

    public PartSortField SortBy { get; set; } = PartSortField.PartNumber;

    public bool SortDescending { get; set; }
}

public enum PartSearchMode { Auto, Exact, Prefix, Suffix, Contains }
