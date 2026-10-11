using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;

namespace InventStock.Application.Interfaces.Repositories;

public interface IBrandRepository
{
    Task<IReadOnlyList<BrandDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<ProductBrand?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameOrAliasExistsAsync(string normalized, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrandDto>> FindCandidatesAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(ProductBrand brand, CancellationToken cancellationToken = default);
    Task AddAliasAsync(BrandAlias alias, CancellationToken cancellationToken = default);
}
