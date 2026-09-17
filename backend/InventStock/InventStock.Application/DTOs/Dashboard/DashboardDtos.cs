using InventStock.Application.DTOs.Catalog;
using InventStock.Application.DTOs.Stock;

namespace InventStock.Application.DTOs.Dashboard;

public sealed record DashboardSummaryDto
{
    public int TotalParts { get; init; }

    public int ActiveParts { get; init; }

    public int LowStockCount { get; init; }

    public int TotalCategories { get; init; }

    public int TotalSuppliers { get; init; }

    public int TotalStockUnits { get; init; }

    public decimal TotalStockValue { get; init; }

    public IReadOnlyList<StockMovementDto> RecentMovements { get; init; } = Array.Empty<StockMovementDto>();

    public IReadOnlyList<PartDto> LowStockParts { get; init; } = Array.Empty<PartDto>();
}
