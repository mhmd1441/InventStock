using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventStock.Application.Common;
using InventStock.Application.DTOs.Auth;
using InventStock.Infrastructure.Data;
using InventStock.Infrastructure.Data.Seed;
using InventStock.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace InventStock.Tests.Api;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@inventstock.local";
    public const string AdminPassword = "Adm!nPassw0rd2026";
    public const string StaffEmail = "staff@inventstock.local";
    public const string StaffPassword = "St@ffPassw0rd2026";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        // The API reads configuration while composing services, which happens before any source this
        // factory could add takes effect, so the test settings have to be in the environment first.
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings__DefaultConnection"] = "Host=localhost;Database=unused;Username=test;Password=test",
            ["Jwt__Issuer"] = "InventStock.Tests",
            ["Jwt__Audience"] = "InventStock.Tests",
            ["Jwt__SigningKey"] = "integration-test-signing-key-that-is-long-enough",
            ["Jwt__AccessTokenMinutes"] = "15",
            ["Jwt__RefreshTokenDays"] = "1",
            ["Database__MigrateOnStartup"] = "false",
            ["Cors__AllowedOrigins__0"] = "http://localhost:3000"
        };

        foreach (var (key, value) in settings)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Staging);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            _connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var seeder = provider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(new SeedOptions
        {
            AdminEmail = AdminEmail,
            AdminPassword = AdminPassword,
            AdminFullName = "Integration Admin",
            IncludeDemoData = true
        });

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var staff = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = StaffEmail,
            Email = StaffEmail,
            FullName = "Integration Staff",
            EmailConfirmed = true
        };

        await userManager.CreateAsync(staff, StaffPassword);
        await userManager.AddToRoleAsync(staff, RoleNames.Staff);
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task<HttpClient> CreateClientForAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    public Task<HttpClient> CreateAdminClientAsync() => CreateClientForAsync(AdminEmail, AdminPassword);

    public Task<HttpClient> CreateStaffClientAsync() => CreateClientForAsync(StaffEmail, StaffPassword);
}
