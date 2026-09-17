using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", t =>
            t.HasCheckConstraint("CK_StockMovements_QuantityChange_NotZero", "\"QuantityChange\" <> 0"));

        builder.HasKey(x => x.Id);

        // Stored as text so the audit trail stays readable when queried directly in the database.
        builder.Property(x => x.MovementType)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.QuantityChange).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(StockMovement.ReasonMaxLength);
        builder.Property(x => x.Reference).HasMaxLength(StockMovement.ReferenceMaxLength);
        builder.Property(x => x.PerformedByUserId).IsRequired();
        builder.Property(x => x.OccurredAtUtc).IsRequired();

        builder.HasIndex(x => new { x.PartId, x.OccurredAtUtc });
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.PerformedByUserId);

        builder.HasOne(x => x.Part)
            .WithMany(p => p.Movements)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
