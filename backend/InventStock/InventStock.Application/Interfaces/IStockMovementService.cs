using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Stock;

namespace InventStock.Application.Interfaces;

public interface IStockMovementService
{
    Task<StockMovementDto> StockInAsync(Guid partId, StockInRequest request, CancellationToken cancellationToken = default);

    Task<StockMovementDto> StockOutAsync(Guid partId, StockOutRequest request, CancellationToken cancellationToken = default);

    Task<StockMovementDto> AdjustAsync(Guid partId, StockAdjustmentRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<StockMovementDto>> SearchAsync(StockMovementQuery query, CancellationToken cancellationToken = default);
}
