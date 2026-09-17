using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    public CategoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await Project(_db.Categories.AsNoTracking())
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<CategoryDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Project(_db.Categories.AsNoTracking().Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.AnyAsync(x => x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return _db.Categories.AnyAsync(
            x => x.Name.ToLower() == normalized && (excludeId == null || x.Id != excludeId),
            cancellationToken);
    }

    public Task<bool> HasPartsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Parts.AnyAsync(x => x.CategoryId == id, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        await _db.Categories.AddAsync(category, cancellationToken);

    public void Remove(Category category) => _db.Categories.Remove(category);

    private IQueryable<CategoryDto> Project(IQueryable<Category> source) =>
        source.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            PartCount = _db.Parts.Count(p => p.CategoryId == c.Id)
        });
}
