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

    public async Task<StockMovementDto> CountAsync(Guid partId, StockCountRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("A stock count requires an authenticated user.");
        var performedBy = _currentUser.FullName ?? _currentUser.Email ?? "Unknown";
        var fingerprint = FingerprintCount(partId, request);
        StockMovement movement;
        Part part;
        var isReplay = false;

        await using (var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            if (request.OperationId == Guid.Empty) throw new ConflictException("A stock operation ID is required.");
            await _movements.LockOperationAsync(userId, request.OperationId, cancellationToken);

            var existing = await _movements.FindOperationAsync(request.OperationId, cancellationToken);
            if (existing is not null)
            {
                if (existing.PerformedByUserId != userId)
                    throw new ConflictException("This operation ID belongs to another user.");
                if (!string.Equals(existing.PayloadFingerprint, fingerprint, StringComparison.Ordinal) || existing.PartId != partId)
                    throw new ConflictException("This operation ID was already used with different stock details.");
                part = await _parts.GetByIdAsync(partId, cancellationToken)
                    ?? throw new NotFoundException("Part", partId);
                movement = existing;
                isReplay = true;
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await _parts.LockForStockChangeAsync(partId, cancellationToken);
                part = await _parts.GetByIdAsync(partId, cancellationToken)
                    ?? throw new NotFoundException("Part", partId);
                if (!part.IsActive)
                    throw new ConflictException($"Part '{part.PartNumber}' is inactive and cannot be counted.");

                var bookQuantity = await _parts.GetQuantityOnHandAsync(partId, cancellationToken);
                if (bookQuantity != request.ExpectedBookQuantity)
                    throw new ConflictException("Stock changed while this count was open. Physically recount before confirming.");
                var quarantined = await _parts.GetQuarantinedQuantityAsync(partId, cancellationToken);
                if (request.CountedQuantity < quarantined)
                    throw new ConflictException("The physical count is below the quarantined quantity. Resolve the held goods before confirming.");

                movement = StockMovement.PhysicalCount(partId, request.CountedQuantity, bookQuantity, userId, request.Reason);
                movement.SetOperation(request.OperationId, fingerprint);
                await _movements.AddAsync(movement, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }

        var dto = Map(movement, part, performedBy);
        if (!isReplay)
            await NotifyAsync(part, movement, request.CountedQuantity - await _parts.GetQuarantinedQuantityAsync(partId, cancellationToken), performedBy, isCounted: true, cancellationToken);
        return dto;
    }

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
                quantityOnHandAfter = await _parts.GetAvailableQuantityAsync(partId, cancellationToken);
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

            var physicalBefore = await _parts.GetQuantityOnHandAsync(partId, cancellationToken);
            var availableBefore = await _parts.GetAvailableQuantityAsync(partId, cancellationToken);

            movement = candidate;
            movement.SetOperation(operationId, fingerprint);
            quantityOnHandAfter = availableBefore + movement.QuantityChange;

            if (quantityOnHandAfter < 0)
            {
                throw new InsufficientStockException(part.PartNumber, availableBefore, Math.Abs(movement.QuantityChange));
            }

            if (physicalBefore + movement.QuantityChange < await _parts.GetQuarantinedQuantityAsync(partId, cancellationToken))
                throw new ConflictException("This adjustment would consume quarantined stock. Release or resolve the hold explicitly first.");

            await _movements.AddAsync(movement, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        Committed:

        var dto = Map(movement, part, performedBy);

        if (!isReplay)
            await NotifyAsync(part, movement, quantityOnHandAfter, performedBy,
                await _parts.HasBeenCountedAsync(part.Id, cancellationToken), cancellationToken);

        return dto;
    }

    private static string Fingerprint(Guid partId, StockMovement movement)
    {
        var canonical = string.Join('|', partId.ToString("N"), movement.MovementType,
            movement.QuantityChange, movement.Reason?.Trim() ?? "", movement.Reference?.Trim() ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string FingerprintCount(Guid partId, StockCountRequest request)
    {
        var canonical = string.Join('|', partId.ToString("N"), "PhysicalCount", request.CountedQuantity,
            request.ExpectedBookQuantity, request.Reason?.Trim() ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static StockMovementDto Map(StockMovement movement, Part part, string performedBy) => new()
    {
        Id = movement.Id, OperationId = movement.OperationId, PartId = part.Id,
        PartNumber = part.PartNumber, PartName = part.Name, MovementType = movement.MovementType,
        QuantityChange = movement.QuantityChange, CountedQuantity = movement.CountedQuantity,
        BookQuantityBeforeCount = movement.BookQuantityBeforeCount, Reason = movement.Reason, Reference = movement.Reference,
        PerformedByUserId = movement.PerformedByUserId, PerformedByUserName = performedBy,
        OccurredAtUtc = movement.OccurredAtUtc
    };

    private async Task NotifyAsync(
        Part part,
        StockMovement movement,
        int quantityOnHand,
        string performedBy,
        bool isCounted,
        CancellationToken cancellationToken)
    {
        var notification = new StockChangedNotification
        {
            PartId = part.Id,
            PartNumber = part.PartNumber,
            PartName = part.Name,
            QuantityOnHand = quantityOnHand,
            LowStockThreshold = part.LowStockThreshold,
            IsLowStock = isCounted && quantityOnHand <= part.LowStockThreshold,
            StockConfidence = isCounted ? Domain.Enums.StockConfidence.Counted : Domain.Enums.StockConfidence.NotCounted,
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
