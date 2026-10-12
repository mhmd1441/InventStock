using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using InventStock.Infrastructure.Repositories;
using InventStock.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace InventStock.Tests.Infrastructure;

[Collection("PostgreSQL schema migrations")]
public class ReferenceUpgradeTests
{
    [PostgresFact]
    public async Task PopulatedPreviousSchema_PreservesIdsStockAndReferenceLinks()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_POSTGRES"));
        Assert.Equal("inventstock_test", builder.Database);
        var schema = "upgrade_test_" + Guid.NewGuid().ToString("N");
        builder.SearchPath = schema + ",public";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options;
        var actor = new TestCurrentUser(Guid.NewGuid());
        await using var db = new AppDbContext(options, actor);
#pragma warning disable EF1002 // Schema identifier is exclusively a generated GUID with a fixed prefix.
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261010205239_ReferenceIdentityAndAudit");
            var category = Category.Create("Legacy", null);
            var part = Part.Create("OLD-1", "Legacy part", null, category.Id, null, 15, 0, null);
            part.SetIdentity("  Febi  ", "Includes seal");
            var movement = StockMovement.StockIn(part.Id, 7, actor.UserId!.Value, "Original receipt", null);
            db.Add(category);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Parts" ("Id", "PartNumber", "Name", "Brand", "Variant", "CategoryId", "UnitPrice", "LowStockThreshold", "IsActive", "CreatedAtUtc")
                VALUES ({part.Id}, {part.PartNumber}, {part.Name}, {part.Brand}, {part.Variant}, {part.CategoryId}, {part.UnitPrice}, {part.LowStockThreshold}, TRUE, now());
                INSERT INTO "StockMovements" ("Id", "PartId", "MovementType", "QuantityChange", "Reason", "PerformedByUserId", "OccurredAtUtc", "CreatedAtUtc")
                VALUES ({movement.Id}, {part.Id}, 'In', {movement.QuantityChange}, {movement.Reason}, {actor.UserId.Value}, now(), now());
                """);
            var referenceId = Guid.NewGuid();
            var mercedesId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CatalogueReferences" ("Id", "Kind", "Issuer", "Value", "IdentityKey", "SearchValue", "MatchValue", "CreatedAtUtc")
                VALUES ({referenceId}, 'Manufacturer', '', 'ab-12', 'AB-12', 'AB12', 'AB12', now()),
                       ({mercedesId}, 'MercedesOE', 'MERCEDES', '204 501 27 82', 'A2045012782', 'A2045012782', '2045012782', now());
                INSERT INTO "SkuReferences" ("PartId", "ReferenceId", "LinkedAtUtc", "LinkedByUserId", "IsActive", "Revision")
                VALUES ({part.Id}, {referenceId}, now(), {actor.UserId.Value}, TRUE, 0);
                """);
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(part.Id, (await db.Parts.SingleAsync()).Id);
            Assert.Equal(movement.Id, (await db.StockMovements.SingleAsync()).Id);
            Assert.Equal(7, await new PartRepository(db).GetQuantityOnHandAsync(part.Id));
            var migratedBrand = await db.ProductBrands.SingleAsync(x => x.Id != ProductBrand.UnknownId);
            Assert.Equal("FEBI", migratedBrand.NormalizedName);
            Assert.Equal(migratedBrand.Id, (await db.Parts.SingleAsync()).BrandId);
            Assert.Equal("AB-12", (await db.Set<CatalogueReference>().SingleAsync(r => r.Id == referenceId)).IdentityKey);
            Assert.Equal("A2045012782", (await db.Set<CatalogueReference>().SingleAsync(r => r.Id == mercedesId)).IdentityKey);
            var link = await db.Set<SkuReference>().SingleAsync();
            Assert.True(link.IsActive);
            Assert.Equal(0, link.Revision);
            Assert.Equal(actor.UserId, link.LinkedByUserId);
            Assert.Empty(await db.Set<ReferenceLinkEvent>().ToListAsync()); // No invented legacy audit.
            var service = new ReferenceService(db, actor);
            await service.AddAsync(part.Id, new(ReferenceKind.Manufacturer, null, "AB12"));
            await service.SetStateAsync(part.Id, referenceId, new(false, "Verified wrong legacy link", 0));
            Assert.Single(await service.HistoryAsync(part.Id, referenceId));
            Assert.Equal(7, await new PartRepository(db).GetQuantityOnHandAsync(part.Id));
        }
        finally { await db.Database.ExecuteSqlRawAsync($"DROP SCHEMA \"{schema}\" CASCADE"); }
#pragma warning restore EF1002
    }
}
