using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("Sales", t =>
        {
            t.HasCheckConstraint("CK_Sales_Settlement", "\"TotalUsd\" > 0 AND \"PaymentsUsd\" >= \"ChangeUsd\" AND \"ChangeUsd\" >= 0 AND \"ExchangeCreditUsd\" >= 0 AND \"CustomerCreditUsedUsd\" >= 0 AND \"UnpaidDebtUsd\" >= 0 AND \"TotalUsd\" = \"PaymentsUsd\" - \"ChangeUsd\" + \"ExchangeCreditUsd\" + \"CustomerCreditUsedUsd\" + \"UnpaidDebtUsd\"");
            t.HasCheckConstraint("CK_Sales_Customer", "\"CustomerId\" IS NOT NULL OR (\"CustomerCreditUsedUsd\" = 0 AND \"UnpaidDebtUsd\" = 0)");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => x.ReceiptNumber).IsUnique();
        b.HasIndex(x => x.PostedAtUtc);
        b.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.CustomerName).HasMaxLength(200);
        b.Property(x => x.TotalUsd).HasPrecision(18, 2);
        b.Property(x => x.PaymentsUsd).HasPrecision(18, 2);
        b.Property(x => x.ChangeUsd).HasPrecision(18, 2);
        b.Property(x => x.ExchangeCreditUsd).HasPrecision(18, 2);
        b.Property(x => x.CustomerCreditUsedUsd).HasPrecision(18, 2);
        b.Property(x => x.UnpaidDebtUsd).HasPrecision(18, 2);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Tenders).WithOne().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        b.ToTable("SaleLines", t =>
        {
            t.HasCheckConstraint("CK_SaleLines_Amounts", "\"Quantity\" > 0 AND \"UnitSellingPriceUsd\" > 0 AND \"LineTotalUsd\" >= 0");
            t.HasCheckConstraint("CK_SaleLines_PriceOverride", "(\"SuggestedUnitPriceUsd\" IS NULL AND \"PriceOverridden\" = FALSE) OR (\"SuggestedUnitPriceUsd\" > 0 AND \"PriceOverridden\" = (\"UnitSellingPriceUsd\" <> \"SuggestedUnitPriceUsd\"))");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SaleId, x.Position }).IsUnique();
        b.HasIndex(x => x.StockMovementId).IsUnique();
        b.HasIndex(x => x.PartId, "SaleLinePartLookup").HasDatabaseName("IX_SaleLines_PartId_All");
        b.HasIndex(x => x.PartId, "SaleLineShortageLookup").HasDatabaseName("IX_SaleLines_PartId")
            .HasFilter("\"StockShortage\" = TRUE");
        b.Property(x => x.PartNumber).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Brand).HasMaxLength(80).IsRequired();
        b.Property(x => x.Variant).HasMaxLength(120).IsRequired();
        b.Property(x => x.StockUnit).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.UnitSellingPriceUsd).HasPrecision(18, 6);
        b.Property(x => x.SuggestedUnitPriceUsd).HasPrecision(18, 6);
        b.Property(x => x.LineTotalUsd).HasPrecision(18, 2);
        b.Property(x => x.EstimatedBaseUnitCost).HasPrecision(20, 6);
        b.Property(x => x.CostCurrency).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.CostTaxTreatment).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.StockExceptionReason).HasMaxLength(500);
        b.HasOne<Part>().WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.StockMovementId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SupplierCostObservation>().WithMany().HasForeignKey(x => x.CostObservationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SaleTenderConfiguration : IEntityTypeConfiguration<SaleTender>
{
    public void Configure(EntityTypeBuilder<SaleTender> b)
    {
        b.ToTable("SaleTenders", t => t.HasCheckConstraint("CK_SaleTenders_Amounts", "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SaleId, x.Position }).IsUnique();
        b.Property(x => x.AccountName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.NativeAmount).HasPrecision(20, 6);
        b.Property(x => x.ConfirmedUsdAmount).HasPrecision(18, 2);
        b.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SaleTenderAccountCorrectionConfiguration : IEntityTypeConfiguration<SaleTenderAccountCorrection>
{
    public void Configure(EntityTypeBuilder<SaleTenderAccountCorrection> b)
    {
        b.ToTable("SaleTenderAccountCorrections", t =>
        {
            t.HasCheckConstraint("CK_SaleTenderAccountCorrections_Amounts", "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")");
            t.HasCheckConstraint("CK_SaleTenderAccountCorrections_Accounts", "\"OriginalCashAccountId\" <> \"CorrectCashAccountId\"");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OriginalSaleTenderId).IsUnique();
        b.HasIndex(x => x.OperationId).IsUnique();
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.NativeAmount).HasPrecision(20, 6);
        b.Property(x => x.ConfirmedUsdAmount).HasPrecision(18, 2);
        b.Property(x => x.Reason).HasMaxLength(SaleTenderAccountCorrection.ReasonMaxLength).IsRequired();
        b.Property(x => x.PayloadFingerprint).HasMaxLength(SaleTenderAccountCorrection.FingerprintMaxLength).IsRequired();
        b.HasOne<SaleTender>().WithMany().HasForeignKey(x => x.OriginalSaleTenderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.OriginalCashAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.CorrectCashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SaleTenderAccountCorrectionReversalConfiguration : IEntityTypeConfiguration<SaleTenderAccountCorrectionReversal>
{
    public void Configure(EntityTypeBuilder<SaleTenderAccountCorrectionReversal> b)
    {
        b.ToTable("SaleTenderAccountCorrectionReversals");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.SaleTenderAccountCorrectionId).IsUnique();
        b.HasIndex(x => x.OperationId).IsUnique();
        b.Property(x => x.Reason).HasMaxLength(SaleTenderAccountCorrectionReversal.ReasonMaxLength).IsRequired();
        b.Property(x => x.PayloadFingerprint).HasMaxLength(SaleTenderAccountCorrection.FingerprintMaxLength).IsRequired();
        b.HasOne<SaleTenderAccountCorrection>().WithMany().HasForeignKey(x => x.SaleTenderAccountCorrectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.DistinguishingLabel).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => x.Name); // Names are deliberately not unique identifiers.
    }
}

public sealed class CustomerAccountEntryConfiguration : IEntityTypeConfiguration<CustomerAccountEntry>
{
    public void Configure(EntityTypeBuilder<CustomerAccountEntry> b)
    {
        b.ToTable("CustomerAccountEntries", t => t.HasCheckConstraint("CK_CustomerEntries_Kind",
            "(\"Kind\" = 'Opening' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL) OR (\"Kind\" = 'OpeningCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"OriginalOpeningEntryId\" IS NOT NULL AND \"CorrectedOpeningBalanceUsd\" IS NOT NULL AND \"BalanceAfterUsd\" IS NOT NULL AND \"AmountUsd\" <> 0) OR (\"Kind\" = 'SaleCharge' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" > 0) OR (\"Kind\" = 'SalePayment' AND \"SaleId\" IS NOT NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'ReturnReduction' AND \"SaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'LegacyReturnReduction' AND \"LegacySaleReturnId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'ExchangeReturnValue' AND \"SaleExchangeId\" IS NOT NULL AND \"SaleId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'AccountPayment' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NULL AND \"AmountUsd\" < 0) OR (\"Kind\" = 'PaymentCorrection' AND \"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL AND \"CustomerPaymentCorrectionId\" IS NOT NULL AND \"AmountUsd\" > 0)"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(30).IsRequired();
        b.Property(x => x.AmountUsd).HasPrecision(18, 2);
        b.Property(x => x.CorrectedOpeningBalanceUsd).HasPrecision(18, 2);
        b.Property(x => x.BalanceAfterUsd).HasPrecision(18, 2);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.PayloadFingerprint).HasMaxLength(64);
        b.HasIndex(x => x.CustomerId).IsUnique().HasFilter("\"Kind\" = 'Opening'");
        b.HasIndex(x => new { x.CustomerPaymentId, x.Kind }).IsUnique();
        b.HasIndex(x => x.CustomerPaymentCorrectionId).IsUnique();
        b.HasIndex(x => x.SaleReturnId).IsUnique().HasFilter("\"SaleReturnId\" IS NOT NULL");
        b.HasIndex(x => x.LegacySaleReturnId).IsUnique().HasFilter("\"LegacySaleReturnId\" IS NOT NULL");
        b.HasIndex(x => x.SaleExchangeId).IsUnique().HasFilter("\"SaleExchangeId\" IS NOT NULL");
        b.HasIndex(x => x.OriginalOpeningEntryId);
        b.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.OperationId, x.Kind }).IsUnique();
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CustomerPayment>().WithMany().HasForeignKey(x => x.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CustomerPaymentCorrection>().WithMany().HasForeignKey(x => x.CustomerPaymentCorrectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SaleReturn>().WithMany().HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LegacySaleReturn>().WithMany().HasForeignKey(x => x.LegacySaleReturnId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SaleExchange>().WithMany().HasForeignKey(x => x.SaleExchangeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CustomerAccountEntry>().WithMany().HasForeignKey(x => x.OriginalOpeningEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
