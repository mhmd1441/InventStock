using InventStock.Domain.Entities;
using InventStock.Infrastructure.Repositories;

namespace InventStock.Tests.Infrastructure;

public class LowStockQueryTests
{
    private static async Task<(SqliteTestContext Context, PartRepository Repository)> SeedAsync()
    {
        var context = new SqliteTestContext();
        var userId = context.CurrentUser.UserId!.Value;

        var category = Category.Create("Brakes", null);
        context.Db.Categories.Add(category);
        await context.Db.SaveChangesAsync();

        // threshold, opening stock
        var specs = new[]
        {
            ("HEALTHY-1", 5, 50),
            ("AT-THRESHOLD", 10, 10),
            ("BELOW-1", 10, 4),
            ("EMPTY-1", 3, 0)
        };

        foreach (var (partNumber, threshold, opening) in specs)
        {
            var part = Part.Create(partNumber, partNumber, null, category.Id, null, 10m, threshold, null);
            context.Db.Parts.Add(part);
            await context.Db.SaveChangesAsync();

            if (opening > 0)
            {
                context.Db.StockMovements.Add(
                    StockMovement.StockIn(part.Id, opening, userId, "Opening", null));
                await context.Db.SaveChangesAsync();
            }
        }

        return (context, new PartRepository(context.Db));
    }

    [Fact]
    public async Task GetLowStock_ReturnsOnlyPartsAtOrBelowTheirThreshold()
    {
        var (context, repository) = await SeedAsync();
        using var _ = context;

        var lowStock = await repository.GetLowStockAsync(10);
        var partNumbers = lowStock.Select(p => p.PartNumber).ToList();

        Assert.DoesNotContain("HEALTHY-1", partNumbers);
        Assert.Contains("AT-THRESHOLD", partNumbers);
        Assert.Contains("BELOW-1", partNumbers);
        Assert.Contains("EMPTY-1", partNumbers);
    }

    [Fact]
    public async Task GetLowStock_OrdersByHowFarBelowThresholdTheyAre()
    {
        var (context, repository) = await SeedAsync();
        using var _ = context;

        var lowStock = await repository.GetLowStockAsync(10);

        // BELOW-1 is 6 under, EMPTY-1 is 3 under, AT-THRESHOLD is exactly on it.
        Assert.Equal(
            new[] { "BELOW-1", "EMPTY-1", "AT-THRESHOLD" },
            lowStock.Select(p => p.PartNumber).ToArray());
    }

    [Fact]
    public async Task GetLowStock_RespectsTheLimit()
    {
        var (context, repository) = await SeedAsync();
        using var _ = context;

        var lowStock = await repository.GetLowStockAsync(2);

        Assert.Equal(2, lowStock.Count);
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveParts()
    {
        var (context, repository) = await SeedAsync();
        using var _ = context;

        var below = context.Db.Parts.First(p => p.PartNumber == "BELOW-1");
        below.Deactivate();
        await context.Db.SaveChangesAsync();

        var lowStock = await repository.GetLowStockAsync(10);

        Assert.DoesNotContain("BELOW-1", lowStock.Select(p => p.PartNumber));
    }
}
