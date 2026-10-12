using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Receiving;

public sealed record PostPurchaseReceiptRequest
{
    public Guid OperationId { get; init; }
    public Guid SupplierId { get; init; }
    public string? SupplierReference { get; init; }
    public string? Notes { get; init; }
    public DateTime ReceivedAtUtc { get; init; }
    public IReadOnlyList<PostPurchaseReceiptLineRequest> Lines { get; init; } = [];
}

public sealed record PostPurchaseReceiptLineRequest
{
    public Guid PartId { get; init; }
    public int PackageCount { get; init; } = 1;
    public int BaseUnitsPerPackage { get; init; } = 1;
    public decimal? CostPerPackage { get; init; }
    public MoneyCurrency? Currency { get; init; }
    public TaxTreatment TaxTreatment { get; init; } = TaxTreatment.Unknown;
    public string? SupplierCode { get; init; }
    public string? Notes { get; init; }
}

public sealed record PurchaseReceiptDto(Guid Id, Guid OperationId, Guid SupplierId, string SupplierName,
    string? SupplierReference, string? Notes, DateTime ReceivedAtUtc, Guid ReceivedByUserId,
    DateTime RecordedAtUtc, IReadOnlyList<PurchaseReceiptLineDto> Lines);

public sealed record PurchaseReceiptLineDto(Guid Id, Guid PartId, string PartNumber, string PartName,
    StockUnit StockUnit, int PackageCount, int BaseUnitsPerPackage, int BaseQuantity,
    decimal? CostPerPackage, MoneyCurrency? Currency, TaxTreatment TaxTreatment,
    string? SupplierCode, string? Notes, Guid StockMovementId, Guid? SupplierCostObservationId);

public sealed record ReceivingOperationStatusDto(Guid OperationId, bool IsCommitted, PurchaseReceiptDto Receipt);
