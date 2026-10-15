using InventStock.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using InventStock.Tests.Infrastructure;

namespace InventStock.Tests.Api;

/// <summary>Uses the real PostgreSQL constraints and decimals, including for HTTP authorization/privacy tests.</summary>
public sealed class PostgresSaleApiFactory : ApiFactory
{
    private readonly string schema = "sale_api_" + Guid.NewGuid().ToString("N");
    private bool created;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var configured = Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidOperationException("Sale HTTP tests require disposable PostgreSQL.");
        var connection = new NpgsqlConnectionStringBuilder(configured);
        if (connection.Database != "inventstock_test") throw new InvalidOperationException("Only disposable inventstock_test is allowed.");
        connection.SearchPath = schema + ",public";
        builder.UseEnvironment(Environments.Staging);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection.ConnectionString,
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", schema)));
        });
    }

    protected override async Task PrepareDatabaseAsync(AppDbContext db)
    {
        await TestPostgresExtensions.EnsurePublicAsync(Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_POSTGRES"));
#pragma warning disable EF1002 // Private generated schema, never supplied by a request.
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{schema}\"");
#pragma warning restore EF1002
        created = true;
        await db.Database.MigrateAsync();
    }

    public override async Task DisposeAsync()
    {
        if (created)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"DROP SCHEMA \"{schema}\" CASCADE");
#pragma warning restore EF1002
        }
        await base.DisposeAsync();
    }
}
