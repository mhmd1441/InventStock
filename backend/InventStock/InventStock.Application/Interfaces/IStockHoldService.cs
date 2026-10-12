using InventStock.Application.DTOs.Stock;

namespace InventStock.Application.Interfaces;

public interface IStockHoldService
{
    Task<StockHoldMovementDto> HoldAsync(Guid partId, StockHoldRequest request, CancellationToken cancellationToken = default);
    Task<StockHoldMovementDto> ReleaseAsync(Guid partId, StockReleaseRequest request, CancellationToken cancellationToken = default);
    Task<StockHoldMovementDto> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
}
