using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class SaleExchangeConfiguration : IEntityTypeConfiguration<SaleExchange>
{
    public void Configure(EntityTypeBuilder<SaleExchange> b)
    {
        b.ToTable("SaleExchanges", t =>
        {
            t.HasCheckConstraint("CK_SaleExchanges_ReturnSettlement",
                "\"ReturnTotalUsd\" > 0 AND \"ReturnAppliedUsd\" >= 0 AND \"RefundUsd\" >= 0 AND \"AccountReductionUsd\" >= 0 AND \"ReturnTotalUsd\" = \"ReturnAppliedUsd\" + \"RefundUsd\" + \"AccountReductionUsd\"");
            t.HasCheckConstraint("CK_SaleExchanges_Replacement", "\"ReplacementTotalUsd\" > 0");
            t.HasCheckConstraint("CK_SaleExchanges_Customer",
                "\"CustomerId\" IS NOT NULL OR \"AccountReductionUsd\" = 0");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => x.SaleReturnId).IsUnique();
        b.HasIndex(x => x.ReplacementSaleId).IsUnique();
        b.HasIndex(x => new { x.OriginalSaleId, x.PostedAtUtc });
        b.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReturnTotalUsd).HasPrecision(18, 2);
        b.Property(x => x.ReturnAppliedUsd).HasPrecision(18, 2);
        b.Property(x => x.RefundUsd).HasPrecision(18, 2);
        b.Property(x => x.AccountReductionUsd).HasPrecision(18, 2);
        b.Property(x => x.ReplacementTotalUsd).HasPrecision(18, 2);
        b.HasOne<Sale>().WithMany().HasForeignKey(x => x.OriginalSaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SaleReturn).WithMany().HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReplacementSale).WithMany().HasForeignKey(x => x.ReplacementSaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
