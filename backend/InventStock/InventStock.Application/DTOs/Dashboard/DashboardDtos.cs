using InventStock.Application.DTOs.Catalog;
using InventStock.Application.DTOs.Stock;

namespace InventStock.Application.DTOs.Dashboard;

public sealed record CounterDashboardDto
{
    public int TotalParts { get; init; }
    public int ActiveParts { get; init; }
    public int LowStockCount { get; init; }
    public int TotalCategories { get; init; }
    public int TotalStockUnits { get; init; }
    public int CountedParts { get; init; }
    public int NotCountedParts { get; init; }
    public IReadOnlyList<StockMovementDto> RecentMovements { get; init; } = Array.Empty<StockMovementDto>();
    public IReadOnlyList<CounterPartDto> LowStockParts { get; init; } = Array.Empty<CounterPartDto>();
}

public sealed record DashboardSummaryDto
{
    public int TotalParts { get; init; }

    public int ActiveParts { get; init; }

    public int LowStockCount { get; init; }

    public int TotalCategories { get; init; }

    public int TotalSuppliers { get; init; }

    public int TotalStockUnits { get; init; }
    public int CountedParts { get; init; }
    public int NotCountedParts { get; init; }

    public decimal TotalStockValue { get; init; }

    public int UnreviewedExceptionCount { get; init; }

    public DateTime? ExceptionCountSinceUtc { get; init; }

    public IReadOnlyList<StockMovementDto> RecentMovements { get; init; } = Array.Empty<StockMovementDto>();

    public IReadOnlyList<PartDto> LowStockParts { get; init; } = Array.Empty<PartDto>();
}
