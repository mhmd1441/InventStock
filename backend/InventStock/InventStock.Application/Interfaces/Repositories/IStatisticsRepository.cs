namespace InventStock.Application.Interfaces.Repositories;

public sealed record InventoryTotals(
    int TotalParts,
    int ActiveParts,
    int TotalCategories,
    int TotalSuppliers,
    int TotalStockUnits,
    decimal TotalStockValue,
    int LowStockCount,
    int CountedParts,
    int NotCountedParts);

public interface IStatisticsRepository
{
    Task<InventoryTotals> GetInventoryTotalsAsync(CancellationToken cancellationToken = default, bool includePrivate = true);
}
