using InventStock.Application.DTOs.Dashboard;
using InventStock.Application.Interfaces;
using InventStock.Application.Interfaces.Repositories;

namespace InventStock.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private const int RecentMovementCount = 10;
    private const int LowStockPreviewCount = 10;

    private readonly IStatisticsRepository _statistics;
    private readonly IStockMovementRepository _movements;
    private readonly IPartRepository _parts;
    private readonly ITransactionExceptionService _exceptions;

    public DashboardService(
        IStatisticsRepository statistics,
        IStockMovementRepository movements,
        IPartRepository parts,
        ITransactionExceptionService exceptions)
    {
        _statistics = statistics;
        _movements = movements;
        _parts = parts;
        _exceptions = exceptions;
    }

    public async Task<CounterDashboardDto> GetCounterSummaryAsync(CancellationToken cancellationToken = default)
    {
        var totals = await _statistics.GetInventoryTotalsAsync(cancellationToken, includePrivate: false);
        return new CounterDashboardDto
        {
            TotalParts = totals.TotalParts,
            ActiveParts = totals.ActiveParts,
            LowStockCount = totals.LowStockCount,
            TotalCategories = totals.TotalCategories,
            TotalStockUnits = totals.TotalStockUnits,
            CountedParts = totals.CountedParts,
            NotCountedParts = totals.NotCountedParts,
            RecentMovements = await _movements.GetRecentAsync(RecentMovementCount, cancellationToken),
            LowStockParts = await _parts.GetCounterLowStockAsync(LowStockPreviewCount, cancellationToken)
        };
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var totals = await _statistics.GetInventoryTotalsAsync(cancellationToken);
        var recentMovements = await _movements.GetRecentAsync(RecentMovementCount, cancellationToken);
        var lowStockParts = await _parts.GetLowStockAsync(LowStockPreviewCount, cancellationToken);
        var lastClose = await _statistics.GetLastCashCloseAtUtcAsync(cancellationToken);
        var exceptionFrom = lastClose ?? DateTime.UnixEpoch;
        var exceptionCount = await _exceptions.CountSinceAsync(exceptionFrom, cancellationToken);

        return new DashboardSummaryDto
        {
            TotalParts = totals.TotalParts,
            ActiveParts = totals.ActiveParts,
            LowStockCount = totals.LowStockCount,
            TotalCategories = totals.TotalCategories,
            TotalSuppliers = totals.TotalSuppliers,
            TotalStockUnits = totals.TotalStockUnits,
            CountedParts = totals.CountedParts,
            NotCountedParts = totals.NotCountedParts,
            TotalStockValue = totals.TotalStockValue,
            UnreviewedExceptionCount = exceptionCount,
            ExceptionCountSinceUtc = lastClose,
            RecentMovements = recentMovements,
            LowStockParts = lowStockParts
        };
    }
}
