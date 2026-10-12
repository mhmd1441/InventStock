using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;

namespace InventStock.Application.Services;

public sealed class BrandService(IBrandRepository brands, IUnitOfWork unitOfWork) : IBrandService
{
    public Task<IReadOnlyList<BrandDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        brands.ListAsync(includeInactive, cancellationToken);

    public Task<IReadOnlyList<BrandDto>> FindCandidatesAsync(string name, CancellationToken cancellationToken = default) =>
        brands.FindCandidatesAsync(name, cancellationToken);

    public async Task<BrandDto> CreateAsync(SaveBrandRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = ProductBrand.Normalize(request.Name);
        if (await brands.NameOrAliasExistsAsync(normalized, cancellationToken))
            throw new ConflictException($"Brand '{request.Name.Trim()}' already exists as a brand or alias.");

        var candidates = await brands.FindCandidatesAsync(request.Name, cancellationToken);
        if (candidates.Count > 0 && !request.ConfirmPossibleDuplicate)
            throw new ConflictException($"Possible duplicate brand: {string.Join(", ", candidates.Select(x => x.Name))}. Review it, then explicitly confirm if this is genuinely different.");

        var brand = ProductBrand.Create(request.Name);
        await brands.AddAsync(brand, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new BrandDto(brand.Id, brand.Name, brand.IsActive, []);
    }

    public async Task<BrandDto> AddAliasAsync(Guid id, SaveBrandAliasRequest request, CancellationToken cancellationToken = default)
    {
        if (id == ProductBrand.UnknownId) throw new ConflictException("The system Unknown brand cannot have aliases.");
        var brand = await brands.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Brand", id);
        var normalized = ProductBrand.Normalize(request.Value);
        if (normalized == brand.NormalizedName || await brands.NameOrAliasExistsAsync(normalized, cancellationToken))
            throw new ConflictException($"Brand name or alias '{request.Value.Trim()}' already exists.");
        await brands.AddAliasAsync(BrandAlias.Create(id, request.Value), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await brands.ListAsync(true, cancellationToken)).Single(x => x.Id == id);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        if (id == ProductBrand.UnknownId) throw new ConflictException("The system Unknown brand cannot be deactivated.");
        var brand = await brands.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Brand", id);
        if (isActive) brand.Reactivate(); else brand.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<BrandMergeDto> MergeAsync(Guid sourceId, MergeBrandRequest request, CancellationToken cancellationToken = default)
    {
        if (sourceId == ProductBrand.UnknownId || request.TargetBrandId == ProductBrand.UnknownId)
            throw new ConflictException("The system Unknown brand cannot be merged.");
        if (sourceId == request.TargetBrandId) throw new ConflictException("Choose two different brands.");
        return brands.MergeAsync(sourceId, request.TargetBrandId, request.Reason, cancellationToken);
    }
}
