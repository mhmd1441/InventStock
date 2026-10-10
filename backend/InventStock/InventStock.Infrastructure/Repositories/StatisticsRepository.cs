using InventStock.Application.Interfaces.Repositories;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Repositories;

public sealed class StatisticsRepository : IStatisticsRepository
{
    private readonly AppDbContext _db;

    public StatisticsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<InventoryTotals> GetInventoryTotalsAsync(CancellationToken cancellationToken = default, bool includePrivate = true)
    {
        var totalParts = await _db.Parts.CountAsync(cancellationToken);
        var activeParts = await _db.Parts.CountAsync(p => p.IsActive, cancellationToken);
        var totalCategories = await _db.Categories.CountAsync(cancellationToken);
        var totalSuppliers = includePrivate ? await _db.Suppliers.CountAsync(cancellationToken) : 0;

        var totalStockUnits = await _db.StockMovements
            .SumAsync(m => (int?)m.QuantityChange, cancellationToken) ?? 0;

        var totalStockValue = includePrivate ? await _db.Parts
            .Where(p => p.IsActive)
            .SumAsync(
                p => (decimal?)((_db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0)
                                * p.UnitPrice),
                cancellationToken) ?? 0m : 0m;

        var lowStockCount = await _db.Parts
            .CountAsync(
                p => p.IsActive &&
                     (_db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0)
                     <= p.LowStockThreshold,
                cancellationToken);

        return new InventoryTotals(
            totalParts,
            activeParts,
            totalCategories,
            totalSuppliers,
            totalStockUnits,
            totalStockValue,
            lowStockCount);
    }
}
