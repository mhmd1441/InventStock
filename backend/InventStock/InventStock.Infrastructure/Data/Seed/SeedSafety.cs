namespace InventStock.Infrastructure.Data.Seed;

/// <summary>Prevents sample data from ever being treated as production data.</summary>
public static class SeedSafety
{
    public static void EnsureDemoSeedIsAllowed(bool isProduction, SeedOptions options)
    {
        if (isProduction && options.IncludeDemoData)
        {
            throw new InvalidOperationException(
                "Seed:IncludeDemoData cannot be enabled in Production. Remove it before starting InventStock.");
        }
    }
}
