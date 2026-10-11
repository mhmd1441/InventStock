using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Repositories;

public sealed class BrandRepository(AppDbContext db) : IBrandRepository
{
    public async Task<IReadOnlyList<BrandDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        await Project(db.ProductBrands.Where(x => includeInactive || x.IsActive).OrderBy(x => x.Name))
            .ToListAsync(cancellationToken);

    public Task<ProductBrand?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ProductBrands.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> NameOrAliasExistsAsync(string normalized, CancellationToken cancellationToken = default)
    {
        if (await db.ProductBrands.AnyAsync(x => x.NormalizedName == normalized, cancellationToken)) return true;
        return await db.BrandAliases.AnyAsync(x => x.NormalizedValue == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<BrandDto>> FindCandidatesAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = ProductBrand.Normalize(name);
        var key = ProductBrand.CandidateKey(name);
        var matches = db.ProductBrands.Where(x =>
                x.NormalizedName == normalized ||
                x.NormalizedName.Replace(" ", "").Contains(key) ||
                db.BrandAliases.Any(a => a.ProductBrandId == x.Id &&
                    (a.NormalizedValue == normalized || a.NormalizedValue.Replace(" ", "").Contains(key))));
        return await Project(matches.OrderBy(x => x.Name).Take(10)).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ProductBrand brand, CancellationToken cancellationToken = default) =>
        await db.ProductBrands.AddAsync(brand, cancellationToken);

    public async Task AddAliasAsync(BrandAlias alias, CancellationToken cancellationToken = default) =>
        await db.BrandAliases.AddAsync(alias, cancellationToken);

    private static IQueryable<BrandDto> Project(IQueryable<ProductBrand> source) => source.Select(x =>
        new BrandDto(x.Id, x.Name, x.IsActive, x.Aliases.OrderBy(a => a.Value).Select(a => a.Value).ToList()));
}
