using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Imports;
using InventStock.Application.Interfaces;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Services;

public sealed class SupplierImportService(AppDbContext db, ICurrentUserService currentUser) : ISupplierImportService
{
    public async Task<SupplierImportBatchDto> StageAsync(StageSupplierImportRequest request, CancellationToken cancellationToken = default)
    {
        _ = currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken))
            throw new NotFoundException("Active supplier", request.SupplierId);
        var hash = request.SourceSha256.ToUpperInvariant();
        if (await db.SupplierImportBatches.AnyAsync(x => x.SupplierId == request.SupplierId && x.SourceSha256 == hash, cancellationToken))
            throw new ConflictException("This supplier file has already been staged.");
        if (request.Rows.Select(x => x.SourceRowNumber).Distinct().Count() != request.Rows.Count)
            throw new ConflictException("Source row numbers must be unique within an import.");
        var duplicate = request.Rows.GroupBy(x => new
            {
                Number = Part.NormalizePartNumber(x.PartNumber),
                Brand = (x.Brand ?? string.Empty).Trim().ToUpperInvariant(),
                Variant = (x.Variant ?? string.Empty).Trim().ToUpperInvariant(),
                Name = x.Name.Trim().ToUpperInvariant(),
                SupplierCode = (x.SupplierCode ?? string.Empty).Trim().ToUpperInvariant(),
                x.CatalogueCost,
                x.Currency,
                x.TaxTreatment
            })
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new ConflictException("The supplier file contains an exact duplicate row. Remove it before review.");

        var batch = SupplierImportBatch.Create(request.SupplierId, request.SourceFileName, hash);
        foreach (var row in request.Rows.OrderBy(x => x.SourceRowNumber))
            batch.AddRow(row.SourceRowNumber, row.PartNumber, row.Name, row.Brand, row.Variant,
                row.SupplierCode, row.CatalogueCost, row.Currency, row.TaxTreatment);
        db.SupplierImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        return Map(await Query().SingleAsync(x => x.Id == batch.Id, cancellationToken));
    }

    public async Task<SupplierImportBatchDto> GetAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        Map(await Query().SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new NotFoundException("Supplier import", batchId));

    public async Task<SupplierImportBatchDto> ReviewRowAsync(Guid batchId, Guid rowId,
        ReviewSupplierImportRowRequest request, CancellationToken cancellationToken = default)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var batch = await db.SupplierImportBatches.Include(x => x.Rows)
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new NotFoundException("Supplier import", batchId);
        if (batch.Status != SupplierImportStatus.Staged) throw new ConflictException("A committed import cannot be reviewed again.");
        var row = batch.Rows.SingleOrDefault(x => x.Id == rowId) ?? throw new NotFoundException("Supplier import row", rowId);

        if (request.Decision == SupplierImportDecision.LinkExisting &&
            !await db.Parts.AnyAsync(x => x.Id == request.TargetPartId && x.IsActive, cancellationToken))
            throw new NotFoundException("Active target part", request.TargetPartId ?? Guid.Empty);
        if (request.Decision == SupplierImportDecision.CreateNew)
        {
            if (!await db.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken))
                throw new NotFoundException("Category", request.CategoryId ?? Guid.Empty);
            var brandId = request.BrandId ?? ProductBrand.UnknownId;
            if (!await db.ProductBrands.AnyAsync(x => x.Id == brandId && x.IsActive, cancellationToken))
                throw new NotFoundException("Active brand", brandId);
        }

        row.Review(request.Decision, request.TargetPartId, request.CategoryId, request.BrandId, request.StockUnit, actorId);
        await db.SaveChangesAsync(cancellationToken);
        return Map(await Query().SingleAsync(x => x.Id == batchId, cancellationToken));
    }

    public async Task<CommitSupplierImportResult> CommitAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await db.SupplierImportBatches.Include(x => x.Rows)
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new NotFoundException("Supplier import", batchId);
        if (batch.Status != SupplierImportStatus.Staged) throw new ConflictException("This import is already committed.");
        if (batch.Rows.Any(x => x.Decision == SupplierImportDecision.Pending))
            throw new ConflictException("Every row must be reviewed before commit.");

        var brandIds = batch.Rows.Where(x => x.Decision == SupplierImportDecision.CreateNew).Select(x => x.BrandId ?? ProductBrand.UnknownId).Distinct().ToArray();
        var brands = await db.ProductBrands.Where(x => brandIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        var resultPartIds = new List<Guid>();
        var created = 0;
        var linked = 0;
        var costs = 0;

        foreach (var row in batch.Rows.OrderBy(x => x.SourceRowNumber))
        {
            if (row.Decision == SupplierImportDecision.Skip) continue;
            Part part;
            if (row.Decision == SupplierImportDecision.LinkExisting)
            {
                part = await db.Parts.SingleOrDefaultAsync(x => x.Id == row.TargetPartId && x.IsActive, cancellationToken)
                    ?? throw new NotFoundException("Active target part", row.TargetPartId ?? Guid.Empty);
                linked++;
            }
            else
            {
                var brandId = row.BrandId ?? ProductBrand.UnknownId;
                if (!brands.TryGetValue(brandId, out var brand)) throw new NotFoundException("Active brand", brandId);
                var normalizedVariant = row.Variant.Trim().ToUpperInvariant();
                if (await db.Parts.AnyAsync(x => x.PartNumber == row.PartNumber && x.BrandId == brandId && x.Variant == normalizedVariant, cancellationToken) ||
                    db.ChangeTracker.Entries<Part>().Any(x => x.State == EntityState.Added && x.Entity.PartNumber == row.PartNumber && x.Entity.BrandId == brandId && x.Entity.Variant == normalizedVariant))
                    throw new ConflictException($"Row {row.SourceRowNumber} matches an existing SKU. Review it as LinkExisting instead.");
                part = Part.Create(row.PartNumber, row.Name, null, row.CategoryId!.Value, batch.SupplierId, 0m, 0, null);
                part.SetIdentity(row.Brand, row.Variant);
                part.SetBrand(brand);
                part.SetStockUnit(row.StockUnit, false);
                db.Parts.Add(part);
                created++;
            }

            resultPartIds.Add(part.Id);
            if (row.CatalogueCost.HasValue)
            {
                db.SupplierCostObservations.Add(SupplierCostObservation.Create(part.Id, batch.SupplierId,
                    row.CatalogueCost.Value, row.Currency!.Value, row.TaxTreatment,
                    CostSource.SupplierCatalogue, 1, row.SupplierCode,
                    $"Imported from {batch.SourceFileName}, row {row.SourceRowNumber}", batch.CreatedAtUtc));
                costs++;
            }
        }

        batch.Commit(actorId);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new CommitSupplierImportResult(batch.Id, created, linked,
            batch.Rows.Count(x => x.Decision == SupplierImportDecision.Skip), costs, resultPartIds);
    }

    private IQueryable<SupplierImportBatch> Query() => db.SupplierImportBatches.AsNoTracking()
        .Include(x => x.Supplier).Include(x => x.Rows);

    private static SupplierImportBatchDto Map(SupplierImportBatch batch) => new(batch.Id, batch.SupplierId,
        batch.Supplier.Name, batch.SourceFileName, batch.SourceSha256, batch.Status, batch.CreatedAtUtc,
        batch.CommittedAtUtc, batch.Rows.OrderBy(x => x.SourceRowNumber).Select(x =>
            new SupplierImportRowDto(x.Id, x.SourceRowNumber, x.PartNumber, x.Name, x.Brand, x.Variant,
                x.SupplierCode, x.CatalogueCost, x.Currency, x.TaxTreatment, x.Decision, x.TargetPartId,
                x.CategoryId, x.BrandId, x.StockUnit, x.ReviewedAtUtc)).ToArray());
}
