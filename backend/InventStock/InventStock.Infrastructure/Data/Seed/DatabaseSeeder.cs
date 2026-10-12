using InventStock.Application.Common;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventStock.Infrastructure.Data.Seed;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = string.Empty;

    public string AdminFullName { get; set; } = "System Administrator";

    public string AdminPassword { get; set; } = string.Empty;

    public bool IncludeDemoData { get; set; }
}

public sealed class DatabaseSeeder
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedCashFoundationAsync(cancellationToken);
        var adminId = await SeedAdminAsync(options);

        if (options.IncludeDemoData && adminId.HasValue)
        {
            await SeedDemoCatalogAsync(adminId.Value, cancellationToken);
        }
    }

    private async Task SeedCashFoundationAsync(CancellationToken cancellationToken)
    {
        var existingAccountCodes = await _db.CashAccounts.Select(x => x.Code).ToListAsync(cancellationToken);
        var accounts = new[]
        {
            CashAccount.Create("SAFE_USD", "Physical safe — USD", Domain.Enums.MoneyCurrency.USD, Domain.Enums.CashAccountKind.PhysicalSafe),
            CashAccount.Create("SAFE_LBP", "Physical safe — LBP", Domain.Enums.MoneyCurrency.LBP, Domain.Enums.CashAccountKind.PhysicalSafe),
            CashAccount.Create("SAFE_EUR", "Physical safe — EUR", Domain.Enums.MoneyCurrency.EUR, Domain.Enums.CashAccountKind.PhysicalSafe),
            CashAccount.Create("WHISH_USD", "Whish wallet — USD", Domain.Enums.MoneyCurrency.USD, Domain.Enums.CashAccountKind.Whish),
            CashAccount.Create("WHISH_LBP", "Whish wallet — LBP", Domain.Enums.MoneyCurrency.LBP, Domain.Enums.CashAccountKind.Whish)
        }.Where(x => !existingAccountCodes.Contains(x.Code)).ToArray();
        if (accounts.Length > 0) _db.CashAccounts.AddRange(accounts);

        var categoryNames = new[]
        {
            "Municipality / government fees", "Utilities", "Internet installation / subscription",
            "Professional fees", "Shop refreshments", "Personal / family withdrawal", "Charity", "Other"
        };
        var existingCategoryNames = await _db.ExpenseCategories.Select(x => x.Name).ToListAsync(cancellationToken);
        var categories = categoryNames.Where(x => !existingCategoryNames.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Select(ExpenseCategory.Create).ToArray();
        if (categories.Length > 0) _db.ExpenseCategories.AddRange(categories);

        if (accounts.Length > 0 || categories.Length > 0) await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in RoleNames.All)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole(role));
                _logger.LogInformation("Created role {Role}.", role);
            }
        }
    }

    private async Task<Guid?> SeedAdminAsync(SeedOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            _logger.LogWarning(
                "Seed:AdminEmail and Seed:AdminPassword are not configured, so no administrator was created.");
            return null;
        }

        var existing = await _userManager.FindByEmailAsync(options.AdminEmail);
        if (existing is not null)
        {
            return existing.Id;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = options.AdminEmail,
            Email = options.AdminEmail,
            FullName = options.AdminFullName,
            EmailConfirmed = true
        };

        var created = await _userManager.CreateAsync(admin, options.AdminPassword);
        if (!created.Succeeded)
        {
            _logger.LogError(
                "Failed to create the seed administrator: {Errors}",
                string.Join(" ", created.Errors.Select(e => e.Description)));
            return null;
        }

        await _userManager.AddToRoleAsync(admin, RoleNames.Admin);
        _logger.LogInformation("Created seed administrator {Email}.", options.AdminEmail);

        return admin.Id;
    }

    private async Task SeedDemoCatalogAsync(Guid adminId, CancellationToken cancellationToken)
    {
        if (await _db.Parts.AnyAsync(cancellationToken))
        {
            return;
        }

        var brakes = Category.Create("Brakes", "Brake pads, discs and related hardware");
        var filters = Category.Create("Filters", "Oil, air and cabin filters");
        var electrical = Category.Create("Electrical", "Batteries, alternators and sensors");
        _db.Categories.AddRange(brakes, filters, electrical);

        var bosch = Supplier.Create("Bosch Middle East", "Rami Haddad", "orders@bosch-me.example", "+961 1 000 111", "Beirut, Lebanon");
        var denso = Supplier.Create("Denso Parts Co.", "Yara Khalil", "sales@denso-parts.example", "+961 1 222 333", "Tripoli, Lebanon");
        _db.Suppliers.AddRange(bosch, denso);

        await _db.SaveChangesAsync(cancellationToken);

        var parts = new[]
        {
            Part.Create("BRK-1001", "Front Brake Pad Set", "Ceramic front brake pads", brakes.Id, bosch.Id, 42.50m, 10, "7501234567890"),
            Part.Create("BRK-1002", "Rear Brake Disc", "Vented rear brake disc, 280mm", brakes.Id, bosch.Id, 65.00m, 6, null),
            Part.Create("FLT-2001", "Oil Filter", "Spin-on oil filter", filters.Id, denso.Id, 8.75m, 25, "7501234567891"),
            Part.Create("FLT-2002", "Cabin Air Filter", "Activated carbon cabin filter", filters.Id, denso.Id, 14.20m, 15, null),
            Part.Create("ELC-3001", "12V Battery 70Ah", "Maintenance-free lead-acid battery", electrical.Id, bosch.Id, 120.00m, 4, null),
            Part.Create("ELC-3002", "Oxygen Sensor", "Upstream lambda sensor", electrical.Id, denso.Id, 58.90m, 8, null)
        };

        _db.Parts.AddRange(parts);
        await _db.SaveChangesAsync(cancellationToken);

        var openingStock = new[] { 40, 12, 60, 30, 3, 9 };
        for (var i = 0; i < parts.Length; i++)
        {
            _db.StockMovements.Add(
                StockMovement.StockIn(parts[i].Id, openingStock[i], adminId, "Opening stock", "SEED-001"));
        }

        _db.StockMovements.Add(StockMovement.StockOut(parts[0].Id, 6, adminId, "Workshop issue", "WO-1042"));
        _db.StockMovements.Add(StockMovement.StockOut(parts[2].Id, 18, adminId, "Workshop issue", "WO-1043"));
        _db.StockMovements.Add(StockMovement.Adjustment(parts[3].Id, -2, adminId, "Damaged in storage", null));

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded demo catalogue with {PartCount} parts.", parts.Length);
    }
}
