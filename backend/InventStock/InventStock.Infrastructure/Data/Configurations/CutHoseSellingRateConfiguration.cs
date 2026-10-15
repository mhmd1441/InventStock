using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class CutHoseSellingRateConfiguration : IEntityTypeConfiguration<CutHoseSellingRate>
{
    public void Configure(EntityTypeBuilder<CutHoseSellingRate> builder)
    {
        builder.ToTable("CutHoseSellingRates", t => t.HasCheckConstraint("CK_CutHoseSellingRates_Rate",
            "\"Revision\" > 0 AND (\"UsdPerMeter\" IS NULL OR \"UsdPerMeter\" > 0)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UsdPerMeter).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.PartId, x.Revision }).IsUnique();
        builder.HasOne<Part>().WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
    }
}
