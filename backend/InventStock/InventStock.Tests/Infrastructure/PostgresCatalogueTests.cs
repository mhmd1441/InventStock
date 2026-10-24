using System.Data.Common;
using System.Diagnostics;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using InventStock.Application.Services;
using InventStock.Domain.Entities;
using InventStock.Domain.Exceptions;
using InventStock.Infrastructure.Data;
using InventStock.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit.Abstractions;

namespace InventStock.Tests.Infrastructure;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_POSTGRES")))
            Skip = "Set INVENTSTOCK_TEST_POSTGRES to a disposable database named inventstock_test.";
    }
}

public sealed class PostgresCatalogueFixture : IAsyncLifetime
{
    private readonly string _schema = "catalogue_test_" + Guid.NewGuid().ToString("N");
    private string? _connectionString;
    private bool _created;
    public Guid CategoryId { get; private set; }

    public AppDbContext Open(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", _schema))
            .AddInterceptors(interceptors).Options;
        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var builder = new NpgsqlConnectionStringBuilder(configured);
        if (builder.Database != "inventstock_test")
            throw new InvalidOperationException("Tests require a disposable database named inventstock_test. Never use a shop database.");
        await TestPostgresSchemas.EnsurePublicAsync();
        builder.SearchPath = _schema + ",public";
        builder.Timeout = 10;
        builder.CommandTimeout = 30;
        _connectionString = builder.ConnectionString;
        await TestPostgresSchemas.MigrateAsync(_schema);
        _created = true;
        await using var db = Open();
        var category = Category.Create("Benchmark catalogue", null);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        CategoryId = category.Id;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Parts" ("Id", "PartNumber", "Name", "CategoryId", "UnitPrice", "LowStockThreshold", "IsActive", "CreatedAtUtc")
            SELECT gen_random_uuid(), (1000000000 + n)::text, 'Coolant hose ' || n, {CategoryId}, 15, 5, true, now()
            FROM generate_series(1, 20000) AS n
            """);
        var actor = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "StockMovements" ("Id", "PartId", "MovementType", "QuantityChange", "PerformedByUserId", "OccurredAtUtc", "CreatedAtUtc")
            SELECT gen_random_uuid(), p."Id", 'In', 10, {actor}, now(), now()
            FROM "Parts" p CROSS JOIN generate_series(1, 5)
            """);
        await db.Database.ExecuteSqlRawAsync("ANALYZE \"Parts\"");
        await db.Database.ExecuteSqlRawAsync("ANALYZE \"StockMovements\"");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "CatalogueReferences" ("Id", "Kind", "Issuer", "Value", "IdentityKey", "SearchValue", "MatchValue", "CreatedAtUtc")
            SELECT "Id", 'MercedesOE', 'MERCEDES', 'A' || "PartNumber", 'A' || "PartNumber", 'A' || "PartNumber", "PartNumber", now() FROM "Parts";
            INSERT INTO "SkuReferences" ("PartId", "ReferenceId", "LinkedAtUtc", "Revision")
            SELECT "Id", "Id", now(), 0 FROM "Parts";
            -- Hostile reference cardinality: 13 references/SKU (260k total), with every
            -- fiftieth reference deliberately shared by multiple SKUs.
            WITH source AS (
              SELECT p."Id" AS part_id, 'ALT-' || p."PartNumber" || '-' || slot AS value
              FROM "Parts" p CROSS JOIN generate_series(1, 12) slot
            ), refs AS (
              INSERT INTO "CatalogueReferences" ("Id", "Kind", "Issuer", "Value", "IdentityKey", "SearchValue", "MatchValue", "CreatedAtUtc")
              SELECT gen_random_uuid(), 'Competitor', 'BENCH', value, value,
                     replace(value, '-', ''), replace(value, '-', ''), now() FROM source
              RETURNING "Id", "Value"
            ) INSERT INTO "SkuReferences" ("PartId", "ReferenceId", "LinkedAtUtc", "Revision")
            SELECT source.part_id, refs."Id", now(), 0 FROM source JOIN refs ON refs."Value" = source.value;
            WITH source AS (
              SELECT p."Id" AS part_id, 'SHARED-FILTER-' || (p."PartNumber"::integer % 400) AS value,
                     'SHARED-FILTER-' || p."PartNumber" AS identity_key FROM "Parts" p
            ), refs AS (
              INSERT INTO "CatalogueReferences" ("Id", "Kind", "Issuer", "Value", "IdentityKey", "SearchValue", "MatchValue", "CreatedAtUtc")
              SELECT gen_random_uuid(), 'Competitor', 'BENCH', value, identity_key,
                     replace(value, '-', ''), replace(value, '-', ''), now() FROM source
              RETURNING "Id", "IdentityKey"
            ) INSERT INTO "SkuReferences" ("PartId", "ReferenceId", "LinkedAtUtc", "Revision")
            SELECT source.part_id, refs."Id", now(), 0 FROM source JOIN refs ON refs."IdentityKey" = source.identity_key;
            ANALYZE "CatalogueReferences";
            ANALYZE "SkuReferences";
            """);
    }

    public async Task DisposeAsync()
    {
        if (!_created) return;
        await TestPostgresSchemas.DropAsync(_schema);
    }
}

[Collection("PostgreSQL schema migrations")]
public sealed class PostgresCatalogueTests(PostgresCatalogueFixture fixture, ITestOutputHelper output)
    : IClassFixture<PostgresCatalogueFixture>
{
    private sealed class Commands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    [PostgresFact]
    public async Task CounterSearch_UsesTwoQueries_NoSupplierJoinOrPrivateColumns()
    {
        var commands = new Commands();
        await using var db = fixture.Open(commands);
        var results = await new PartRepository(db).SearchCounterAsync(new() { Search = "~/00582" });
        var expected = Assert.Single(results.Items.Where(x => x.PartNumber == "1000000582"));
        Assert.Equal(50, expected.QuantityOnHand);
        Assert.Equal(2, commands.Sql.Count); // total count and a bounded page, never per-item queries
        foreach (var sql in commands.Sql)
        {
            Assert.DoesNotContain("\"Suppliers\"", sql);
            Assert.DoesNotContain("\"UnitPrice\"", sql);
            Assert.DoesNotContain("\"SupplierId\"", sql);
        }
        Assert.Contains("LIKE", commands.Sql[0]);
    }

    [PostgresFact]
    public async Task SearchBenchmark_20000Parts_280000References_100000Movements()
    {
        await using var db = fixture.Open();
        var repository = new PartRepository(db);
        var cases = new[]
        {
            new PartQuery { Search = "1000000582", SearchMode = PartSearchMode.Exact },
            new PartQuery { Search = "A1000000582", SearchMode = PartSearchMode.Exact },
            new PartQuery { Search = "~/00582" },
            new PartQuery { Search = "1 000-000-582" },
            new PartQuery { Search = "fil", SearchMode = PartSearchMode.Contains },
            new PartQuery { Search = "000058", SearchMode = PartSearchMode.Contains },
            new PartQuery { Search = "filter", SearchMode = PartSearchMode.Contains, PageSize = 20 },
            new PartQuery { PageSize = 20 },
            new PartQuery { Page = 500, PageSize = 20 },
            new PartQuery { SortBy = PartSortField.QuantityOnHand, PageSize = 20 }
        };
        foreach (var query in cases)
        {
            await repository.SearchCounterAsync(query); // warm query compilation/connection
            var samples = new List<double>();
            for (var i = 0; i < 20; i++)
            {
                var timer = Stopwatch.StartNew();
                var page = await repository.SearchCounterAsync(query);
                samples.Add(timer.Elapsed.TotalMilliseconds);
                Assert.InRange(page.Items.Count, 0, 20);
            }
            samples.Sort();
            output.WriteLine($"{query.Search ?? query.SortBy.ToString()}: p50={samples[10]:F1}ms, p95={samples[18]:F1}ms, max={samples[19]:F1}ms");
            // Report the real latency; a hardware-independent 300ms CI gate would be misleading.
        }
    }

    [PostgresFact]
    public async Task SimultaneousLastUnitSales_OneCommitsOneConflicts()
    {
        await using var setup = fixture.Open();
        var part = Part.Create("RACE-" + Guid.NewGuid().ToString("N"), "Race test", null, fixture.CategoryId, null, 1, 0, null);
        setup.Parts.Add(part);
        setup.StockMovements.Add(StockMovement.StockIn(part.Id, 1, Guid.NewGuid(), "Opening", null));
        await setup.SaveChangesAsync();

        await using var blocker = fixture.Open();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await new PartRepository(blocker).LockForStockChangeAsync(part.Id);

        async Task<bool> Sell()
        {
            await using var db = fixture.Open();
            var service = new StockMovementService(new PartRepository(db), new StockMovementRepository(db),
                new UnitOfWork(db), new TestCurrentUser(Guid.NewGuid()), Substitute.For<IStockNotifier>(),
                NullLogger<StockMovementService>.Instance);
            try { await service.StockOutAsync(part.Id, new() { OperationId = Guid.NewGuid(), Quantity = 1, Reason = "Race test" }); return true; }
            catch (InsufficientStockException) { return false; }
        }
        var first = Sell();
        var second = Sell();
        // Prove both independent connections contend before releasing the stock lock.
        var waiting = 0;
        var timeout = Stopwatch.StartNew();
        while (waiting < 2 && timeout.Elapsed < TimeSpan.FromSeconds(8))
        {
            waiting = await setup.Database.SqlQueryRaw<int>("""
                SELECT count(*)::integer AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                AND query LIKE '%Parts%FOR UPDATE%'
                """).SingleAsync();
            if (waiting < 2) await Task.Delay(25);
        }
        await transaction.CommitAsync();
        var results = await Task.WhenAll(first, second);
        Assert.True(waiting >= 2, "Both stock changes must have contended for the row lock.");
        Assert.Single(results, succeeded => succeeded);
        Assert.Equal(0, await new PartRepository(setup).GetQuantityOnHandAsync(part.Id));
        Assert.Equal(2, await setup.StockMovements.CountAsync(m => m.PartId == part.Id));
    }

    [PostgresFact]
    public async Task ConcurrentDuplicateOperation_CommitsOneMovementAndReturnsSameOutcome()
    {
        await using var setup = fixture.Open();
        var part = Part.Create("RETRY-" + Guid.NewGuid().ToString("N"), "Retry test", null, fixture.CategoryId, null, 1, 0, null);
        setup.Parts.Add(part);
        await setup.SaveChangesAsync();
        var userId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        async Task<StockMovementDto> Submit()
        {
            await using var db = fixture.Open();
            var service = new StockMovementService(new PartRepository(db), new StockMovementRepository(db),
                new UnitOfWork(db), new TestCurrentUser(userId), Substitute.For<IStockNotifier>(),
                NullLogger<StockMovementService>.Instance);
            return await service.StockInAsync(part.Id, new() { OperationId = operationId, Quantity = 4, Reason = "Delivery" });
        }

        var results = await Task.WhenAll(Submit(), Submit());

        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(4, await new PartRepository(setup).GetQuantityOnHandAsync(part.Id));
        Assert.Single(await setup.StockMovements.Where(x => x.PartId == part.Id).ToListAsync());
    }
}
