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
        builder.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.AmountChange).HasPrecision(20, 6);
        builder.HasIndex(x => new { x.CashAccountId, x.OccurredAtUtc });
        builder.HasIndex(x => x.OpeningBalanceId).IsUnique().HasFilter("\"OpeningBalanceId\" IS NOT NULL");
        builder.HasIndex(x => x.OutgoingPaymentId).IsUnique().HasFilter("\"OutgoingPaymentId\" IS NOT NULL");
        builder.HasIndex(x => x.OutgoingPaymentReversalId).IsUnique().HasFilter("\"OutgoingPaymentReversalId\" IS NOT NULL");
        builder.HasIndex(x => x.SaleTenderId).IsUnique().HasFilter("\"SaleTenderId\" IS NOT NULL");
        builder.HasIndex(x => x.SaleTenderAccountCorrectionId);
        builder.HasIndex(x => x.SaleTenderAccountCorrectionReversalId);
        builder.HasOne<SaleTender>().WithMany().HasForeignKey(x => x.SaleTenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SaleTenderAccountCorrection>().WithMany().HasForeignKey(x => x.SaleTenderAccountCorrectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SaleTenderAccountCorrectionReversal>().WithMany().HasForeignKey(x => x.SaleTenderAccountCorrectionReversalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CustomerPaymentTenderId).IsUnique().HasFilter("\"CustomerPaymentTenderId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL");
        builder.HasIndex(x => new { x.CustomerPaymentCorrectionId, x.CustomerPaymentTenderId }).IsUnique();
        builder.HasOne<CustomerPaymentTender>().WithMany().HasForeignKey(x => x.CustomerPaymentTenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerPaymentCorrection>().WithMany().HasForeignKey(x => x.CustomerPaymentCorrectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SaleRefundTenderId).IsUnique().HasFilter("\"SaleRefundTenderId\" IS NOT NULL");
        builder.HasOne<SaleRefundTender>().WithMany().HasForeignKey(x => x.SaleRefundTenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LegacySaleRefundTenderId).IsUnique().HasFilter("\"LegacySaleRefundTenderId\" IS NOT NULL");
        builder.HasOne<LegacySaleRefundTender>().WithMany().HasForeignKey(x => x.LegacySaleRefundTenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SupplierPaymentLineId).IsUnique().HasFilter("\"SupplierPaymentLineId\" IS NOT NULL");
        builder.HasOne<SupplierPaymentLine>().WithMany().HasForeignKey(x => x.SupplierPaymentLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SupplierPaymentReversalId);
        builder.HasOne<SupplierPaymentReversal>().WithMany().HasForeignKey(x => x.SupplierPaymentReversalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CashTransferId);
        builder.HasOne<CashTransfer>().WithMany().HasForeignKey(x => x.CashTransferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CashTransferReversalId);
        builder.HasOne<CashTransferReversal>().WithMany().HasForeignKey(x => x.CashTransferReversalId).OnDelete(DeleteBehavior.Restrict);
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

public sealed class CashReconciliationConfiguration : IEntityTypeConfiguration<CashReconciliation>
{
    public void Configure(EntityTypeBuilder<CashReconciliation> builder)
    {
        builder.ToTable("CashReconciliations", t =>
        {
            t.HasCheckConstraint("CK_CashReconciliations_Opening_Nonnegative", "\"OpeningAmount\" >= 0");
            t.HasCheckConstraint("CK_CashReconciliations_Counted_Nonnegative", "\"CountedAmount\" >= 0");
            t.HasCheckConstraint("CK_CashReconciliations_Reason_When_Different", "\"DifferenceAmount\" = 0 OR \"DifferenceReason\" IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BusinessDate).HasColumnType("date");
        builder.Property(x => x.OpeningAmount).HasPrecision(20, 6);
        builder.Property(x => x.OpeningSource).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExpectedClosingAmount).HasPrecision(20, 6);
        builder.Property(x => x.CountedAmount).HasPrecision(20, 6);
        builder.Property(x => x.DifferenceAmount).HasPrecision(20, 6);
        builder.Property(x => x.DifferenceReason).HasMaxLength(CashReconciliation.ReasonMaxLength);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(CashReconciliation.FingerprintMaxLength);
        // One original close per account/day, plus at most one final close which explicitly supersedes it.
        builder.HasIndex(x => new { x.CashAccountId, x.BusinessDate }).IsUnique()
            .HasFilter("\"SupersedesCashReconciliationId\" IS NULL");
        builder.HasIndex(x => x.SupersedesCashReconciliationId).IsUnique()
            .HasFilter("\"SupersedesCashReconciliationId\" IS NOT NULL");
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => new { x.CashAccountId, x.ClosedAtUtc });
        builder.HasOne(x => x.CashAccount).WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashReconciliation>().WithMany().HasForeignKey(x => x.SupersedesCashReconciliationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashReconciliationCorrectionConfiguration : IEntityTypeConfiguration<CashReconciliationCorrection>
{
    public void Configure(EntityTypeBuilder<CashReconciliationCorrection> builder)
    {
        builder.ToTable("CashReconciliationCorrections", t =>
        {
            t.HasCheckConstraint("CK_CashReconciliationCorrections_Counted_Nonnegative", "\"CountedAmount\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExpectedClosingAmount).HasPrecision(20, 6);
        builder.Property(x => x.CountedAmount).HasPrecision(20, 6);
        builder.Property(x => x.DifferenceAmount).HasPrecision(20, 6);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(CashReconciliationCorrection.ReasonMaxLength);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(CashReconciliationCorrection.FingerprintMaxLength);
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => new { x.CashReconciliationId, x.CorrectedAtUtc });
        builder.HasOne(x => x.CashReconciliation).WithMany().HasForeignKey(x => x.CashReconciliationId).OnDelete(DeleteBehavior.Restrict);
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

public sealed class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        builder.ToTable("SupplierPayments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PaidOn).HasColumnType("date");
        builder.Property(x => x.Note).HasMaxLength(SupplierPayment.TextMaxLength);
        builder.Property(x => x.ReceiptReference).HasMaxLength(SupplierPayment.ReferenceMaxLength);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(SupplierPayment.FingerprintMaxLength);
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => new { x.SupplierId, x.PaidOn });
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CashAccount).WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupplierPaymentLineConfiguration : IEntityTypeConfiguration<SupplierPaymentLine>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentLine> builder)
    {
        builder.ToTable("SupplierPaymentLines", t =>
        {
            t.HasCheckConstraint("CK_SupplierPaymentLines_OriginalAmount_Positive", "\"OriginalAmount\" > 0");
            t.HasCheckConstraint("CK_SupplierPaymentLines_UsdValue_Positive", "\"AgreedUsdValue\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.OriginalAmount).HasPrecision(20, 6);
        builder.Property(x => x.AgreedUsdValue).HasPrecision(20, 6);
        builder.HasIndex(x => x.CashMovementId).IsUnique();
        builder.HasOne(x => x.SupplierPayment).WithMany(x => x.Lines).HasForeignKey(x => x.SupplierPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupplierPaymentReversalConfiguration : IEntityTypeConfiguration<SupplierPaymentReversal>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentReversal> builder)
    {
        builder.ToTable("SupplierPaymentReversals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(SupplierPaymentReversal.ReasonMaxLength);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(SupplierPaymentReversal.FingerprintMaxLength);
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.SupplierPaymentId).IsUnique();
        builder.HasOne(x => x.SupplierPayment).WithMany().HasForeignKey(x => x.SupplierPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashTransferConfiguration : IEntityTypeConfiguration<CashTransfer>
{
    public void Configure(EntityTypeBuilder<CashTransfer> builder)
    {
        builder.ToTable("CashTransfers", t =>
        {
            t.HasCheckConstraint("CK_CashTransfers_Source_Positive", "\"SourceAmount\" > 0");
            t.HasCheckConstraint("CK_CashTransfers_Destination_Positive", "\"DestinationAmount\" > 0");
            t.HasCheckConstraint("CK_CashTransfers_Rate_Positive", "\"AgreedRate\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransferredOn).HasColumnType("date");
        builder.Property(x => x.SourceAmount).HasPrecision(20, 6);
        builder.Property(x => x.DestinationAmount).HasPrecision(20, 6);
        builder.Property(x => x.AgreedRate).HasPrecision(20, 10);
        builder.Property(x => x.FeeAmount).HasPrecision(20, 6);
        builder.Property(x => x.SourceCurrency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.DestinationCurrency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.FeeCurrency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.Note).HasMaxLength(CashTransfer.NoteMaxLength);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(CashTransfer.FingerprintMaxLength);
        builder.HasIndex(x => x.OperationId).IsUnique();
        // The day/source index is useful for reconciliation, but does not cover a
        // source-only foreign-key lookup. Keep both indexes deliberately.
        builder.HasIndex(x => x.SourceCashAccountId);
        builder.HasIndex(x => new { x.TransferredOn, x.SourceCashAccountId });
        builder.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.SourceCashAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.DestinationCashAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.FeeCashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashTransferReversalConfiguration : IEntityTypeConfiguration<CashTransferReversal>
{
    public void Configure(EntityTypeBuilder<CashTransferReversal> builder)
    {
        builder.ToTable("CashTransferReversals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(CashTransferReversal.ReasonMaxLength);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(CashTransferReversal.FingerprintMaxLength);
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.CashTransferId).IsUnique();
        builder.HasOne<CashTransfer>().WithMany().HasForeignKey(x => x.CashTransferId).OnDelete(DeleteBehavior.Restrict);
    }
}
