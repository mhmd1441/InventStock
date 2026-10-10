using InventStock.Application.DTOs.Dashboard;

namespace InventStock.Application.Interfaces;

public interface IDashboardService
{
    Task<CounterDashboardDto> GetCounterSummaryAsync(CancellationToken cancellationToken = default);

    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
