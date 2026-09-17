using InventStock.Application.Common.Models;
using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Stock;

public sealed record StockMovementDto
{
    public Guid Id { get; init; }

    public Guid PartId { get; init; }

    public string PartNumber { get; init; } = string.Empty;

    public string PartName { get; init; } = string.Empty;

    public MovementType MovementType { get; init; }

    public int QuantityChange { get; init; }

    public string? Reason { get; init; }

    public string? Reference { get; init; }

    public Guid PerformedByUserId { get; init; }

    public string PerformedByUserName { get; init; } = string.Empty;

    public DateTime OccurredAtUtc { get; init; }
}

public sealed record StockInRequest
{
    public int Quantity { get; init; }

    public string? Reason { get; init; }

    public string? Reference { get; init; }
}

public sealed record StockOutRequest
{
    public int Quantity { get; init; }

    public string? Reason { get; init; }

    public string? Reference { get; init; }
}

public sealed record StockAdjustmentRequest
{
    public int QuantityChange { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string? Reference { get; init; }
}

public sealed class StockMovementQuery : PagedQuery
{
    public Guid? PartId { get; set; }

    public MovementType? MovementType { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

public sealed record StockChangedNotification
{
    public Guid PartId { get; init; }

    public string PartNumber { get; init; } = string.Empty;

    public string PartName { get; init; } = string.Empty;

    public int QuantityOnHand { get; init; }

    public int LowStockThreshold { get; init; }

    public bool IsLowStock { get; init; }

    public MovementType MovementType { get; init; }

    public int QuantityChange { get; init; }

    public string PerformedByUserName { get; init; } = string.Empty;

    public DateTime OccurredAtUtc { get; init; }
}
