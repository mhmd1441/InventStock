using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class SupplierImportBatchConfiguration : IEntityTypeConfiguration<SupplierImportBatch>
{
    public void Configure(EntityTypeBuilder<SupplierImportBatch> builder)
    {
        builder.ToTable("SupplierImportBatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceFileName).HasMaxLength(SupplierImportBatch.FileNameMaxLength).IsRequired();
        builder.Property(x => x.SourceSha256).HasMaxLength(SupplierImportBatch.HashMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.SupplierId, x.SourceSha256 }).IsUnique();
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Rows).WithOne(x => x.SupplierImportBatch).HasForeignKey(x => x.SupplierImportBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Rows).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SupplierImportRowConfiguration : IEntityTypeConfiguration<SupplierImportRow>
{
    public void Configure(EntityTypeBuilder<SupplierImportRow> builder)
    {
        builder.ToTable("SupplierImportRows", t => t.HasCheckConstraint("CK_SupplierImportRows_SourceRow_Positive", "\"SourceRowNumber\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PartNumber).HasMaxLength(Part.PartNumberMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Part.NameMaxLength).IsRequired();
        builder.Property(x => x.Brand).HasMaxLength(SupplierImportRow.BrandMaxLength).IsRequired();
        builder.Property(x => x.Variant).HasMaxLength(SupplierImportRow.VariantMaxLength).IsRequired();
        builder.Property(x => x.SupplierCode).HasMaxLength(SupplierImportRow.SupplierCodeMaxLength);
        builder.Property(x => x.CatalogueCost).HasPrecision(18, 6);
        builder.Property(x => x.Currency).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.TaxTreatment).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.StockUnit).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.SupplierImportBatchId, x.SourceRowNumber }).IsUnique();
        builder.HasOne<Part>().WithMany().HasForeignKey(x => x.TargetPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductBrand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
    }
}
