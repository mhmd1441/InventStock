using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class PricingPolicyConfiguration : IEntityTypeConfiguration<PricingPolicy>
{
    public void Configure(EntityTypeBuilder<PricingPolicy> builder)
    {
        builder.ToTable("PricingPolicies", t => t.HasCheckConstraint("CK_PricingPolicies_Singleton", "\"Id\" = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MarkupPercent).HasPrecision(8, 3);
        builder.Property(x => x.TvaPercent).HasPrecision(8, 3);
        builder.Property(x => x.EurUsdRate).HasPrecision(18, 6);
        builder.Property(x => x.LbpPerUsdRate).HasPrecision(18, 3);
        builder.HasData(new { Id = 1, MarkupPercent = 15m, TvaPercent = 11m, Revision = 0 });
    }
}
