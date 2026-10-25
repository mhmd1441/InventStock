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

        var countedParts = await _db.Parts.CountAsync(p => !p.NeedsRecount &&
            _db.StockMovements.Any(m => m.PartId == p.Id && m.CountedQuantity != null), cancellationToken);
        var notCountedParts = totalParts - countedParts;

        // Centimetres and pieces cannot be meaningfully added together. Keep this legacy field to
        // counted piece SKUs only; the UI emphasizes counted/not-counted product totals instead.
        var totalStockUnits = await _db.Parts
            .Where(p => !p.NeedsRecount && p.StockUnit == Domain.Enums.StockUnit.Piece &&
                        _db.StockMovements.Any(m => m.PartId == p.Id && m.CountedQuantity != null))
            .SumAsync(p => _db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0, cancellationToken);

        var totalStockValue = includePrivate ? await _db.Parts
            .Where(p => p.IsActive && !p.NeedsRecount && _db.StockMovements.Any(m => m.PartId == p.Id && m.CountedQuantity != null))
            .SumAsync(
                p => (decimal?)((_db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0)
                                * p.UnitPrice),
                cancellationToken) ?? 0m : 0m;

        var lowStockCount = await _db.Parts
            .CountAsync(
                p => p.IsActive &&
                     (p.NeedsRecount || _db.StockMovements.Any(m => m.PartId == p.Id && m.CountedQuantity != null)) &&
                     ((_db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0) -
                      (_db.StockHoldMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0))
                     <= p.LowStockThreshold,
                cancellationToken);

        return new InventoryTotals(
            totalParts,
            activeParts,
            totalCategories,
            totalSuppliers,
            totalStockUnits,
            totalStockValue,
            lowStockCount,
            countedParts,
            notCountedParts);
    }

    public Task<DateTime?> GetLastCashCloseAtUtcAsync(CancellationToken cancellationToken = default) =>
        _db.CashReconciliations.AsNoTracking().MaxAsync(x => (DateTime?)x.ClosedAtUtc, cancellationToken);
}
