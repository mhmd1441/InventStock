using InventStock.Application.DTOs.Dashboard;

namespace InventStock.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
