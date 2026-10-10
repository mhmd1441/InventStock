using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class ReferenceConfiguration : IEntityTypeConfiguration<CatalogueReference>
{
    public void Configure(EntityTypeBuilder<CatalogueReference> builder)
    {
        builder.ToTable("CatalogueReferences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Issuer).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IdentityKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SearchValue).HasMaxLength(64).IsRequired();
        builder.Property(x => x.MatchValue).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.Kind, x.Issuer, x.IdentityKey }).IsUnique();
        builder.HasIndex(x => x.MatchValue).HasDatabaseName("IX_CatalogueReferences_MatchValue_Exact");
        builder.HasIndex(x => x.MatchValue, "IX_CatalogueReferences_MatchValue_Trigram").HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.HasIndex(x => x.SearchValue).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

public sealed class SkuReferenceConfiguration : IEntityTypeConfiguration<SkuReference>
{
    public void Configure(EntityTypeBuilder<SkuReference> builder)
    {
        builder.ToTable("SkuReferences");
        builder.HasKey(x => new { x.PartId, x.ReferenceId });
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasOne(x => x.Part).WithMany(x => x.References).HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Reference).WithMany().HasForeignKey(x => x.ReferenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ReferenceId, x.PartId });
    }
}

public sealed class ReferenceLinkEventConfiguration : IEntityTypeConfiguration<ReferenceLinkEvent>
{
    public void Configure(EntityTypeBuilder<ReferenceLinkEvent> builder)
    {
        builder.ToTable("ReferenceLinkEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.PartId, x.ReferenceId, x.Revision }).IsUnique();
        builder.HasOne<SkuReference>().WithMany().HasForeignKey(x => new { x.PartId, x.ReferenceId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
