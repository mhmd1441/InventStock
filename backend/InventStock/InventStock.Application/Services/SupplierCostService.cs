using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;

namespace InventStock.Application.Services;

public sealed class SupplierCostService(IPartRepository parts, ISupplierRepository suppliers,
    ISupplierCostRepository costs, IUnitOfWork unitOfWork) : ISupplierCostService
{
    public async Task<IReadOnlyList<SupplierCostObservationDto>> ListAsync(Guid partId, CancellationToken cancellationToken = default)
    {
        _ = await parts.GetByIdAsync(partId, cancellationToken) ?? throw new NotFoundException("Part", partId);
        return await costs.ListAsync(partId, cancellationToken);
    }

    public async Task<SupplierCostObservationDto> AddAsync(Guid partId, AddSupplierCostObservationRequest request, CancellationToken cancellationToken = default)
    {
        _ = await parts.GetByIdAsync(partId, cancellationToken) ?? throw new NotFoundException("Part", partId);
        if (!await suppliers.ExistsAsync(request.SupplierId, cancellationToken)) throw new NotFoundException("Supplier", request.SupplierId);
        var observedAt = request.ObservedAtUtc == default ? DateTime.UtcNow : request.ObservedAtUtc;
        var observation = SupplierCostObservation.Create(partId, request.SupplierId, request.UnitCost,
            request.Currency, request.TaxTreatment, request.Source, request.PackQuantity,
            request.SupplierCode, request.Notes, observedAt);
        await costs.AddAsync(observation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await costs.ListAsync(partId, cancellationToken)).Single(x => x.Id == observation.Id);
    }
}
