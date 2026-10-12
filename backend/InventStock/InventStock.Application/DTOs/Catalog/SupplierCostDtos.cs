using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Catalog;

public sealed record SupplierCostObservationDto(Guid Id, Guid PartId, Guid SupplierId, string SupplierName,
    decimal UnitCost, MoneyCurrency Currency, TaxTreatment TaxTreatment, CostSource Source, int PackQuantity,
    string? SupplierCode, string? Notes, DateTime ObservedAtUtc, Guid? RecordedByUserId, DateTime RecordedAtUtc);

public sealed record AddSupplierCostObservationRequest
{
    public Guid SupplierId { get; init; }
    public decimal UnitCost { get; init; }
    public MoneyCurrency Currency { get; init; }
    public TaxTreatment TaxTreatment { get; init; }
    public CostSource Source { get; init; }
    public int PackQuantity { get; init; } = 1;
    public string? SupplierCode { get; init; }
    public string? Notes { get; init; }
    public DateTime ObservedAtUtc { get; init; }
}
