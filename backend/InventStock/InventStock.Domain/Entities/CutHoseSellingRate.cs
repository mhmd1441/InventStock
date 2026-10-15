using InventStock.Domain.Common;

namespace InventStock.Domain.Entities;

/// <summary>An append-only owner-entered USD selling rate per continuous metre.</summary>
public sealed class CutHoseSellingRate : AuditableEntity
{
    private CutHoseSellingRate() { }
    public Guid PartId { get; private set; }
    public int Revision { get; private set; }
    // Null deliberately clears a previously entered rate; history stays available.
    public decimal? UsdPerMeter { get; private set; }
    public Guid ActorId { get; private set; }

    public static CutHoseSellingRate Create(Guid partId, int revision, decimal? usdPerMeter, Guid actorId)
    {
        if (partId == Guid.Empty || actorId == Guid.Empty || revision < 1 ||
            usdPerMeter is <= 0 or > 999999.99m ||
            (usdPerMeter.HasValue && decimal.Round(usdPerMeter.Value, 2) != usdPerMeter.Value))
            throw new ArgumentOutOfRangeException(nameof(usdPerMeter), "Use a positive USD/metre rate with at most two decimal places, or clear it.");
        return new() { PartId = partId, Revision = revision, UsdPerMeter = usdPerMeter, ActorId = actorId };
    }
}
