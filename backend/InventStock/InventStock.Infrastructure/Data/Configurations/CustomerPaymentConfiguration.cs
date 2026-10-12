using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class CustomerProfileEventConfiguration : IEntityTypeConfiguration<CustomerProfileEvent>
{
    public void Configure(EntityTypeBuilder<CustomerProfileEvent> b)
    {
        b.ToTable("CustomerProfileEvents");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => new { x.CustomerId, x.Revision }).IsUnique();
        b.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.DistinguishingLabel).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(500);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CustomerPaymentConfiguration : IEntityTypeConfiguration<CustomerPayment>
{
    public void Configure(EntityTypeBuilder<CustomerPayment> b)
    {
        b.ToTable("CustomerPayments", t => t.HasCheckConstraint("CK_CustomerPayments_Balance",
            "\"UsdCredit\" > 0 AND \"ExcessCreditUsd\" >= 0 AND \"ExcessCreditUsd\" <= \"UsdCredit\" AND \"BalanceAfterUsd\" = \"BalanceBeforeUsd\" - \"UsdCredit\""));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => new { x.CustomerId, x.PostedAtUtc });
        b.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        b.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.UsdCredit).HasPrecision(18, 2);
        b.Property(x => x.ExcessCreditUsd).HasPrecision(18, 2);
        b.Property(x => x.BalanceBeforeUsd).HasPrecision(18, 2);
        b.Property(x => x.BalanceAfterUsd).HasPrecision(18, 2);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Tenders).WithOne().HasForeignKey(x => x.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CustomerPaymentTenderConfiguration : IEntityTypeConfiguration<CustomerPaymentTender>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentTender> b)
    {
        b.ToTable("CustomerPaymentTenders", t => t.HasCheckConstraint("CK_CustomerPaymentTenders_Amounts",
            "\"NativeAmount\" > 0 AND \"ConfirmedUsdAmount\" > 0 AND (\"Currency\" <> 'USD' OR \"NativeAmount\" = \"ConfirmedUsdAmount\")"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CustomerPaymentId, x.Position }).IsUnique();
        b.Property(x => x.AccountName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.NativeAmount).HasPrecision(20, 6);
        b.Property(x => x.ConfirmedUsdAmount).HasPrecision(18, 2);
        b.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CustomerPaymentCorrectionConfiguration : IEntityTypeConfiguration<CustomerPaymentCorrection>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentCorrection> b)
    {
        b.ToTable("CustomerPaymentCorrections", t => t.HasCheckConstraint("CK_CustomerPaymentCorrections_Positive", "\"RestoredDebtUsd\" > 0"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => x.OriginalPaymentId).IsUnique();
        b.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.RestoredDebtUsd).HasPrecision(18, 2);
        b.Property(x => x.BalanceAfterUsd).HasPrecision(18, 2);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CustomerPayment>().WithMany().HasForeignKey(x => x.OriginalPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
