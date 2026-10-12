using System.Security.Cryptography;
using System.Text;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Services;

public sealed class StockHoldService(AppDbContext db, ICurrentUserService currentUser) : IStockHoldService
{
    public async Task<StockHoldMovementDto> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException("Authentication is required.");
        var movement = await db.StockHoldMovements.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId, cancellationToken)
            ?? throw new NotFoundException("Stock hold operation", operationId);
        if (movement.PerformedByUserId != userId) throw new ConflictException("This operation ID belongs to another user.");
        return Map(movement);
    }

    public Task<StockHoldMovementDto> HoldAsync(Guid partId, StockHoldRequest request, CancellationToken cancellationToken = default) =>
        RecordAsync(partId, request.OperationId, StockHoldAction.Hold, request.Quantity, request.Reason, request.PhysicalSeparationConfirmed, cancellationToken);
    public Task<StockHoldMovementDto> ReleaseAsync(Guid partId, StockReleaseRequest request, CancellationToken cancellationToken = default) =>
        RecordAsync(partId, request.OperationId, StockHoldAction.Release, request.Quantity, request.Reason, false, cancellationToken);

    private async Task<StockHoldMovementDto> RecordAsync(Guid partId, Guid operationId, StockHoldAction action,
        int quantity, string reason, bool separated, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException("Authentication is required.");
        var canonical = $"{partId:N}|{action}|{quantity}|{reason.Trim()}|{separated}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await OperationGuard.LockAsync(db, operationId, "Hold", cancellationToken);
        if (db.Database.IsNpgsql()) {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {partId} FOR UPDATE", cancellationToken);
        }
        var existing = await db.StockHoldMovements.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);
        if (existing is not null) {
            if (existing.PerformedByUserId != userId || existing.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID is already owned by another stock-hold action.");
            await transaction.CommitAsync(cancellationToken); return Map(existing);
        }
        var part = await db.Parts.SingleOrDefaultAsync(x => x.Id == partId, cancellationToken) ?? throw new NotFoundException("Part", partId);
        if (!part.IsActive) throw new ConflictException("Inactive parts cannot change stock condition.");
        var physical = await db.StockMovements.Where(x => x.PartId == partId).SumAsync(x => (int?)x.QuantityChange, cancellationToken) ?? 0;
        var quarantined = await db.StockHoldMovements.Where(x => x.PartId == partId).SumAsync(x => (int?)x.QuantityChange, cancellationToken) ?? 0;
        if (action == StockHoldAction.Hold && quantity > physical - quarantined) throw new InsufficientStockException(part.PartNumber, physical - quarantined, quantity);
        if (action == StockHoldAction.Release && quantity > quarantined) throw new DomainException("Cannot release more than the quarantined quantity.");
        var movement = StockHoldMovement.Create(partId, action, quantity, reason, separated, userId, operationId, fingerprint);
        db.StockHoldMovements.Add(movement); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return Map(movement);
    }
    private static StockHoldMovementDto Map(StockHoldMovement x) => new(x.Id, x.OperationId, x.PartId, x.Action,
        x.QuantityChange, x.Reason, x.PhysicalSeparationConfirmed, x.PerformedByUserId, x.OccurredAtUtc);
}
