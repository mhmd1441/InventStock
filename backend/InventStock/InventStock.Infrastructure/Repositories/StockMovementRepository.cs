using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Repositories;

public sealed class StockMovementRepository : IStockMovementRepository
{
    private const string UnknownUser = "Unknown";

    private readonly AppDbContext _db;

    public StockMovementRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default) =>
        await _db.StockMovements.AddAsync(movement, cancellationToken);

    public Task<StockMovement?> FindOperationAsync(Guid operationId, CancellationToken cancellationToken = default) =>
        _db.StockMovements.AsNoTracking().FirstOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);

    public async Task LockOperationAsync(Guid userId, Guid operationId, CancellationToken cancellationToken = default)
    {
        await OperationGuard.LockAsync(_db, operationId, "Stock", cancellationToken);
    }

    public async Task<PagedResult<StockMovementDto>> SearchAsync(StockMovementQuery query, CancellationToken cancellationToken = default)
    {
        var source = _db.StockMovements.AsNoTracking();

        if (query.PartId.HasValue)
        {
            source = source.Where(m => m.PartId == query.PartId.Value);
        }

        if (query.MovementType.HasValue)
        {
            source = source.Where(m => m.MovementType == query.MovementType.Value);
        }

        if (query.FromUtc.HasValue)
        {
            source = source.Where(m => m.OccurredAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            source = source.Where(m => m.OccurredAtUtc <= query.ToUtc.Value);
        }

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await Project(source)
            .OrderByDescending(m => m.OccurredAtUtc)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        await Project(_db.StockMovements.AsNoTracking())
            .OrderByDescending(m => m.OccurredAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

    private IQueryable<StockMovementDto> Project(IQueryable<StockMovement> source) =>
        source.Select(m => new StockMovementDto
        {
            Id = m.Id,
            OperationId = m.OperationId,
            PartId = m.PartId,
            PartNumber = m.Part.PartNumber,
            PartName = m.Part.Name,
            MovementType = m.MovementType,
            QuantityChange = m.QuantityChange,
            CountedQuantity = m.CountedQuantity,
            BookQuantityBeforeCount = m.BookQuantityBeforeCount,
            Reason = m.Reason,
            Reference = m.Reference,
            PerformedByUserId = m.PerformedByUserId,
            PerformedByUserName = _db.Users
                .Where(u => u.Id == m.PerformedByUserId)
                .Select(u => u.FullName)
                .FirstOrDefault() ?? UnknownUser,
            OccurredAtUtc = m.OccurredAtUtc
        });
}
