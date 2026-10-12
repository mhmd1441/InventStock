namespace InventStock.Application.DTOs.Catalog;

using InventStock.Domain.Enums;

// Deliberately independent of PartDto: private fields cannot appear through inheritance.
public sealed record CounterPartDto
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
}
