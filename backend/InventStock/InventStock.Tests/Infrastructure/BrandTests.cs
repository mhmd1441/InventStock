using InventStock.Domain.Entities;
using InventStock.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Tests.Infrastructure;

public sealed class BrandTests
{
    [Fact]
    public async Task SameNumberAndVariant_WithUnknownBrand_CannotSplitIntoDuplicateSkus()
    {
        using var fixture = new SqliteTestContext();
        var category = Category.Create("Duplicate guard", null);
        fixture.Db.Categories.Add(category);
        fixture.Db.Parts.Add(Part.Create("NO-BRAND-1", "First", null, category.Id, null, 1, 0, null));
        await fixture.Db.SaveChangesAsync();
        fixture.Db.Parts.Add(Part.Create("NO-BRAND-1", "Second", null, category.Id, null, 1, 0, null));

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public void Normalization_CollapsesCaseAndWhitespace()
    {
        Assert.Equal("FEBI BILSTEIN", ProductBrand.Normalize("  Febi   Bilstein "));
        Assert.Equal("FEBIBILSTEIN", ProductBrand.CandidateKey("Febi-Bilstein"));
    }

    [Fact]
    public async Task CanonicalNameAndAlias_AreGloballyUnique()
    {
        using var fixture = new SqliteTestContext();
        var febi = ProductBrand.Create("FEBI Bilstein");
        fixture.Db.ProductBrands.Add(febi);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.BrandAliases.Add(BrandAlias.Create(febi.Id, "FEBI"));
        await fixture.Db.SaveChangesAsync();

        var repository = new BrandRepository(fixture.Db);
        Assert.True(await repository.NameOrAliasExistsAsync("FEBI BILSTEIN"));
        Assert.True(await repository.NameOrAliasExistsAsync("FEBI"));
    }

    [Fact]
    public async Task CandidateSearch_WarnsAboutCompactOrExpandedSpellings()
    {
        using var fixture = new SqliteTestContext();
        fixture.Db.ProductBrands.Add(ProductBrand.Create("FEBI BILSTEIN"));
        await fixture.Db.SaveChangesAsync();

        var candidates = await new BrandRepository(fixture.Db).FindCandidatesAsync("FebiBilstein");

        Assert.Single(candidates);
        Assert.Equal("FEBI BILSTEIN", candidates[0].Name);
    }
}
