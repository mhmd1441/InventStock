using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Stock;
using InventStock.Domain.Entities;

namespace InventStock.Application.Interfaces.Repositories;

public interface IStockMovementRepository
{
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default);

    Task<PagedResult<StockMovementDto>> SearchAsync(StockMovementQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovementDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
