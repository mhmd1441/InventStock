using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;

namespace InventStock.Application.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default) =>
        _categories.ListAsync(cancellationToken);

    public async Task<CategoryDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _categories.GetDtoByIdAsync(id, cancellationToken)
               ?? throw new NotFoundException("Category", id);
    }

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNameIsFreeAsync(request.Name, null, cancellationToken);

        var category = Category.Create(request.Name, request.Description);
        await _categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(category.Id, cancellationToken);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Category", id);

        await EnsureNameIsFreeAsync(request.Name, id, cancellationToken);

        category.Update(request.Name, request.Description);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Category", id);

        if (await _categories.HasPartsAsync(id, cancellationToken))
        {
            throw new ConflictException("This category still has parts assigned to it and cannot be deleted.");
        }

        _categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        if (await _categories.NameExistsAsync(trimmed, excludeId, cancellationToken))
        {
            throw new ConflictException($"A category named '{trimmed}' already exists.");
        }
    }
}
