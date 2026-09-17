using InventStock.Application.Common.Exceptions;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;

namespace InventStock.Application.Services;

public sealed class PartService : IPartService
{
    private readonly IPartRepository _parts;
    private readonly ICategoryRepository _categories;
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public PartService(
        IPartRepository parts,
        ICategoryRepository categories,
        ISupplierRepository suppliers,
        IUnitOfWork unitOfWork)
    {
        _parts = parts;
        _categories = categories;
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<PartDto>> SearchAsync(PartQuery query, CancellationToken cancellationToken = default) =>
        _parts.SearchAsync(query, cancellationToken);

    public async Task<PartDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _parts.GetDtoByIdAsync(id, cancellationToken)
               ?? throw new NotFoundException("Part", id);
    }

    public async Task<PartDto> GetByPartNumberAsync(string partNumber, CancellationToken cancellationToken = default)
    {
        var normalized = Part.NormalizePartNumber(partNumber);
        return await _parts.GetDtoByPartNumberAsync(normalized, cancellationToken)
               ?? throw new NotFoundException($"Part with number '{normalized}' was not found.");
    }

    public Task<IReadOnlyList<PartDto>> GetLowStockAsync(int limit, CancellationToken cancellationToken = default) =>
        _parts.GetLowStockAsync(limit <= 0 ? 10 : limit, cancellationToken);

    public async Task<PartDto> CreateAsync(SavePartRequest request, CancellationToken cancellationToken = default)
    {
        var partNumber = Part.NormalizePartNumber(request.PartNumber);
        await EnsurePartNumberIsFreeAsync(partNumber, null, cancellationToken);
        await EnsureBarcodeIsFreeAsync(request.Barcode, null, cancellationToken);
        await EnsureRelationsExistAsync(request.CategoryId, request.SupplierId, cancellationToken);

        var part = Part.Create(
            partNumber,
            request.Name,
            request.Description,
            request.CategoryId,
            request.SupplierId,
            request.UnitPrice,
            request.LowStockThreshold,
            request.Barcode);

        await _parts.AddAsync(part, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(part.Id, cancellationToken);
    }

    public async Task<PartDto> UpdateAsync(Guid id, SavePartRequest request, CancellationToken cancellationToken = default)
    {
        var part = await _parts.GetByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException("Part", id);

        var partNumber = Part.NormalizePartNumber(request.PartNumber);
        await EnsurePartNumberIsFreeAsync(partNumber, id, cancellationToken);
        await EnsureBarcodeIsFreeAsync(request.Barcode, id, cancellationToken);
        await EnsureRelationsExistAsync(request.CategoryId, request.SupplierId, cancellationToken);

        part.Update(
            partNumber,
            request.Name,
            request.Description,
            request.CategoryId,
            request.SupplierId,
            request.UnitPrice,
            request.LowStockThreshold,
            request.Barcode);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var part = await _parts.GetByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException("Part", id);

        if (isActive)
        {
            part.Reactivate();
        }
        else
        {
            part.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePartNumberIsFreeAsync(string partNumber, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await _parts.PartNumberExistsAsync(partNumber, excludeId, cancellationToken))
        {
            throw new ConflictException($"Part number '{partNumber}' is already in use.");
        }
    }

    private async Task EnsureBarcodeIsFreeAsync(string? barcode, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return;
        }

        if (await _parts.BarcodeExistsAsync(barcode.Trim(), excludeId, cancellationToken))
        {
            throw new ConflictException($"Barcode '{barcode.Trim()}' is already assigned to another part.");
        }
    }

    private async Task EnsureRelationsExistAsync(Guid categoryId, Guid? supplierId, CancellationToken cancellationToken)
    {
        if (!await _categories.ExistsAsync(categoryId, cancellationToken))
        {
            throw new NotFoundException("Category", categoryId);
        }

        if (supplierId.HasValue && !await _suppliers.ExistsAsync(supplierId.Value, cancellationToken))
        {
            throw new NotFoundException("Supplier", supplierId.Value);
        }
    }
}
