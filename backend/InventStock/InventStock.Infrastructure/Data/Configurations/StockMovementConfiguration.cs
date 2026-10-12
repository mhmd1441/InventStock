using InventStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventStock.Infrastructure.Data.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", t =>
            t.HasCheckConstraint("CK_StockMovements_QuantityChange_NotZero",
                "\"QuantityChange\" <> 0 OR \"MovementType\" = 'PhysicalCount'"));

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
        builder.Property(x => x.PayloadFingerprint).HasMaxLength(StockMovement.PayloadFingerprintMaxLength);
        builder.Property(x => x.CountedQuantity);
        builder.Property(x => x.BookQuantityBeforeCount);
        builder.Property(x => x.PurchaseReceiptLineId);

        builder.HasIndex(x => new { x.PartId, x.OccurredAtUtc });
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.PerformedByUserId);
        builder.HasIndex(x => x.OperationId)
            .IsUnique().HasFilter("\"OperationId\" IS NOT NULL");
        builder.HasIndex(x => x.PurchaseReceiptLineId)
            .IsUnique().HasFilter("\"PurchaseReceiptLineId\" IS NOT NULL");

        builder.HasOne(x => x.Part)
            .WithMany(p => p.Movements)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
