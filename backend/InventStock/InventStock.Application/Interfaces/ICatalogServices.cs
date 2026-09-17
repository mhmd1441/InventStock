using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Catalog;

namespace InventStock.Application.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CategoryDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<SupplierDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default);

    Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public interface IPartService
{
    Task<PagedResult<PartDto>> SearchAsync(PartQuery query, CancellationToken cancellationToken = default);

    Task<PartDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PartDto> GetByPartNumberAsync(string partNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartDto>> GetLowStockAsync(int limit, CancellationToken cancellationToken = default);

    Task<PartDto> CreateAsync(SavePartRequest request, CancellationToken cancellationToken = default);

    Task<PartDto> UpdateAsync(Guid id, SavePartRequest request, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
