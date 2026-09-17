using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _db;

    public SupplierRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var source = _db.Suppliers.AsNoTracking();
        if (!includeInactive)
        {
            source = source.Where(x => x.IsActive);
        }

        return await Project(source).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<SupplierDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Project(_db.Suppliers.AsNoTracking().Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Suppliers.AnyAsync(x => x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return _db.Suppliers.AnyAsync(
            x => x.Name.ToLower() == normalized && (excludeId == null || x.Id != excludeId),
            cancellationToken);
    }

    public Task<bool> HasPartsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Parts.AnyAsync(x => x.SupplierId == id, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) =>
        await _db.Suppliers.AddAsync(supplier, cancellationToken);

    private IQueryable<SupplierDto> Project(IQueryable<Supplier> source) =>
        source.Select(s => new SupplierDto
        {
            Id = s.Id,
            Name = s.Name,
            ContactName = s.ContactName,
            Email = s.Email,
            Phone = s.Phone,
            Address = s.Address,
            IsActive = s.IsActive,
            PartCount = _db.Parts.Count(p => p.SupplierId == s.Id)
        });
}
