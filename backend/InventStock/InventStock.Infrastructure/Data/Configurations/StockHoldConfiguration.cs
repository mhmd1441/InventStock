using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class StockHoldMovementConfiguration : IEntityTypeConfiguration<StockHoldMovement>
{
    public void Configure(EntityTypeBuilder<StockHoldMovement> builder)
    {
        builder.ToTable("StockHoldMovements", t => t.HasCheckConstraint("CK_StockHoldMovements_Quantity_NotZero", "\"QuantityChange\" <> 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(StockHoldMovement.ReasonMaxLength).IsRequired();
        builder.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => new { x.PartId, x.OccurredAtUtc });
        builder.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
    }
}
