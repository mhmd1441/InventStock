using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Application.Common.Exceptions;
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

    public async Task<BrandMergeDto> MergeAsync(Guid sourceId, Guid targetId, string reason, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (db.Database.IsNpgsql())
        {
            var ordered = new[] { sourceId, targetId }.OrderBy(x => x).ToArray();
            foreach (var id in ordered)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"ProductBrands\" WHERE \"Id\" = {id} FOR UPDATE", cancellationToken);
        }

        var source = await db.ProductBrands.Include(x => x.Aliases).Include(x => x.Parts)
            .SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken) ?? throw new NotFoundException("Brand", sourceId);
        var target = await db.ProductBrands.Include(x => x.Aliases).Include(x => x.Parts)
            .SingleOrDefaultAsync(x => x.Id == targetId, cancellationToken) ?? throw new NotFoundException("Brand", targetId);
        if (!source.IsActive || !target.IsActive) throw new ConflictException("Both brands must be active before merging.");

        var targetKeys = target.Parts.Select(x => (x.PartNumber, x.Variant)).ToHashSet();
        var collisions = source.Parts.Where(x => targetKeys.Contains((x.PartNumber, x.Variant)))
            .Select(x => $"{x.PartNumber}{(x.Variant.Length > 0 ? $" ({x.Variant})" : "")}").Take(10).ToList();
        if (collisions.Count > 0)
            throw new ConflictException($"Merge would create duplicate SKUs: {string.Join(", ", collisions)}. Resolve those products first.");

        var targetNames = target.Aliases.Select(x => x.NormalizedValue).Append(target.NormalizedName).ToHashSet();
        foreach (var alias in source.Aliases.Where(x => !targetNames.Contains(x.NormalizedValue))) alias.Reassign(target.Id);
        if (!targetNames.Contains(source.NormalizedName)) await db.BrandAliases.AddAsync(BrandAlias.Create(target.Id, source.Name), cancellationToken);
        foreach (var part in source.Parts) part.SetBrand(target);
        source.Deactivate();
        var merge = BrandMergeEvent.Create(source, target, reason, source.Parts.Count);
        await db.BrandMergeEvents.AddAsync(merge, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BrandMergeDto(merge.Id, source.Id, target.Id, merge.SourceName, merge.TargetName,
            merge.Reason, merge.ReassignedPartCount, merge.CreatedByUserId, merge.CreatedAtUtc);
    }

    private static IQueryable<BrandDto> Project(IQueryable<ProductBrand> source) => source.Select(x =>
        new BrandDto(x.Id, x.Name, x.IsActive, x.Aliases.OrderBy(a => a.Value).Select(a => a.Value).ToList()));
}
