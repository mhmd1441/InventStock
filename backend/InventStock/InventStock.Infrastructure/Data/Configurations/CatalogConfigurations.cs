using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(Category.NameMaxLength);

        builder.Property(x => x.Description)
            .HasMaxLength(Category.DescriptionMaxLength);

        builder.HasIndex(x => x.Name).IsUnique();

        builder.Metadata
            .FindNavigation(nameof(Category.Parts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(Supplier.NameMaxLength);
        builder.Property(x => x.ContactName).HasMaxLength(Supplier.ContactNameMaxLength);
        builder.Property(x => x.Email).HasMaxLength(Supplier.EmailMaxLength);
        builder.Property(x => x.Phone).HasMaxLength(Supplier.PhoneMaxLength);
        builder.Property(x => x.Address).HasMaxLength(Supplier.AddressMaxLength);
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();

        builder.Metadata
            .FindNavigation(nameof(Supplier.Parts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> builder)
    {
        builder.ToTable("Parts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartNumber)
            .IsRequired()
            .HasMaxLength(Part.PartNumberMaxLength);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(Part.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(Part.DescriptionMaxLength);
        builder.Property(x => x.Barcode).HasMaxLength(Part.BarcodeMaxLength);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LowStockThreshold).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => x.PartNumber).IsUnique();
        builder.HasIndex(x => x.Barcode).IsUnique().HasFilter(null);
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.SupplierId);

        builder.HasOne(x => x.Category)
            .WithMany(c => c.Parts)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Supplier)
            .WithMany(s => s.Parts)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata
            .FindNavigation(nameof(Part.Movements))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
