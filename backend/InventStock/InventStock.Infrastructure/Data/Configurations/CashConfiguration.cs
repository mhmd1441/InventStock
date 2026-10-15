using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class CashAccountConfiguration : IEntityTypeConfiguration<CashAccount>
{
    public void Configure(EntityTypeBuilder<CashAccount> builder)
    {
        builder.ToTable("CashAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(CashAccount.CodeMaxLength);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(CashAccount.NameMaxLength);
        builder.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("ExpenseCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(ExpenseCategory.NameMaxLength);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(ExpenseCategory.NameMaxLength);
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}

public sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("CashMovements", t => t.HasCheckConstraint("CK_CashMovements_Amount_NotZero_ExceptOpening", "\"AmountChange\" <> 0 OR \"MovementType\" = 'OpeningBalance'"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.AmountChange).HasPrecision(20, 6);
        builder.HasIndex(x => new { x.CashAccountId, x.OccurredAtUtc });
        builder.HasIndex(x => x.OpeningBalanceId).IsUnique().HasFilter("\"OpeningBalanceId\" IS NOT NULL");
        builder.HasIndex(x => x.OutgoingPaymentId).IsUnique().HasFilter("\"OutgoingPaymentId\" IS NOT NULL");
        builder.HasIndex(x => x.OutgoingPaymentReversalId).IsUnique().HasFilter("\"OutgoingPaymentReversalId\" IS NOT NULL");
        builder.HasIndex(x => x.SaleTenderId).IsUnique().HasFilter("\"SaleTenderId\" IS NOT NULL");
        builder.HasOne<SaleTender>().WithMany().HasForeignKey(x => x.SaleTenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CustomerPaymentTenderId).IsUnique().HasFilter("\"CustomerPaymentTenderId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL");
        builder.HasIndex(x => new { x.CustomerPaymentCorrectionId, x.CustomerPaymentTenderId }).IsUnique();
        builder.HasOne<CustomerPaymentTender>().WithMany().HasForeignKey(x => x.CustomerPaymentTenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerPaymentCorrection>().WithMany().HasForeignKey(x => x.CustomerPaymentCorrectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SaleRefundTenderId).IsUnique().HasFilter("\"SaleRefundTenderId\" IS NOT NULL");
        builder.HasOne<SaleRefundTender>().WithMany().HasForeignKey(x => x.SaleRefundTenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LegacySaleRefundTenderId).IsUnique().HasFilter("\"LegacySaleRefundTenderId\" IS NOT NULL");
        builder.HasOne<LegacySaleRefundTender>().WithMany().HasForeignKey(x => x.LegacySaleRefundTenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CashAccount).WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashOpeningBalanceConfiguration : IEntityTypeConfiguration<CashOpeningBalance>
{
    public void Configure(EntityTypeBuilder<CashOpeningBalance> builder)
    {
        builder.ToTable("CashOpeningBalances", t => t.HasCheckConstraint("CK_CashOpeningBalances_Amount_Nonnegative", "\"CountedAmount\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CountedAmount).HasPrecision(20, 6);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(CashOpeningBalance.FingerprintMaxLength);
        builder.HasIndex(x => x.CashAccountId).IsUnique();
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.CashMovementId).IsUnique();
        builder.HasOne(x => x.CashAccount).WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OutgoingPaymentConfiguration : IEntityTypeConfiguration<OutgoingPayment>
{
    public void Configure(EntityTypeBuilder<OutgoingPayment> builder)
    {
        builder.ToTable("OutgoingPayments", t =>
        {
            t.HasCheckConstraint("CK_OutgoingPayments_Amount_Positive", "\"OriginalAmount\" > 0");
            t.HasCheckConstraint("CK_OutgoingPayments_UsdValue_Positive", "\"AgreedUsdValue\" > 0");
            t.HasCheckConstraint("CK_OutgoingPayments_FundingAccount", "(\"FundingSource\" = 'ShopAccount' AND \"CashAccountId\" IS NOT NULL) OR (\"FundingSource\" = 'PersonalPocket' AND \"CashAccountId\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FundingSource).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.OriginalAmount).HasPrecision(20, 6);
        builder.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.AgreedUsdValue).HasPrecision(20, 6);
        builder.Property(x => x.Payee).HasMaxLength(OutgoingPayment.TextMaxLength);
        builder.Property(x => x.Description).HasMaxLength(OutgoingPayment.TextMaxLength);
        builder.Property(x => x.ForPeriod).HasMaxLength(OutgoingPayment.TextMaxLength);
        builder.Property(x => x.ReceiptReference).HasMaxLength(OutgoingPayment.ReceiptReferenceMaxLength);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(OutgoingPayment.FingerprintMaxLength);
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.Property(x => x.PaidOn).HasColumnType("date");
        builder.HasIndex(x => new { x.PaidOn, x.ExpenseCategoryId });
        builder.HasIndex(x => x.CashMovementId).IsUnique().HasFilter("\"CashMovementId\" IS NOT NULL");
        builder.HasOne(x => x.ExpenseCategory).WithMany().HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CashAccount).WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OutgoingPaymentReversalConfiguration : IEntityTypeConfiguration<OutgoingPaymentReversal>
{
    public void Configure(EntityTypeBuilder<OutgoingPaymentReversal> builder)
    {
        builder.ToTable("OutgoingPaymentReversals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(OutgoingPaymentReversal.ReasonMaxLength);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(OutgoingPaymentReversal.FingerprintMaxLength);
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.OutgoingPaymentId).IsUnique();
        builder.HasIndex(x => x.CashMovementId).IsUnique().HasFilter("\"CashMovementId\" IS NOT NULL");
        builder.HasOne(x => x.OutgoingPayment).WithMany().HasForeignKey(x => x.OutgoingPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
