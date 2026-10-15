using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class LegacySaleReturnConfiguration : IEntityTypeConfiguration<LegacySaleReturn>
{
    public void Configure(EntityTypeBuilder<LegacySaleReturn> b)
    {
        b.ToTable("LegacySaleReturns", t => t.HasCheckConstraint("CK_LegacySaleReturns_Settlement",
            "\"TotalUsd\" > 0 AND \"AccountReductionUsd\" >= 0 AND \"AccountReductionUsd\" <= \"TotalUsd\""));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => x.PostedAtUtc);
        b.HasIndex(x => new { x.CustomerId, x.PostedAtUtc });
        b.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.CustomerName).HasMaxLength(200);
        b.Property(x => x.OriginalSaleDate).HasColumnType("date");
        b.Property(x => x.OriginalReference).HasMaxLength(200);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.TotalUsd).HasPrecision(18, 2);
        b.Property(x => x.AccountReductionUsd).HasPrecision(18, 2);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.LegacySaleReturnId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Refunds).WithOne().HasForeignKey(x => x.LegacySaleReturnId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LegacySaleReturnLineConfiguration : IEntityTypeConfiguration<LegacySaleReturnLine>
{
    public void Configure(EntityTypeBuilder<LegacySaleReturnLine> b)
    {
        b.ToTable("LegacySaleReturnLines", t => t.HasCheckConstraint("CK_LegacySaleReturnLines_Quantity",
            "\"Quantity\" > 0 AND \"RefundUsd\" > 0 AND ((\"Condition\" = 'Quarantined' AND \"StockHoldMovementId\" IS NOT NULL AND \"IssueReason\" IS NOT NULL) OR (\"Condition\" = 'Sellable' AND \"StockHoldMovementId\" IS NULL))"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.LegacySaleReturnId, x.Position }).IsUnique();
        b.HasIndex(x => x.StockMovementId).IsUnique();
        b.HasIndex(x => x.StockHoldMovementId).IsUnique().HasFilter("\"StockHoldMovementId\" IS NOT NULL");
        b.HasIndex(x => new { x.PartId, x.CreatedAtUtc });
        b.Property(x => x.PartNumber).HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Brand).HasMaxLength(100).IsRequired();
        b.Property(x => x.Variant).HasMaxLength(200).IsRequired();
        b.Property(x => x.StockUnit).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RefundUsd).HasPrecision(18, 2);
        b.Property(x => x.Condition).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.IssueReason).HasMaxLength(500);
        b.HasOne<Part>().WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.StockMovementId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StockHoldMovement>().WithMany().HasForeignKey(x => x.StockHoldMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LegacySaleRefundTenderConfiguration : IEntityTypeConfiguration<LegacySaleRefundTender>
{
    public void Configure(EntityTypeBuilder<LegacySaleRefundTender> b)
    {
        b.ToTable("LegacySaleRefundTenders", t => t.HasCheckConstraint("CK_LegacySaleRefundTenders_Amount",
            "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.LegacySaleReturnId, x.Position }).IsUnique();
        b.Property(x => x.AccountName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.NativeAmount).HasPrecision(20, 6);
        b.Property(x => x.ConfirmedUsdAmount).HasPrecision(18, 2);
        b.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
