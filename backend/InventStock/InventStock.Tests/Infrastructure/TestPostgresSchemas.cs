using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace InventStock.Tests.Infrastructure;

/// <summary>
/// Keeps test DDL on the disposable migrator connection while the test host uses
/// the least-privileged runtime connection. This mirrors the production split.
/// </summary>
internal static class TestPostgresSchemas
{
    public static string RuntimeConnection(string? configured)
    {
        var runtime = new NpgsqlConnectionStringBuilder(configured);
        if (runtime.Database != "inventstock_test")
            throw new InvalidOperationException("Only disposable inventstock_test is allowed.");
        return runtime.ConnectionString;
    }

    private static string MigratorConnection()
    {
        var configured = Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_MIGRATOR_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("Split-role PostgreSQL tests require INVENTSTOCK_TEST_MIGRATOR_POSTGRES.");
        var migrator = new NpgsqlConnectionStringBuilder(configured);
        if (migrator.Database != "inventstock_test")
            throw new InvalidOperationException("Only disposable inventstock_test is allowed.");
        return migrator.ConnectionString;
    }

    public static async Task EnsurePublicAsync()
        => await TestPostgresExtensions.EnsurePublicAsync(MigratorConnection());

    public static async Task MigrateAsync(string schema, string? targetMigration = null)
    {
        await EnsurePublicAsync();
        var builder = new NpgsqlConnectionStringBuilder(MigratorConnection()) { SearchPath = schema + ",public" };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(builder.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;
        await using var db = new AppDbContext(options);
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{schema}\"");
#pragma warning restore EF1002
        if (targetMigration is null)
            await db.Database.MigrateAsync();
        else
            await db.GetService<IMigrator>().MigrateAsync(targetMigration);
        await GrantRuntimeAsync(schema);
    }

    public static async Task MigrateExistingSchemaAsync(string schema)
    {
        var builder = new NpgsqlConnectionStringBuilder(MigratorConnection()) { SearchPath = schema + ",public" };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(builder.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await GrantRuntimeAsync(schema);
    }

    public static async Task DropAsync(string schema)
    {
        var builder = new NpgsqlConnectionStringBuilder(MigratorConnection()) { SearchPath = "public" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
#pragma warning disable EF1002
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", connection);
#pragma warning restore EF1002
        await command.ExecuteNonQueryAsync();
    }

    private static async Task GrantRuntimeAsync(string schema)
    {
        var builder = new NpgsqlConnectionStringBuilder(MigratorConnection()) { SearchPath = "public" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
#pragma warning disable EF1002
        await using var command = new NpgsqlCommand($"""
            GRANT USAGE ON SCHEMA "{schema}" TO inventstock_app;
            GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA "{schema}" TO inventstock_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA "{schema}" TO inventstock_app;
            GRANT DELETE ON TABLE "{schema}"."AspNetUserRoles", "{schema}"."AspNetUserClaims",
                "{schema}"."AspNetUserLogins", "{schema}"."AspNetUserTokens", "{schema}"."RefreshTokens" TO inventstock_app;
            """, connection);
#pragma warning restore EF1002
        await command.ExecuteNonQueryAsync();
    }
}
