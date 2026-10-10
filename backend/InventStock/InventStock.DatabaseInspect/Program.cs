using System.Text.Json;
using Npgsql;

// Deliberately does not start the API, construct a DbContext, migrate, or seed.
var apiDirectory = Path.GetFullPath(args.Length > 0 ? args[0] : "InventStock.Api");
string? connectionString = null;
void ReadSetting(string path)
{
    if (!File.Exists(path)) return;
    using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
    if (json.RootElement.TryGetProperty("ConnectionStrings:DefaultConnection", out var flat)) connectionString = flat.GetString();
    if (json.RootElement.TryGetProperty("ConnectionStrings", out var section) && section.TryGetProperty("DefaultConnection", out var nested)) connectionString = nested.GetString();
}
ReadSetting(Path.Combine(apiDirectory, "appsettings.json"));
ReadSetting(Path.Combine(apiDirectory, "appsettings.Development.json"));
ReadSetting(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", "6ab48418-5131-4613-80c2-d0e6510997bc", "secrets.json"));
connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? connectionString;
if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("No local connection configured.");
var builder = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 5, CommandTimeout = 15, IncludeErrorDetail = false };
if (builder.Host is not ("localhost" or "127.0.0.1" or "::1"))
    throw new InvalidOperationException("This inspection tool permits local database hosts only.");
await using var connection = new NpgsqlConnection(builder.ConnectionString);
try { await connection.OpenAsync(); }
catch (Exception ex) { Console.WriteLine($"Inspection connection failed ({ex.GetType().Name}); credentials omitted."); return 1; }
await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction)) await readOnly.ExecuteNonQueryAsync();
async Task Report(string title, string sql)
{
    Console.WriteLine(title);
    await using var command = new NpgsqlCommand(sql, connection, transaction);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) Console.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString())));
}
async Task<bool> Exists(string name)
{
    await using var command = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", connection, transaction);
    command.Parameters.AddWithValue("name", "\"" + name + "\"");
    return (bool)(await command.ExecuteScalarAsync())!;
}
await Report("Database (read-only snapshot)", "SELECT current_database(), current_setting('server_version'), current_setting('transaction_read_only')");
if (await Exists("__EFMigrationsHistory")) await Report("Applied migrations", "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1");
if (await Exists("Parts")) await Report("Part count", "SELECT count(*) FROM \"Parts\"");
if (await Exists("StockMovements")) await Report("Movement count / net quantity", "SELECT count(*), coalesce(sum(\"QuantityChange\"),0) FROM \"StockMovements\"");
if (await Exists("CatalogueReferences"))
{
    await Report("Reference count", "SELECT count(*) FROM \"CatalogueReferences\"");
    await Report("Generic references potentially affected by old separator identity (review candidates, NOT proven collapses)", """
        SELECT "Id", "Kind", "Issuer", "Value", "SearchValue" FROM "CatalogueReferences"
        WHERE "Kind" <> 'MercedesOE' ORDER BY "Id"
        """);
    Console.WriteLine("Previous alternate submitted spellings were not retained. Actual collapsed references cannot be reconstructed from these rows.");
}
await transaction.RollbackAsync();
return 0;
