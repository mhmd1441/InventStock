using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;

namespace InventStock.Application.Services;

public sealed class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public SupplierService(ISupplierRepository suppliers, IUnitOfWork unitOfWork)
    {
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        _suppliers.ListAsync(includeInactive, cancellationToken);

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _suppliers.GetDtoByIdAsync(id, cancellationToken)
               ?? throw new NotFoundException("Supplier", id);
    }

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNameIsFreeAsync(request.Name, null, cancellationToken);

        var supplier = Supplier.Create(request.Name, request.ContactName, request.Email, request.Phone, request.Address);
        await _suppliers.AddAsync(supplier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(supplier.Id, cancellationToken);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Supplier", id);

        await EnsureNameIsFreeAsync(request.Name, id, cancellationToken);

        supplier.Update(request.Name, request.ContactName, request.Email, request.Phone, request.Address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Supplier", id);

        if (isActive)
        {
            supplier.Reactivate();
        }
        else
        {
            supplier.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        if (await _suppliers.NameExistsAsync(trimmed, excludeId, cancellationToken))
        {
            throw new ConflictException($"A supplier named '{trimmed}' already exists.");
        }
    }
}
