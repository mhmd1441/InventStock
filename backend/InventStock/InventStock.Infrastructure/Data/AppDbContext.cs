using System.Reflection;
using InventStock.Application.Interfaces;
using InventStock.Domain.Common;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ICurrentUserService? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Part> Parts => Set<Part>();

    public DbSet<ProductBrand> ProductBrands => Set<ProductBrand>();

    public DbSet<BrandAlias> BrandAliases => Set<BrandAlias>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("pg_trgm");
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    private void ApplyAuditInformation()
    {
        if (ChangeTracker.Entries<ReferenceLinkEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Reference audit events cannot be edited or deleted.");
        var now = DateTime.UtcNow;
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedByUserId = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedByUserId = userId;
                    break;
            }
        }
    }
}
