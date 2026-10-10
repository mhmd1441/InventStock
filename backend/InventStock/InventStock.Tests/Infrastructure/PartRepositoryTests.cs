using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Repositories;

namespace InventStock.Tests.Infrastructure;

public class PartRepositoryTests
{
    private static async Task<(SqliteTestContext Context, PartRepository Repository, Guid PartId)> SeedAsync(
        int lowStockThreshold = 5,
        params int[] movements)
    {
        var context = new SqliteTestContext();
        var userId = context.CurrentUser.UserId!.Value;

        var category = Category.Create("Brakes", null);
        context.Db.Categories.Add(category);
        await context.Db.SaveChangesAsync();

        var part = Part.Create("BRK-1001", "Front Brake Pad", null, category.Id, null, 42.50m, lowStockThreshold, null);
        context.Db.Parts.Add(part);
        await context.Db.SaveChangesAsync();

        foreach (var quantity in movements)
        {
            context.Db.StockMovements.Add(quantity >= 0
                ? StockMovement.StockIn(part.Id, quantity, userId, null, null)
                : StockMovement.StockOut(part.Id, -quantity, userId, "Workshop issue", null));
        }

        await context.Db.SaveChangesAsync();

        return (context, new PartRepository(context.Db), part.Id);
    }

    [Fact]
    public async Task GetQuantityOnHand_SumsSignedMovements()
    {
        var (context, repository, partId) = await SeedAsync(5, 20, -5, -2);
        using var _ = context;

        Assert.Equal(13, await repository.GetQuantityOnHandAsync(partId));
    }

    [Fact]
    public async Task GetQuantityOnHand_IsZeroWhenNoMovementsExist()
    {
        var (context, repository, partId) = await SeedAsync();
        using var _ = context;

        Assert.Equal(0, await repository.GetQuantityOnHandAsync(partId));
    }

    [Fact]
    public async Task Projection_ExposesQuantityOnHandAndLowStockFlag()
    {
        var (context, repository, partId) = await SeedAsync(10, 20, -15);
        using var _ = context;

        var dto = await repository.GetDtoByIdAsync(partId);

        Assert.NotNull(dto);
        Assert.Equal(5, dto!.QuantityOnHand);
        Assert.True(dto.IsLowStock);
        Assert.Equal(5 * 42.50m, dto.StockValue);
        Assert.Equal("Brakes", dto.CategoryName);
    }

    [Fact]
    public async Task Search_LowStockOnly_ExcludesHealthyParts()
    {
        var (context, repository, _) = await SeedAsync(2, 50);
        using var _unused = context;

        var all = await repository.SearchAsync(new PartQuery());
        var lowStock = await repository.SearchAsync(new PartQuery { LowStockOnly = true });

        Assert.Single(all.Items);
        Assert.Empty(lowStock.Items);
    }

    [Fact]
    public async Task Search_LowStockOnly_IncludesPartsAtOrBelowThreshold()
    {
        var (context, repository, _) = await SeedAsync(10, 10);
        using var _unused = context;

        var lowStock = await repository.SearchAsync(new PartQuery { LowStockOnly = true });

        Assert.Single(lowStock.Items);
    }

    [Fact]
    public async Task Search_MatchesPartNumberCaseInsensitively()
    {
        var (context, repository, _) = await SeedAsync();
        using var _unused = context;

        var result = await repository.SearchAsync(new PartQuery { Search = "brk-10" });

        Assert.Single(result.Items);
        Assert.Equal("BRK-1001", result.Items[0].PartNumber);
    }

    [Fact]
    public async Task Search_ExcludesInactivePartsUnlessRequested()
    {
        var (context, repository, partId) = await SeedAsync();
        using var _unused = context;

        var part = await repository.GetByIdAsync(partId);
        part!.Deactivate();
        await context.Db.SaveChangesAsync();

        Assert.Empty((await repository.SearchAsync(new PartQuery())).Items);
        Assert.Single((await repository.SearchAsync(new PartQuery { IncludeInactive = true })).Items);
    }

    [Fact]
    public async Task PartNumberExists_IgnoresTheExcludedPart()
    {
        var (context, repository, partId) = await SeedAsync();
        using var _unused = context;

        Assert.True(await repository.PartNumberExistsAsync("BRK-1001", null));
        Assert.False(await repository.PartNumberExistsAsync("BRK-1001", partId));
        Assert.True(await repository.PartNumberExistsAsync("brk-1001", null));
    }

    [Fact]
    public async Task Search_PaginatesResults()
    {
        var context = new SqliteTestContext();
        using var _unused = context;

        var category = Category.Create("Filters", null);
        context.Db.Categories.Add(category);
        await context.Db.SaveChangesAsync();

        for (var i = 1; i <= 25; i++)
        {
            context.Db.Parts.Add(Part.Create($"FLT-{i:D4}", $"Filter {i}", null, category.Id, null, 1m, 0, null));
        }

        await context.Db.SaveChangesAsync();

        var repository = new PartRepository(context.Db);
        var page2 = await repository.SearchAsync(new PartQuery { Page = 2, PageSize = 10 });

        Assert.Equal(25, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal("FLT-0011", page2.Items[0].PartNumber);
        Assert.True(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);
    }
}
