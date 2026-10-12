using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Repositories;

public sealed class SupplierCostRepository(AppDbContext db) : ISupplierCostRepository
{
    public async Task<IReadOnlyList<SupplierCostObservationDto>> ListAsync(Guid partId, CancellationToken cancellationToken = default) =>
        await db.SupplierCostObservations.AsNoTracking().Where(x => x.PartId == partId)
            .OrderByDescending(x => x.ObservedAtUtc).ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new SupplierCostObservationDto(x.Id, x.PartId, x.SupplierId, x.Supplier.Name,
                x.UnitCost, x.Currency, x.TaxTreatment, x.Source, x.PackQuantity, x.SupplierCode,
                x.Notes, x.ObservedAtUtc, x.CreatedByUserId, x.CreatedAtUtc)).ToListAsync(cancellationToken);

    public async Task AddAsync(SupplierCostObservation observation, CancellationToken cancellationToken = default) =>
        await db.SupplierCostObservations.AddAsync(observation, cancellationToken);
}
