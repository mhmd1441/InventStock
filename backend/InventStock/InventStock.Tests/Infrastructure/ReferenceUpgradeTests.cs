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
    public async Task PopulatedReviewedSupplierImports_UpgradesAllPendingMigrationsWithoutChangingCatalogueOrStock()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_POSTGRES"));
        Assert.Equal("inventstock_test", builder.Database);
        await TestPostgresSchemas.EnsurePublicAsync();
        var schema = "reviewed_upgrade_" + Guid.NewGuid().ToString("N");
        builder.SearchPath = schema + ",public";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options;
        var actor = new TestCurrentUser(Guid.NewGuid());
        await TestPostgresSchemas.MigrateAsync(schema, "20261012143056_ReviewedSupplierImports");
        await using var db = new AppDbContext(options, actor);
#pragma warning disable EF1002 // Schema identifier is exclusively a generated GUID with a fixed prefix.
        try
        {
            const string reviewedImports = "20261012143056_ReviewedSupplierImports";

            var categoryId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var brandId = Guid.NewGuid();
            var piecePartId = Guid.NewGuid();
            var hosePartId = Guid.NewGuid();
            var pieceInId = Guid.NewGuid();
            var pieceOutId = Guid.NewGuid();
            var hoseInId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var categoryName = "Legacy inventory";
            var supplierName = "Legacy Supplier";
            var brandName = "FEBI";
            var pieceNumber = "2045012782";
            var pieceName = "Coolant hose";
            var pieceVariant = "Includes seal";
            var hoseNumber = "R8-HOSE";
            var hoseName = "Oil hose per metre";
            var empty = string.Empty;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Categories" ("Id", "Name", "Description", "CreatedAtUtc")
                VALUES ({categoryId}, {categoryName}, 'Populated upgrade rehearsal', {now});
                INSERT INTO "Suppliers" ("Id", "Name", "IsActive", "CreatedAtUtc")
                VALUES ({supplierId}, {supplierName}, TRUE, {now});
                INSERT INTO "ProductBrands" ("Id", "Name", "NormalizedName", "IsActive", "CreatedAtUtc")
                VALUES ({brandId}, {brandName}, {brandName}, TRUE, {now});
                INSERT INTO "Parts" ("Id", "PartNumber", "Name", "Brand", "BrandId", "Variant", "CategoryId",
                    "SupplierId", "UnitPrice", "LowStockThreshold", "StockUnit", "IsActive", "CreatedAtUtc")
                VALUES ({piecePartId}, {pieceNumber}, {pieceName}, {brandName}, {brandId}, {pieceVariant},
                        {categoryId}, {supplierId}, {15.00m}, 1, 'Piece', TRUE, {now}),
                       ({hosePartId}, {hoseNumber}, {hoseName}, {brandName}, {brandId}, {empty},
                        {categoryId}, {supplierId}, {6.00m}, 100, 'Centimeter', TRUE, {now});
                INSERT INTO "StockMovements" ("Id", "PartId", "MovementType", "QuantityChange", "Reason",
                    "PerformedByUserId", "OccurredAtUtc", "CreatedAtUtc")
                VALUES ({pieceInId}, {piecePartId}, 'In', 7, 'Legacy receipt', {actor.UserId!.Value}, {now}, {now}),
                       ({pieceOutId}, {piecePartId}, 'Out', -2, 'Legacy sale', {actor.UserId.Value}, {now}, {now}),
                       ({hoseInId}, {hosePartId}, 'In', 300, 'Legacy opening length', {actor.UserId.Value}, {now}, {now});
                """);

            var beforeIds = new[] { piecePartId, hosePartId };
            var beforeMovementIds = new[] { pieceInId, pieceOutId, hoseInId };
            await TestPostgresSchemas.MigrateExistingSchemaAsync(schema);
            db.ChangeTracker.Clear();

            Assert.Equal(beforeIds.Order(), (await db.Parts.Select(x => x.Id).ToListAsync()).Order());
            Assert.Equal(beforeMovementIds.Order(), (await db.StockMovements.Select(x => x.Id).ToListAsync()).Order());
            Assert.Equal(categoryId, (await db.Parts.SingleAsync(x => x.Id == piecePartId)).CategoryId);
            Assert.Equal(supplierId, (await db.Parts.SingleAsync(x => x.Id == piecePartId)).SupplierId);
            Assert.Equal(brandId, (await db.Parts.SingleAsync(x => x.Id == piecePartId)).BrandId);
            var repository = new PartRepository(db);
            Assert.Equal(5, await repository.GetQuantityOnHandAsync(piecePartId));
            Assert.Equal(300, await repository.GetQuantityOnHandAsync(hosePartId));
            Assert.False((await db.Parts.SingleAsync(x => x.Id == piecePartId)).NeedsRecount);

            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var reviewedIndex = Array.IndexOf(applied, reviewedImports);
            Assert.True(reviewedIndex >= 0, "The populated upgrade baseline must remain in the applied migration chain.");
            Assert.Equal(new[]
            {
                "20261012152847_CashFoundation",
                "20261012203135_AtomicCounterSales",
                "20261013074603_CustomerAccountsAndPayments",
                "20261013080730_SaleStockDiscrepancy",
                "20261013142722_CustomerOpeningCorrections",
                "20261013151728_LinkedSaleReturns",
                "20261014075323_SuggestedSellingPrice",
                "20261014081617_SalePriceGuidanceSnapshot",
                "20261014082611_DirectCutHoseSellingRate",
                "20261015174820_SaleLookupIndexes",
                "20261015180443_LegacySaleReturns",
                "20261017171314_AtomicSaleExchanges",
                "20261017203857_DailyCashReconciliation",
                "20261017211043_CashReconciliationCorrections",
                "20261017212401_SupplierPayments",
                "20261018074320_FinalizePartialCashReconciliations",
                "20261018080125_CashAccountTransfers",
                "20261018084113_SaleTenderAccountCorrections",
                "20261018084631_SaleTenderAccountCorrectionReversal",
                "20261018084816_ExpandCashMovementType",
                "20261018114500_AddCashTransferSourceAccountIndex",
                "20261020180251_PaperRecoveryEntries",
                "20261020182119_InternalSaleReceipts"
            }, applied[(reviewedIndex + 1)..]);
        }
        finally { await TestPostgresSchemas.DropAsync(schema); }
#pragma warning restore EF1002
    }

    [PostgresFact]
    public async Task PopulatedPreviousSchema_PreservesIdsStockAndReferenceLinks()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_POSTGRES"));
        Assert.Equal("inventstock_test", builder.Database);
        await TestPostgresSchemas.EnsurePublicAsync();
        var schema = "upgrade_test_" + Guid.NewGuid().ToString("N");
        builder.SearchPath = schema + ",public";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options;
        var actor = new TestCurrentUser(Guid.NewGuid());
        await TestPostgresSchemas.MigrateAsync(schema, "20261010205239_ReferenceIdentityAndAudit");
        await using var db = new AppDbContext(options, actor);
#pragma warning disable EF1002 // Schema identifier is exclusively a generated GUID with a fixed prefix.
        try
        {
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
            await TestPostgresSchemas.MigrateExistingSchemaAsync(schema);
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
        finally { await TestPostgresSchemas.DropAsync(schema); }
#pragma warning restore EF1002
    }
}
