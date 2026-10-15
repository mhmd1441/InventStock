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
    public DbSet<BrandMergeEvent> BrandMergeEvents => Set<BrandMergeEvent>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<SupplierCostObservation> SupplierCostObservations => Set<SupplierCostObservation>();
    public DbSet<PricingPolicy> PricingPolicies => Set<PricingPolicy>();
    public DbSet<CutHoseSellingRate> CutHoseSellingRates => Set<CutHoseSellingRate>();
    public DbSet<StockHoldMovement> StockHoldMovements => Set<StockHoldMovement>();
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
    public DbSet<PurchaseReceiptLine> PurchaseReceiptLines => Set<PurchaseReceiptLine>();
    public DbSet<SupplierImportBatch> SupplierImportBatches => Set<SupplierImportBatch>();
    public DbSet<SupplierImportRow> SupplierImportRows => Set<SupplierImportRow>();
    public DbSet<CashAccount> CashAccounts => Set<CashAccount>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<CashOpeningBalance> CashOpeningBalances => Set<CashOpeningBalance>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<OutgoingPayment> OutgoingPayments => Set<OutgoingPayment>();
    public DbSet<OutgoingPaymentReversal> OutgoingPaymentReversals => Set<OutgoingPaymentReversal>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<SaleTender> SaleTenders => Set<SaleTender>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnLine> SaleReturnLines => Set<SaleReturnLine>();
    public DbSet<SaleRefundTender> SaleRefundTenders => Set<SaleRefundTender>();
    public DbSet<LegacySaleReturn> LegacySaleReturns => Set<LegacySaleReturn>();
    public DbSet<LegacySaleReturnLine> LegacySaleReturnLines => Set<LegacySaleReturnLine>();
    public DbSet<LegacySaleRefundTender> LegacySaleRefundTenders => Set<LegacySaleRefundTender>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAccountEntry> CustomerAccountEntries => Set<CustomerAccountEntry>();
    public DbSet<CustomerProfileEvent> CustomerProfileEvents => Set<CustomerProfileEvent>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
    public DbSet<CustomerPaymentTender> CustomerPaymentTenders => Set<CustomerPaymentTender>();
    public DbSet<CustomerPaymentCorrection> CustomerPaymentCorrections => Set<CustomerPaymentCorrection>();

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
        if (ChangeTracker.Entries().Any(e => e.State is EntityState.Modified or EntityState.Deleted &&
            e.Entity is Sale or SaleLine or SaleTender or SaleReturn or SaleReturnLine or SaleRefundTender
                or LegacySaleReturn or LegacySaleReturnLine or LegacySaleRefundTender
                or CustomerAccountEntry or StockMovement or CustomerProfileEvent
                or CustomerPayment or CustomerPaymentTender or CustomerPaymentCorrection))
            throw new InvalidOperationException("Posted sale and ledger records cannot be edited or deleted.");
        if (ChangeTracker.Entries<ReferenceLinkEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Reference audit events cannot be edited or deleted.");
        if (ChangeTracker.Entries<SupplierCostObservation>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Supplier cost history cannot be edited or deleted; add a newer observation.");
        if (ChangeTracker.Entries<CutHoseSellingRate>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Cut-hose selling-rate history cannot be edited or deleted; add a newer rate.");
        if (ChangeTracker.Entries<BrandMergeEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Brand merge history cannot be edited or deleted.");
        if (ChangeTracker.Entries<StockHoldMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Stock hold history cannot be edited or deleted.");
        if (ChangeTracker.Entries<PurchaseReceipt>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<PurchaseReceiptLine>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Posted purchase receipts cannot be edited or deleted; post a correcting receipt or stock adjustment.");
        if (ChangeTracker.Entries<SupplierImportBatch>().Any(e => e.State == EntityState.Deleted) ||
            ChangeTracker.Entries<SupplierImportRow>().Any(e => e.State == EntityState.Deleted))
            throw new InvalidOperationException("Supplier import evidence cannot be deleted.");
        if (ChangeTracker.Entries<CashOpeningBalance>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<CashMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<OutgoingPayment>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<OutgoingPaymentReversal>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Posted cash records cannot be edited or deleted; create a linked reversal.");
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
