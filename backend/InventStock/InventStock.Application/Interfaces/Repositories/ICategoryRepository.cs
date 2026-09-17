using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;

namespace InventStock.Application.Interfaces.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CategoryDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task<bool> HasPartsAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    void Remove(Category category);
}
