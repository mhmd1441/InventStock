using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class PaperRecoveryEntryConfiguration : IEntityTypeConfiguration<PaperRecoveryEntry>
{
    public void Configure(EntityTypeBuilder<PaperRecoveryEntry> b)
    {
        b.ToTable("PaperRecoveryEntries", t => t.HasCheckConstraint("CK_PaperRecoveryEntries_OneTarget",
            "(\"SaleId\" IS NOT NULL AND \"CustomerPaymentId\" IS NULL) OR (\"SaleId\" IS NULL AND \"CustomerPaymentId\" IS NOT NULL)"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.PaperReference).IsUnique();
        b.HasIndex(x => x.SaleId).IsUnique().HasFilter("\"SaleId\" IS NOT NULL");
        b.HasIndex(x => x.CustomerPaymentId).IsUnique().HasFilter("\"CustomerPaymentId\" IS NOT NULL");
        b.Property(x => x.PaperReference).HasMaxLength(PaperRecoveryEntry.PaperReferenceMaxLength).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(PaperRecoveryEntry.ReasonMaxLength).IsRequired();
        b.Property(x => x.OriginalSellerNote).HasMaxLength(PaperRecoveryEntry.OriginalSellerNoteMaxLength);
        b.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CustomerPayment>().WithMany().HasForeignKey(x => x.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
