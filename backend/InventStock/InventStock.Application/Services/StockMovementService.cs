using InventStock.Application.Common.Exceptions;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Domain.Entities;
using InventStock.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace InventStock.Application.Services;

public sealed class StockMovementService : IStockMovementService
{
    private readonly IPartRepository _parts;
    private readonly IStockMovementRepository _movements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IStockNotifier _notifier;
    private readonly ILogger<StockMovementService> _logger;

    public StockMovementService(
        IPartRepository parts,
        IStockMovementRepository movements,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IStockNotifier notifier,
        ILogger<StockMovementService> logger)
    {
        _parts = parts;
        _movements = movements;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _notifier = notifier;
        _logger = logger;
    }

    public Task<StockMovementDto> StockInAsync(Guid partId, StockInRequest request, CancellationToken cancellationToken = default) =>
        RecordAsync(
            partId,
            request.OperationId,
            userId => StockMovement.StockIn(partId, request.Quantity, userId, request.Reason, request.Reference),
            cancellationToken);

    public Task<StockMovementDto> StockOutAsync(Guid partId, StockOutRequest request, CancellationToken cancellationToken = default) =>
        RecordAsync(
            partId,
            request.OperationId,
            userId => StockMovement.StockOut(partId, request.Quantity, userId, request.Reason, request.Reference),
            cancellationToken);

    public Task<StockMovementDto> AdjustAsync(Guid partId, StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
        RecordAsync(
            partId,
            request.OperationId,
            userId => StockMovement.Adjustment(partId, request.QuantityChange, userId, request.Reason, request.Reference),
            cancellationToken);

    public Task<PagedResult<StockMovementDto>> SearchAsync(StockMovementQuery query, CancellationToken cancellationToken = default) =>
        _movements.SearchAsync(query, cancellationToken);

    public async Task<StockOperationStatusDto> GetOperationStatusAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("Operation status requires an authenticated user.");
        var movement = await _movements.FindOperationAsync(operationId, cancellationToken)
            ?? throw new NotFoundException("Stock operation", operationId);
        if (movement.PerformedByUserId != userId)
            throw new ConflictException("This operation ID belongs to another user.");
        var part = await _parts.GetByIdAsync(movement.PartId, cancellationToken)
            ?? throw new NotFoundException("Part", movement.PartId);
        return new("Committed", Map(movement, part, _currentUser.FullName ?? _currentUser.Email ?? "Unknown"));
    }

    private async Task<StockMovementDto> RecordAsync(
        Guid partId,
        Guid operationId,
        Func<Guid, StockMovement> movementFactory,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
                     ?? throw new AuthenticationFailedException("A stock movement requires an authenticated user.");

        var performedBy = _currentUser.FullName ?? _currentUser.Email ?? "Unknown";

        StockMovement movement;
        Part part;
        int quantityOnHandAfter;
        var isReplay = false;

        await using (var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            if (operationId == Guid.Empty) throw new ConflictException("A stock operation ID is required.");
            await _movements.LockOperationAsync(userId, operationId, cancellationToken);

            var candidate = movementFactory(userId);
            var fingerprint = Fingerprint(partId, candidate);
            var existing = await _movements.FindOperationAsync(operationId, cancellationToken);
            if (existing is not null)
            {
                if (existing.PerformedByUserId != userId)
                    throw new ConflictException("This operation ID belongs to another user.");
                if (!string.Equals(existing.PayloadFingerprint, fingerprint, StringComparison.Ordinal))
                    throw new ConflictException("This operation ID was already used with different stock details.");
                if (existing.PartId != partId)
                    throw new ConflictException("This operation ID belongs to another part.");
                part = await _parts.GetByIdAsync(partId, cancellationToken)
                    ?? throw new NotFoundException("Part", partId);
                movement = existing;
                quantityOnHandAfter = await _parts.GetQuantityOnHandAsync(partId, cancellationToken);
                isReplay = true;
                await transaction.CommitAsync(cancellationToken);
                goto Committed;
            }

            // Serialise concurrent movements for this part so the read-then-write below cannot race.
            await _parts.LockForStockChangeAsync(partId, cancellationToken);

            part = await _parts.GetByIdAsync(partId, cancellationToken)
                   ?? throw new NotFoundException("Part", partId);

            if (!part.IsActive)
            {
                throw new ConflictException($"Part '{part.PartNumber}' is inactive and cannot receive stock movements.");
            }

            var quantityOnHandBefore = await _parts.GetQuantityOnHandAsync(partId, cancellationToken);

            movement = candidate;
            movement.SetOperation(operationId, fingerprint);
            quantityOnHandAfter = quantityOnHandBefore + movement.QuantityChange;

            if (quantityOnHandAfter < 0)
            {
                throw new InsufficientStockException(part.PartNumber, quantityOnHandBefore, Math.Abs(movement.QuantityChange));
            }

            await _movements.AddAsync(movement, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        Committed:

        var dto = Map(movement, part, performedBy);

        if (!isReplay)
            await NotifyAsync(part, movement, quantityOnHandAfter, performedBy, cancellationToken);

        return dto;
    }

    private static string Fingerprint(Guid partId, StockMovement movement)
    {
        var canonical = string.Join('|', partId.ToString("N"), movement.MovementType,
            movement.QuantityChange, movement.Reason?.Trim() ?? "", movement.Reference?.Trim() ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static StockMovementDto Map(StockMovement movement, Part part, string performedBy) => new()
    {
        Id = movement.Id, OperationId = movement.OperationId, PartId = part.Id,
        PartNumber = part.PartNumber, PartName = part.Name, MovementType = movement.MovementType,
        QuantityChange = movement.QuantityChange, Reason = movement.Reason, Reference = movement.Reference,
        PerformedByUserId = movement.PerformedByUserId, PerformedByUserName = performedBy,
        OccurredAtUtc = movement.OccurredAtUtc
    };

    private async Task NotifyAsync(
        Part part,
        StockMovement movement,
        int quantityOnHand,
        string performedBy,
        CancellationToken cancellationToken)
    {
        var notification = new StockChangedNotification
        {
            PartId = part.Id,
            PartNumber = part.PartNumber,
            PartName = part.Name,
            QuantityOnHand = quantityOnHand,
            LowStockThreshold = part.LowStockThreshold,
            IsLowStock = quantityOnHand <= part.LowStockThreshold,
            MovementType = movement.MovementType,
            QuantityChange = movement.QuantityChange,
            PerformedByUserName = performedBy,
            OccurredAtUtc = movement.OccurredAtUtc
        };

        try
        {
            // Broadcast only after the transaction has committed, so clients never observe rolled-back stock.
            await _notifier.StockChangedAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            // A failed broadcast must not fail an already-committed movement.
            _logger.LogError(ex, "Failed to broadcast stock change for part {PartId}.", part.Id);
        }
    }
}
