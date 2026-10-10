using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Tests.Infrastructure;

public class CatalogueSearchTests
{
    private static async Task<SqliteTestContext> Seed()
    {
        var context = new SqliteTestContext();
        var category = Category.Create("Hoses", null);
        context.Db.Categories.Add(category);
        await context.Db.SaveChangesAsync();
        foreach (var number in new[] { "12582", "12582-99", "204-501-2582", "991258200", "0012582", "A2045012582" })
            context.Db.Parts.Add(Part.Create(number, "Coolant hose", null, category.Id, null, 15m, 0, null));
        await context.Db.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task Auto_RanksExactPrefixSuffixThenContains()
    {
        using var context = await Seed();
        var result = await new PartRepository(context.Db).SearchCounterAsync(new() { Search = "12582" });
        Assert.Equal("12582", result.Items[0].PartNumber);
        Assert.Equal("12582-99", result.Items[1].PartNumber);
        Assert.Equal("991258200", result.Items[^1].PartNumber);
        Assert.Equal(6, result.TotalCount);
    }

    [Theory]
    [InlineData("~/12582", PartSearchMode.Auto)]
    [InlineData("12582", PartSearchMode.Suffix)]
    public async Task Suffix_ExcludesPrefixAndMiddleMatches(string search, PartSearchMode mode)
    {
        using var context = await Seed();
        var result = await new PartRepository(context.Db).SearchCounterAsync(new() { Search = search, SearchMode = mode });
        Assert.Equal(4, result.TotalCount);
        Assert.DoesNotContain(result.Items, p => p.PartNumber is "12582-99" or "991258200");
    }

    [Theory]
    [InlineData("2045012582", "204-501-2582")]
    [InlineData("204 501 25 82", "204-501-2582")]
    [InlineData("a2045012582", "A2045012582")]
    [InlineData("0012582", "0012582")]
    public async Task Exact_NormalizesSeparatorsButPreservesLettersAndZeros(string search, string expected)
    {
        using var context = await Seed();
        var result = await new PartRepository(context.Db).SearchCounterAsync(new() { Search = search, SearchMode = PartSearchMode.Exact });
        Assert.Equal(expected, Assert.Single(result.Items).PartNumber);
    }

    [Fact]
    public async Task GeneratedKey_UpdatesWhenPartNumberChanges()
    {
        using var context = await Seed();
        var part = await context.Db.Parts.FirstAsync(p => p.PartNumber == "12582");
        part.Update("NEW-777", part.Name, null, part.CategoryId, null, 15m, 0, null);
        await context.Db.SaveChangesAsync();
        var result = await new PartRepository(context.Db).SearchCounterAsync(new() { Search = "new777", SearchMode = PartSearchMode.Exact });
        Assert.Equal(part.Id, Assert.Single(result.Items).Id);
    }

    [Theory]
    [InlineData("~/")]
    [InlineData("~/12")]
    [InlineData("---")]
    public async Task InvalidSearch_HasAnExplicitError(string search)
    {
        using var context = await Seed();
        await Assert.ThrowsAsync<ConflictException>(() => new PartRepository(context.Db).SearchCounterAsync(new() { Search = search }));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task PatternCharacters_AreNotWildcards(string search)
    {
        using var context = await Seed();
        var result = await new PartRepository(context.Db).SearchCounterAsync(new() { Search = search });
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task NameSearch_AndPaginationWorkTogether()
    {
        using var context = await Seed();
        var result = await new PartRepository(context.Db).SearchCounterAsync(new() { Search = "coolant", PageSize = 2, Page = 2 });
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }
}
