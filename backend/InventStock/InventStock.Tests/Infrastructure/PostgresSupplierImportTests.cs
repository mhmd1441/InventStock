using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Imports;
using InventStock.Application.Interfaces;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace InventStock.Tests.Infrastructure;

[Collection("PostgreSQL schema migrations")]
public sealed class PostgresSupplierImportTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    [Fact]
    public async Task Messy_reviewed_import_creates_catalogue_only_and_rejects_duplicate_source()
    {
        await using var db = fixture.Open();
        var supplier = Supplier.Create("Messy matrix " + Guid.NewGuid().ToString("N"), null, null, null, null);
        var febi = ProductBrand.Create("FEBI " + Guid.NewGuid().ToString("N")[..6]);
        var matrix = ProductBrand.Create("Matrix " + Guid.NewGuid().ToString("N")[..6]);
        db.AddRange(supplier, febi, matrix);
        await db.SaveChangesAsync();
        var current = Substitute.For<ICurrentUserService>();
        current.UserId.Returns(fixture.AdminActor);
        var service = new SupplierImportService(db, current);

        var rows = Enumerable.Range(1, 100).Select(index => new StageSupplierImportRowRequest
        {
            SourceRowNumber = index,
            PartNumber = index is 2 or 3 ? "SAME-001" : index == 4 ? "0000125" : $"MESSY-{index:0000}",
            Name = index == 7 ? "خرطوم زيت" : $"Filter {index}",
            Brand = index == 2 ? febi.Name : index == 3 ? matrix.Name : index == 6 ? null : febi.Name,
            CatalogueCost = index == 8 ? null : 12.50m,
            Currency = index == 8 ? null : MoneyCurrency.USD,
            TaxTreatment = index == 9 ? TaxTreatment.Unknown : TaxTreatment.Included
        }).ToArray();
        var hash = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var batch = await service.StageAsync(new() { SupplierId = supplier.Id, SourceFileName = "messy-100.csv", SourceSha256 = hash, Rows = rows });
        Assert.Equal(100, batch.Rows.Count);

        foreach (var row in batch.Rows)
        {
            var brandId = row.SourceRowNumber == 3 ? matrix.Id : row.SourceRowNumber == 6 ? ProductBrand.UnknownId : febi.Id;
            batch = await service.ReviewRowAsync(batch.Id, row.Id, new()
            {
                Decision = SupplierImportDecision.CreateNew, CategoryId = fixture.CategoryId,
                BrandId = brandId, StockUnit = StockUnit.Piece
            });
        }
        var result = await service.CommitAsync(batch.Id);
        Assert.Equal(100, result.CreatedParts);
        Assert.Equal(99, result.CostObservations);
        Assert.False(await db.StockMovements.AnyAsync(x => result.ResultPartIds.Contains(x.PartId)));
        Assert.False(await db.CashMovements.AnyAsync());
        await Assert.ThrowsAsync<ConflictException>(() => service.StageAsync(new()
            { SupplierId = supplier.Id, SourceFileName = "same-file.csv", SourceSha256 = hash, Rows = rows }));

        var duplicateRows = new[] { rows[0], rows[0] with { SourceRowNumber = 101 } };
        await Assert.ThrowsAsync<ConflictException>(() => service.StageAsync(new()
            { SupplierId = supplier.Id, SourceFileName = "duplicate.csv", SourceSha256 = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), Rows = duplicateRows }));
    }
}
