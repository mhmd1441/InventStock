using Npgsql;

namespace InventStock.Tests.Infrastructure;

/// <summary>Keep shared PostgreSQL extension objects out of per-test schemas.</summary>
internal static class TestPostgresExtensions
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool ready;

    public static async Task EnsurePublicAsync(string? configured)
    {
        var builder = new NpgsqlConnectionStringBuilder(configured);
        if (builder.Database != "inventstock_test")
            throw new InvalidOperationException("Only disposable inventstock_test is allowed.");
        await Gate.WaitAsync();
        try
        {
            if (ready) return;
            builder.SearchPath = "public";
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public; ALTER EXTENSION pg_trgm SET SCHEMA public;", connection);
            await command.ExecuteNonQueryAsync();
            ready = true;
        }
        finally { Gate.Release(); }
    }
}
