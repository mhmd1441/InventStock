namespace InventStock.Domain.Entities;

public sealed class PricingPolicy
{
    public int Id { get; private set; } = 1;
    public decimal MarkupPercent { get; private set; } = 15m;
    public decimal TvaPercent { get; private set; } = 11m;
    public decimal? EurUsdRate { get; private set; }
    public decimal? LbpPerUsdRate { get; private set; }
    public int Revision { get; private set; }
    public Guid? UpdatedById { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private PricingPolicy() { }

    public void Update(decimal markupPercent, decimal tvaPercent, decimal? eurUsdRate,
        decimal? lbpPerUsdRate, Guid actorId)
    {
        if (actorId == Guid.Empty || markupPercent is < 0 or > 1000 || tvaPercent is < 0 or > 100 ||
            eurUsdRate is <= 0 or > 1000 || lbpPerUsdRate is <= 0 or > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(markupPercent), "Invalid pricing policy or exchange rate.");
        MarkupPercent = markupPercent;
        TvaPercent = tvaPercent;
        EurUsdRate = eurUsdRate;
        LbpPerUsdRate = lbpPerUsdRate;
        UpdatedById = actorId;
        UpdatedAtUtc = DateTime.UtcNow;
        Revision++;
    }
}
