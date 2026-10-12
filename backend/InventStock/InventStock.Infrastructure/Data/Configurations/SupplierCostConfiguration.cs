using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class SupplierCostObservationConfiguration : IEntityTypeConfiguration<SupplierCostObservation>
{
    public void Configure(EntityTypeBuilder<SupplierCostObservation> builder)
    {
        builder.ToTable("SupplierCostObservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitCost).HasPrecision(18, 6);
        builder.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.TaxTreatment).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SupplierCode).HasMaxLength(SupplierCostObservation.SupplierCodeMaxLength);
        builder.Property(x => x.Notes).HasMaxLength(SupplierCostObservation.NotesMaxLength);
        builder.HasIndex(x => new { x.PartId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.SupplierId, x.ObservedAtUtc });
        builder.HasIndex(x => x.PurchaseReceiptLineId).IsUnique().HasFilter("\"PurchaseReceiptLineId\" IS NOT NULL");
        builder.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}
