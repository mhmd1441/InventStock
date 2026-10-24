using InventStock.Application.Interfaces;
using InventStock.Application.Interfaces.Repositories;
using InventStock.Infrastructure.Data;
using InventStock.Infrastructure.Data.Seed;
using InventStock.Infrastructure.Identity;
using InventStock.Infrastructure.Repositories;
using InventStock.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventStock.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                // Explicit business transactions must not be replayed without idempotency.
            }));

        services.AddIdentityCore(configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPartRepository, PartRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<ISupplierCostRepository, SupplierCostRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();

        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IReferenceService, ReferenceService>();
        services.AddScoped<IStockHoldService, StockHoldService>();
        services.AddScoped<IReceivingService, ReceivingService>();
        services.AddScoped<ISupplierImportService, SupplierImportService>();
        services.AddScoped<ICashService, CashService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<ISaleReturnService, SaleReturnService>();
        services.AddScoped<ISaleExchangeService, SaleExchangeService>();
        services.AddScoped<ITransactionExceptionService, TransactionExceptionService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    private static void AddIdentityCore(this IServiceCollection services, IConfiguration configuration)
    {
        // AddIdentityCore rather than AddIdentity: this API authenticates with bearer tokens, so the
        // cookie authentication schemes AddIdentity registers would be dead weight that also hijacks
        // 401 responses into login redirects.
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();
    }
}
