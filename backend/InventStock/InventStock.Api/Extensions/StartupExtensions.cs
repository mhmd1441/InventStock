using InventStock.Infrastructure.Data;
using InventStock.Infrastructure.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace InventStock.Api.Extensions;

public static class StartupExtensions
{
    public static IServiceCollection AddSwaggerGenWithBearer(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "InventStock API",
                Version = "v1",
                Description = "Inventory and parts management API."
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token returned by /api/auth/login.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
        });

    /// <summary>
    /// Fails fast rather than starting a production instance with development placeholders in place.
    /// </summary>
    public static void ValidateProductionConfiguration(this WebApplication app, string? signingKey)
    {
        if (!app.Environment.IsProduction())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 characters outside development.");
        }

        if (signingKey.Contains("development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The development JWT signing key must not be used in production.");
        }

        if (app.Configuration.GetValue<bool>("Seed:IncludeDemoData"))
        {
            throw new InvalidOperationException("Seed:IncludeDemoData cannot be enabled in Production.");
        }

        var origins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is null || origins.Length == 0)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must list the front-end origins in production.");
        }
    }

    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:MigrateOnStartup", false))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
            SeedSafety.EnsureDemoSeedIsAllowed(app.Environment.IsProduction(), options);
            await db.Database.MigrateAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(options);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration or seeding failed.");
            throw;
        }
    }
}
