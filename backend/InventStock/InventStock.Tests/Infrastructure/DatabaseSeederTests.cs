using InventStock.Infrastructure.Data.Seed;
using InventStock.Infrastructure.Identity;
using InventStock.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InventStock.Tests.Infrastructure;

public sealed class DatabaseSeederTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Empty_seed_options_have_no_default_administrator_credentials()
    {
        var options = new SeedOptions();

        Assert.Equal(string.Empty, options.AdminEmail);
        Assert.Equal(string.Empty, options.AdminPassword);
        Assert.False(options.IncludeDemoData);
    }

    [Fact]
    public void Production_rejects_demo_seeding_before_any_seed_work()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SeedSafety.EnsureDemoSeedIsAllowed(isProduction: true, new SeedOptions { IncludeDemoData = true }));

        Assert.Contains("IncludeDemoData", error.Message, StringComparison.Ordinal);
        Assert.Contains("Production", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_admin_seed_is_idempotent_does_not_reset_password_and_never_logs_it()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventStock.Infrastructure.Data.AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<ApplicationRole>>();
        var logger = new CapturingLogger<DatabaseSeeder>();
        var seeder = new DatabaseSeeder(db, users, roles, logger);
        var email = $"bootstrap-{Guid.NewGuid():N}@inventstock.local";
        const string originalPassword = "Bootstrap!Password2026";
        const string attemptedReplacement = "NeverLogThisReplacement!2026";

        await seeder.SeedAsync(new SeedOptions
        {
            AdminEmail = email,
            AdminFullName = "Bootstrap owner",
            AdminPassword = originalPassword,
            IncludeDemoData = false
        });
        var original = await users.FindByEmailAsync(email);

        await seeder.SeedAsync(new SeedOptions
        {
            AdminEmail = email,
            AdminFullName = "Changed display name must not matter",
            AdminPassword = attemptedReplacement,
            IncludeDemoData = false
        });
        var afterRestart = await users.FindByEmailAsync(email);

        Assert.NotNull(original);
        Assert.NotNull(afterRestart);
        Assert.Equal(original.Id, afterRestart.Id);
        Assert.True(await users.CheckPasswordAsync(afterRestart, originalPassword));
        Assert.False(await users.CheckPasswordAsync(afterRestart, attemptedReplacement));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(originalPassword, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(attemptedReplacement, StringComparison.Ordinal));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
