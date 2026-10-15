using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> b)
    {
        b.ToTable("SaleReturns", t => t.HasCheckConstraint("CK_SaleReturns_Settlement",
            "\"TotalUsd\" > 0 AND \"AccountReductionUsd\" >= 0 AND \"AccountReductionUsd\" <= \"TotalUsd\""));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => new { x.SaleId, x.PostedAtUtc });
        b.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.TotalUsd).HasPrecision(18, 2);
        b.Property(x => x.AccountReductionUsd).HasPrecision(18, 2);
        b.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Refunds).WithOne().HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SaleReturnLineConfiguration : IEntityTypeConfiguration<SaleReturnLine>
{
    public void Configure(EntityTypeBuilder<SaleReturnLine> b)
    {
        b.ToTable("SaleReturnLines", t => t.HasCheckConstraint("CK_SaleReturnLines_Quantity",
            "\"Quantity\" > 0 AND \"RefundUsd\" >= 0 AND ((\"Condition\" = 'Quarantined' AND \"StockHoldMovementId\" IS NOT NULL AND \"IssueReason\" IS NOT NULL) OR (\"Condition\" = 'Sellable' AND \"StockHoldMovementId\" IS NULL))"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SaleReturnId, x.Position }).IsUnique();
        b.HasIndex(x => x.StockMovementId).IsUnique();
        b.HasIndex(x => x.StockHoldMovementId).IsUnique().HasFilter("\"StockHoldMovementId\" IS NOT NULL");
        b.HasIndex(x => x.SaleLineId);
        b.Property(x => x.RefundUsd).HasPrecision(18, 2);
        b.Property(x => x.Condition).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.IssueReason).HasMaxLength(500);
        b.HasOne<SaleLine>().WithMany().HasForeignKey(x => x.SaleLineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Part>().WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.StockMovementId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StockHoldMovement>().WithMany().HasForeignKey(x => x.StockHoldMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SaleRefundTenderConfiguration : IEntityTypeConfiguration<SaleRefundTender>
{
    public void Configure(EntityTypeBuilder<SaleRefundTender> b)
    {
        b.ToTable("SaleRefundTenders", t => t.HasCheckConstraint("CK_SaleRefundTenders_Amount",
            "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SaleReturnId, x.Position }).IsUnique();
        b.Property(x => x.AccountName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.NativeAmount).HasPrecision(20, 6);
        b.Property(x => x.ConfirmedUsdAmount).HasPrecision(18, 2);
        b.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
