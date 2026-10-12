using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;

namespace InventStock.Application.Interfaces.Repositories;

public interface ISupplierCostRepository
{
    Task<IReadOnlyList<SupplierCostObservationDto>> ListAsync(Guid partId, CancellationToken cancellationToken = default);
    Task AddAsync(SupplierCostObservation observation, CancellationToken cancellationToken = default);
}
