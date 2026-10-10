using InventStock.Application.Common.Exceptions;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Repositories;

public sealed class PartRepository(AppDbContext db) : IPartRepository
{
    public Task<Part?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Parts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PartDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ProjectAdmin(db.Parts.Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<PartDto?> GetDtoByPartNumberAsync(string partNumber, CancellationToken cancellationToken = default) =>
        Unique(await ProjectAdmin(db.Parts.Where(x => x.PartNumber == partNumber)).Take(2).ToListAsync(cancellationToken));

    public Task<CounterPartDto?> GetCounterAsync(Guid id, CancellationToken cancellationToken = default) =>
        ProjectCounter(db.Parts.Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<CounterPartDto?> GetCounterByNumberAsync(string number, CancellationToken cancellationToken = default) =>
        Unique(await ProjectCounter(db.Parts.Where(x => x.PartNumber == number)).Take(2).ToListAsync(cancellationToken));

    private static T? Unique<T>(List<T> matches) where T : class => matches.Count > 1
        ? throw new ConflictException("Multiple brands or variants use this number. Search and select a specific part.")
        : matches.FirstOrDefault();

    public async Task<PagedResult<PartDto>> SearchAsync(PartQuery query, CancellationToken cancellationToken = default)
    {
        var source = Filter(query);
        var total = await source.CountAsync(cancellationToken);
        var items = await ProjectAdmin(Order(source, query).Skip(query.Skip).Take(query.PageSize)).ToListAsync(cancellationToken);
        return new(items, total, query.Page, query.PageSize);
    }

    public async Task<PagedResult<CounterPartDto>> SearchCounterAsync(PartQuery query, CancellationToken cancellationToken = default)
    {
        // Guard the repository too: counts and ordering must not leak private data.
        if (query.SupplierId.HasValue || query.SortBy == PartSortField.UnitPrice)
            throw new ConflictException("Private catalogue filters are not available at the counter.");
        var source = Filter(query);
        var total = await source.CountAsync(cancellationToken);
        var items = await ProjectCounter(Order(source, query).Skip(query.Skip).Take(query.PageSize)).ToListAsync(cancellationToken);
        return new(items, total, query.Page, query.PageSize);
    }

    private static (string Number, string Text, PartSearchMode Mode) Parse(PartQuery query)
    {
        if (!Enum.IsDefined(query.SearchMode) || !Enum.IsDefined(query.SortBy))
            throw new ConflictException("Unknown search or sort mode.");
        var raw = query.Search?.Trim() ?? "";
        if (raw.Length > 128) throw new ConflictException("Search must be 128 characters or fewer.");
        var mode = query.SearchMode;
        if (raw.StartsWith("~/", StringComparison.Ordinal))
        {
            raw = raw[2..].Trim();
            mode = PartSearchMode.Suffix;
            if (raw.Length == 0) throw new ConflictException("Enter a number after ~/.");
        }
        var number = raw.Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (raw.Length > 0 && number.Length == 0)
            throw new ConflictException("Enter a part number or name.");
        if (number.Length is > 0 and < 3 && mode is PartSearchMode.Contains or PartSearchMode.Suffix)
            throw new ConflictException("Enter at least three characters for contains or suffix search.");
        return (number, raw.ToUpperInvariant(), mode);
    }

    private IQueryable<Part> Filter(PartQuery query)
    {
        var (number, text, mode) = Parse(query);
        var source = db.Parts.AsNoTracking();
        if (!query.IncludeInactive) source = source.Where(p => p.IsActive);
        if (query.CategoryId.HasValue) source = source.Where(p => p.CategoryId == query.CategoryId);
        if (query.SupplierId.HasValue) source = source.Where(p => p.SupplierId == query.SupplierId);
        if (number.Length > 0)
        {
            // Build a set once rather than a correlated reference lookup per catalogue row.
            // This also lets PostgreSQL use reference indexes independently of part indexes.
            var references = db.Set<SkuReference>().AsNoTracking().Where(r => r.IsActive);
            var matchingReferences = mode switch
            {
                PartSearchMode.Exact => references.Where(r => r.Reference.SearchValue == number || r.Reference.MatchValue == number),
                PartSearchMode.Prefix => references.Where(r => r.Reference.MatchValue.StartsWith(number) || r.Reference.SearchValue.StartsWith(number)),
                PartSearchMode.Suffix => references.Where(r => r.Reference.SearchValue.EndsWith(number)),
                _ when number.Length < 3 => references.Where(r => r.Reference.MatchValue.StartsWith(number) || r.Reference.SearchValue.StartsWith(number)),
                _ => references.Where(r => r.Reference.SearchValue.Contains(number))
            };
            var referenceIds = matchingReferences.Select(r => r.PartId);
            source = mode switch
            {
                PartSearchMode.Exact => source.Where(p => p.SearchNumber == number || p.Barcode == text || referenceIds.Contains(p.Id)),
                PartSearchMode.Prefix => source.Where(p => p.SearchNumber.StartsWith(number) || referenceIds.Contains(p.Id)),
                PartSearchMode.Suffix => source.Where(p => p.SearchNumber.EndsWith(number) || referenceIds.Contains(p.Id)),
                PartSearchMode.Contains => source.Where(p => p.SearchNumber.Contains(number) || p.SearchName.Contains(text) || p.Barcode == text || referenceIds.Contains(p.Id)),
                _ when number.Length < 3 => source.Where(p => p.SearchNumber.StartsWith(number) || p.SearchName.StartsWith(text) || p.Barcode == text || referenceIds.Contains(p.Id)),
                _ => source.Where(p => p.SearchNumber.Contains(number) || p.SearchName.Contains(text) || p.Barcode == text || referenceIds.Contains(p.Id))
            };
        }
        if (query.LowStockOnly) source = LowStock(source);
        return source;
    }

    private IOrderedQueryable<Part> Order(IQueryable<Part> source, PartQuery query)
    {
        var (number, text, _) = Parse(query);
        var references = db.Set<SkuReference>().Where(r => r.IsActive);
        var exact = references.Where(r => r.Reference.SearchValue == number || r.Reference.MatchValue == number).Select(r => r.PartId);
        var prefix = references.Where(r => r.Reference.SearchValue.StartsWith(number) || r.Reference.MatchValue.StartsWith(number)).Select(r => r.PartId);
        var suffix = references.Where(r => r.Reference.SearchValue.EndsWith(number)).Select(r => r.PartId);
        if (number.Length > 0 && query.SortBy == PartSortField.PartNumber && !query.SortDescending)
            return source.OrderBy(p => p.SearchNumber == number || p.Barcode == text || exact.Contains(p.Id) ? 0
                : p.SearchNumber.StartsWith(number) || prefix.Contains(p.Id) ? 1 : p.SearchNumber.EndsWith(number) || suffix.Contains(p.Id) ? 2 : 3)
                .ThenBy(p => p.PartNumber).ThenBy(p => p.Id);
        var ordered = (query.SortBy, query.SortDescending) switch
        {
            (PartSortField.Name, false) => source.OrderBy(p => p.Name),
            (PartSortField.Name, true) => source.OrderByDescending(p => p.Name),
            (PartSortField.UnitPrice, false) => source.OrderBy(p => p.UnitPrice),
            (PartSortField.UnitPrice, true) => source.OrderByDescending(p => p.UnitPrice),
            (PartSortField.QuantityOnHand, false) => source.OrderBy(p => db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0),
            (PartSortField.QuantityOnHand, true) => source.OrderByDescending(p => db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0),
            (PartSortField.CreatedAtUtc, false) => source.OrderBy(p => p.CreatedAtUtc),
            (PartSortField.CreatedAtUtc, true) => source.OrderByDescending(p => p.CreatedAtUtc),
            (_, true) => source.OrderByDescending(p => p.PartNumber),
            _ => source.OrderBy(p => p.PartNumber)
        };
        return ordered.ThenBy(p => p.Id);
    }

    private IQueryable<Part> LowStock(IQueryable<Part> source) => source.Where(p =>
        (db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0) <= p.LowStockThreshold);

    private IQueryable<Part> LowStockPreview(int limit) => LowStock(db.Parts.AsNoTracking().Where(p => p.IsActive))
        .OrderBy(p => (db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0) - p.LowStockThreshold)
        .ThenBy(p => p.PartNumber).ThenBy(p => p.Id).Take(Math.Clamp(limit, 1, 100));

    public async Task<IReadOnlyList<PartDto>> GetLowStockAsync(int limit, CancellationToken cancellationToken = default) =>
        await ProjectAdmin(LowStockPreview(limit)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CounterPartDto>> GetCounterLowStockAsync(int limit, CancellationToken cancellationToken = default) =>
        await ProjectCounter(LowStockPreview(limit)).ToListAsync(cancellationToken);

    public Task<bool> PartNumberExistsAsync(string partNumber, Guid? excludeId, CancellationToken cancellationToken = default, string brand = "", string variant = "")
    {
        var normalized = partNumber.Trim().ToUpperInvariant();
        return db.Parts.AnyAsync(x => x.PartNumber == normalized && x.Brand == brand && x.Variant == variant && (excludeId == null || x.Id != excludeId), cancellationToken);
    }

    public Task<bool> BarcodeExistsAsync(string barcode, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = barcode.Trim();
        return db.Parts.AnyAsync(x => x.Barcode == normalized && (excludeId == null || x.Id != excludeId), cancellationToken);
    }

    public async Task<int> GetQuantityOnHandAsync(Guid partId, CancellationToken cancellationToken = default) =>
        await db.StockMovements.Where(m => m.PartId == partId).SumAsync(m => (int?)m.QuantityChange, cancellationToken) ?? 0;

    public async Task LockForStockChangeAsync(Guid partId, CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsNpgsql()) return;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {partId} FOR UPDATE", cancellationToken);
    }

    public async Task AddAsync(Part part, CancellationToken cancellationToken = default) => await db.Parts.AddAsync(part, cancellationToken);

    // Counter projection never selects purchase fields or joins Suppliers.
    private IQueryable<CounterPartDto> ProjectCounter(IQueryable<Part> source) => source.Select(p => new CounterPartDto
    {
        Id = p.Id, PartNumber = p.PartNumber, Brand = p.Brand, Variant = p.Variant, Name = p.Name, Description = p.Description,
        CategoryId = p.CategoryId, CategoryName = p.Category.Name, LowStockThreshold = p.LowStockThreshold,
        Barcode = p.Barcode, IsActive = p.IsActive, CreatedAtUtc = p.CreatedAtUtc,
        QuantityOnHand = db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0
    });

    private IQueryable<PartDto> ProjectAdmin(IQueryable<Part> source) => source.Select(p => new PartDto
    {
        Id = p.Id, PartNumber = p.PartNumber, Brand = p.Brand, Variant = p.Variant, Name = p.Name, Description = p.Description,
        CategoryId = p.CategoryId, CategoryName = p.Category.Name, LowStockThreshold = p.LowStockThreshold,
        Barcode = p.Barcode, IsActive = p.IsActive, CreatedAtUtc = p.CreatedAtUtc,
        SupplierId = p.SupplierId, SupplierName = p.Supplier != null ? p.Supplier.Name : null, UnitPrice = p.UnitPrice,
        QuantityOnHand = db.StockMovements.Where(m => m.PartId == p.Id).Sum(m => (int?)m.QuantityChange) ?? 0
    });
}
