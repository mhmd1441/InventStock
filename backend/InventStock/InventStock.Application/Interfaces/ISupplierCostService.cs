using InventStock.Application.DTOs.Catalog;

namespace InventStock.Application.Interfaces;

public interface ISupplierCostService
{
    Task<IReadOnlyList<SupplierCostObservationDto>> ListAsync(Guid partId, CancellationToken cancellationToken = default);
    Task<SupplierCostObservationDto> AddAsync(Guid partId, AddSupplierCostObservationRequest request, CancellationToken cancellationToken = default);
}
