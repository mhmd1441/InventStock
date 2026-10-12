using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("PurchaseReceipts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PayloadFingerprint).IsRequired().HasMaxLength(PurchaseReceipt.PayloadFingerprintMaxLength);
        builder.Property(x => x.SupplierReference).HasMaxLength(PurchaseReceipt.ReferenceMaxLength);
        builder.Property(x => x.Notes).HasMaxLength(PurchaseReceipt.NotesMaxLength);
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => new { x.SupplierId, x.ReceivedAtUtc });
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.PurchaseReceipt).HasForeignKey(x => x.PurchaseReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PurchaseReceiptLineConfiguration : IEntityTypeConfiguration<PurchaseReceiptLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.ToTable("PurchaseReceiptLines", t =>
        {
            t.HasCheckConstraint("CK_PurchaseReceiptLines_PackageCount_Positive", "\"PackageCount\" > 0");
            t.HasCheckConstraint("CK_PurchaseReceiptLines_BaseUnitsPerPackage_Positive", "\"BaseUnitsPerPackage\" > 0");
            t.HasCheckConstraint("CK_PurchaseReceiptLines_BaseQuantity_Positive", "\"BaseQuantity\" > 0");
            t.HasCheckConstraint("CK_PurchaseReceiptLines_CostCurrency_Paired",
                "(\"CostPerPackage\" IS NULL AND \"Currency\" IS NULL) OR (\"CostPerPackage\" IS NOT NULL AND \"Currency\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CostPerPackage).HasPrecision(18, 6);
        builder.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.TaxTreatment).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SupplierCode).HasMaxLength(PurchaseReceiptLine.SupplierCodeMaxLength);
        builder.Property(x => x.Notes).HasMaxLength(PurchaseReceiptLine.NotesMaxLength);
        builder.HasIndex(x => x.PartId);
        builder.HasIndex(x => x.StockMovementId).IsUnique();
        builder.HasIndex(x => x.SupplierCostObservationId).IsUnique().HasFilter("\"SupplierCostObservationId\" IS NOT NULL");
        builder.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
    }
}
