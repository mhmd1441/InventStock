using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Stock;

public sealed record StockHoldRequest(Guid OperationId, int Quantity, string Reason, bool PhysicalSeparationConfirmed);
public sealed record StockReleaseRequest(Guid OperationId, int Quantity, string Reason);
public sealed record StockHoldMovementDto(Guid Id, Guid OperationId, Guid PartId, StockHoldAction Action,
    int QuantityChange, string Reason, bool PhysicalSeparationConfirmed, Guid PerformedByUserId, DateTime OccurredAtUtc);
