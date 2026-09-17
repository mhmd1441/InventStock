using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;

namespace InventStock.Application.Interfaces.Repositories;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<SupplierDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task<bool> HasPartsAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
}
