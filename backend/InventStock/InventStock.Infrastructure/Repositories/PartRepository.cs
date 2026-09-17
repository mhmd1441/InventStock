using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Repositories;

public sealed class PartRepository : IPartRepository
{
    private readonly AppDbContext _db;

    public PartRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Part?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Parts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PartDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Project(_db.Parts.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public Task<PartDto?> GetDtoByPartNumberAsync(string partNumber, CancellationToken cancellationToken = default) =>
        Project(_db.Parts.AsNoTracking().Where(x => x.PartNumber == partNumber))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<PartDto>> SearchAsync(PartQuery query, CancellationToken cancellationToken = default)
    {
        var source = _db.Parts.AsNoTracking();

        if (!query.IncludeInactive)
        {
            source = source.Where(p => p.IsActive);
        }

        if (query.CategoryId.HasValue)
        {
            source = source.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.SupplierId.HasValue)
        {
            source = source.Where(p => p.SupplierId == query.SupplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            source = source.Where(p =>
                p.PartNumber.ToLower().Contains(term) ||
                p.Name.ToLower().Contains(term) ||
                (p.Barcode != null && p.Barcode.ToLower() == term));
        }

        if (query.LowStockOnly)
        {
            source = source.Where(p =>
                (_db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0)
                <= p.LowStockThreshold);
        }

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await Sort(Project(source), query)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PartDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<PartDto>> GetLowStockAsync(int limit, CancellationToken cancellationToken = default)
    {
        // Filtering and ordering happen on the entity with the aggregate spelled out, rather than on
        // the projected DTO, so the whole query is a single statement on every provider.
        var source = _db.Parts
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Where(p =>
                (_db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0)
                <= p.LowStockThreshold)
            .OrderBy(p =>
                (_db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0)
                - p.LowStockThreshold)
            .ThenBy(p => p.PartNumber)
            .Take(limit);

        return await Project(source).ToListAsync(cancellationToken);
    }

    public Task<bool> PartNumberExistsAsync(string partNumber, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = partNumber.Trim().ToUpper();
        return _db.Parts.AnyAsync(
            x => x.PartNumber == normalized && (excludeId == null || x.Id != excludeId),
            cancellationToken);
    }

    public Task<bool> BarcodeExistsAsync(string barcode, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = barcode.Trim();
        return _db.Parts.AnyAsync(
            x => x.Barcode == normalized && (excludeId == null || x.Id != excludeId),
            cancellationToken);
    }

    public async Task<int> GetQuantityOnHandAsync(Guid partId, CancellationToken cancellationToken = default) =>
        await _db.StockMovements
            .Where(m => m.PartId == partId)
            .SumAsync(m => (int?)m.QuantityChange, cancellationToken) ?? 0;

    public async Task LockForStockChangeAsync(Guid partId, CancellationToken cancellationToken = default)
    {
        // SQLite (used by the test suite) serialises writes already and has no row-level locking.
        if (!_db.Database.IsNpgsql())
        {
            return;
        }

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {partId} FOR UPDATE",
            cancellationToken);
    }

    public async Task AddAsync(Part part, CancellationToken cancellationToken = default) =>
        await _db.Parts.AddAsync(part, cancellationToken);

    private IQueryable<PartDto> Project(IQueryable<Part> source) =>
        source.Select(p => new PartDto
        {
            Id = p.Id,
            PartNumber = p.PartNumber,
            Name = p.Name,
            Description = p.Description,
            CategoryId = p.CategoryId,
            CategoryName = p.Category.Name,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier != null ? p.Supplier.Name : null,
            UnitPrice = p.UnitPrice,
            LowStockThreshold = p.LowStockThreshold,
            Barcode = p.Barcode,
            IsActive = p.IsActive,
            CreatedAtUtc = p.CreatedAtUtc,
            QuantityOnHand = _db.StockMovements
                .Where(m => m.PartId == p.Id)
                .Sum(m => (int?)m.QuantityChange) ?? 0
        });

    private static IQueryable<PartDto> Sort(IQueryable<PartDto> source, PartQuery query) =>
        (query.SortBy, query.SortDescending) switch
        {
            (PartSortField.Name, false) => source.OrderBy(p => p.Name),
            (PartSortField.Name, true) => source.OrderByDescending(p => p.Name),
            (PartSortField.UnitPrice, false) => source.OrderBy(p => p.UnitPrice),
            (PartSortField.UnitPrice, true) => source.OrderByDescending(p => p.UnitPrice),
            (PartSortField.QuantityOnHand, false) => source.OrderBy(p => p.QuantityOnHand),
            (PartSortField.QuantityOnHand, true) => source.OrderByDescending(p => p.QuantityOnHand),
            (PartSortField.CreatedAtUtc, true) => source.OrderByDescending(p => p.CreatedAtUtc),
            (PartSortField.CreatedAtUtc, false) => source.OrderBy(p => p.CreatedAtUtc),
            (_, true) => source.OrderByDescending(p => p.PartNumber),
            _ => source.OrderBy(p => p.PartNumber)
        };
}
