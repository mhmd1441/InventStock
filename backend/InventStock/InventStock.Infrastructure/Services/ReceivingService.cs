using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Receiving;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventStock.Infrastructure.Services;

public sealed class ReceivingService(AppDbContext db, ICurrentUserService currentUser, IStockNotifier notifier,
    ILogger<ReceivingService> logger)
    : IReceivingService
{
    public async Task<PurchaseReceiptDto> PostAsync(PostPurchaseReceiptRequest request, CancellationToken cancellationToken = default)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var fingerprint = Fingerprint(request);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await OperationGuard.LockAsync(db, request.OperationId, "Receipt", cancellationToken);

        var existing = await Query().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ReceivedByUserId != actorId || existing.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID is already used by another receiving command.");
            await tx.CommitAsync(cancellationToken);
            return Map(existing);
        }

        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Active supplier", request.SupplierId);
        var requestedPartIds = request.Lines.Select(x => x.PartId).Distinct().Order().ToArray();
        foreach (var partId in requestedPartIds)
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Parts\" WHERE \"Id\" = {partId} FOR UPDATE", cancellationToken);
        var parts = await db.Parts.Where(x => requestedPartIds.Contains(x.Id) && x.IsActive)
            .OrderBy(x => x.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        var missing = requestedPartIds.FirstOrDefault(x => !parts.ContainsKey(x));
        if (missing != Guid.Empty) throw new NotFoundException("Active part", missing);

        var receivedAt = request.ReceivedAtUtc == default ? DateTime.UtcNow : request.ReceivedAtUtc;
        var receipt = PurchaseReceipt.Create(request.SupplierId, request.OperationId, fingerprint,
            request.SupplierReference, request.Notes, receivedAt, actorId);

        var notifications = new List<(Part Part, StockMovement Movement)>();
        foreach (var requestedLine in request.Lines)
        {
            var line = receipt.AddLine(requestedLine.PartId, requestedLine.PackageCount,
                requestedLine.BaseUnitsPerPackage, requestedLine.CostPerPackage, requestedLine.Currency,
                requestedLine.TaxTreatment, requestedLine.SupplierCode, requestedLine.Notes);
            var movement = StockMovement.StockIn(requestedLine.PartId, line.BaseQuantity, actorId,
                "Supplier receipt", request.SupplierReference);
            movement.LinkToPurchaseReceiptLine(line.Id);
            db.StockMovements.Add(movement);

            SupplierCostObservation? cost = null;
            if (requestedLine.CostPerPackage.HasValue)
            {
                cost = SupplierCostObservation.Create(requestedLine.PartId, request.SupplierId,
                    requestedLine.CostPerPackage.Value, requestedLine.Currency!.Value,
                    requestedLine.TaxTreatment, CostSource.ActualPurchase, requestedLine.BaseUnitsPerPackage,
                    requestedLine.SupplierCode, requestedLine.Notes, receivedAt);
                cost.LinkToPurchaseReceiptLine(line.Id);
                db.SupplierCostObservations.Add(cost);
            }

            line.LinkArtifacts(movement.Id, cost?.Id);
            notifications.Add((parts[requestedLine.PartId], movement));
        }

        db.PurchaseReceipts.Add(receipt);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var posted = await Query().SingleAsync(x => x.Id == receipt.Id, cancellationToken);
        foreach (var (part, movement) in notifications)
        {
            try
            {
                var available = await AvailableQuantityAsync(part.Id, cancellationToken);
                var confidence = await db.StockMovements.AnyAsync(x => x.PartId == part.Id && x.CountedQuantity != null, cancellationToken)
                    ? StockConfidence.Counted : StockConfidence.NotCounted;
                await notifier.StockChangedAsync(new StockChangedNotification
                {
                    PartId = part.Id,
                    PartNumber = part.PartNumber,
                    PartName = part.Name,
                    QuantityOnHand = available,
                    LowStockThreshold = part.LowStockThreshold,
                    IsLowStock = confidence == StockConfidence.Counted && available <= part.LowStockThreshold,
                    StockConfidence = confidence,
                    MovementType = movement.MovementType,
                    QuantityChange = movement.QuantityChange,
                    PerformedByUserName = currentUser.FullName ?? currentUser.Email ?? "Unknown",
                    OccurredAtUtc = movement.OccurredAtUtc
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                // The receipt is already committed. Live updates are only refresh hints and must not
                // turn a successful receipt into an uncertain failure at the counter.
                logger.LogError(ex, "Failed to broadcast received stock for part {PartId}.", part.Id);
            }
        }
        return Map(posted);
    }

    public async Task<ReceivingOperationStatusDto> GetOperationStatusAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var receipt = await Query().SingleOrDefaultAsync(x => x.OperationId == operationId && x.ReceivedByUserId == actorId, cancellationToken)
            ?? throw new NotFoundException("Receiving operation", operationId);
        return new ReceivingOperationStatusDto(operationId, true, Map(receipt));
    }

    public async Task<PurchaseReceiptDto> GetAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await Query().SingleOrDefaultAsync(x => x.Id == receiptId, cancellationToken)
            ?? throw new NotFoundException("Purchase receipt", receiptId);
        return Map(receipt);
    }

    private IQueryable<PurchaseReceipt> Query() => db.PurchaseReceipts.AsNoTracking()
        .Include(x => x.Supplier).Include(x => x.Lines).ThenInclude(x => x.Part);

    private async Task<int> AvailableQuantityAsync(Guid partId, CancellationToken cancellationToken)
    {
        var physical = await db.StockMovements.Where(x => x.PartId == partId).SumAsync(x => (int?)x.QuantityChange, cancellationToken) ?? 0;
        var held = await db.StockHoldMovements.Where(x => x.PartId == partId).SumAsync(x => (int?)x.QuantityChange, cancellationToken) ?? 0;
        return physical - held;
    }

    private static string Fingerprint(PostPurchaseReceiptRequest request)
    {
        var text = string.Join("|", new[]
        {
            request.SupplierId.ToString("N"), request.SupplierReference?.Trim() ?? "", request.Notes?.Trim() ?? "",
            request.ReceivedAtUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
            string.Join(";", request.Lines.Select(x => string.Join(",", x.PartId.ToString("N"),
                x.PackageCount.ToString(CultureInfo.InvariantCulture), x.BaseUnitsPerPackage.ToString(CultureInfo.InvariantCulture),
                x.CostPerPackage?.ToString("G29", CultureInfo.InvariantCulture) ?? "", x.Currency?.ToString() ?? "",
                x.TaxTreatment.ToString(), x.SupplierCode?.Trim() ?? "", x.Notes?.Trim() ?? "")))
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static PurchaseReceiptDto Map(PurchaseReceipt receipt) => new(receipt.Id, receipt.OperationId,
        receipt.SupplierId, receipt.Supplier.Name, receipt.SupplierReference, receipt.Notes, receipt.ReceivedAtUtc,
        receipt.ReceivedByUserId, receipt.CreatedAtUtc, receipt.Lines.OrderBy(x => x.Id).Select(x =>
            new PurchaseReceiptLineDto(x.Id, x.PartId, x.Part.PartNumber, x.Part.Name, x.Part.StockUnit,
                x.PackageCount, x.BaseUnitsPerPackage, x.BaseQuantity, x.CostPerPackage, x.Currency,
                x.TaxTreatment, x.SupplierCode, x.Notes, x.StockMovementId, x.SupplierCostObservationId)).ToArray());
}
