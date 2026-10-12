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

    [Fact]
    public async Task Merge_ReassignsPartsAndAliasesAndWritesImmutableAudit()
    {
        using var fixture = new SqliteTestContext();
        var category = Category.Create("Merge", null);
        var source = ProductBrand.Create("Febi Bilstein");
        var target = ProductBrand.Create("FEBI");
        var part = Part.Create("FE-100", "Hose", null, category.Id, null, 1, 0, null);
        part.SetBrand(source);
        fixture.Db.AddRange(category, source, target, part, BrandAlias.Create(source.Id, "Bilstein Group"));
        await fixture.Db.SaveChangesAsync();

        var result = await new BrandRepository(fixture.Db).MergeAsync(source.Id, target.Id, "Same manufacturer");

        Assert.Equal(1, result.ReassignedPartCount);
        Assert.Equal(target.Id, (await fixture.Db.Parts.SingleAsync()).BrandId);
        Assert.False((await fixture.Db.ProductBrands.SingleAsync(x => x.Id == source.Id)).IsActive);
        Assert.Contains(await fixture.Db.BrandAliases.Where(x => x.ProductBrandId == target.Id).Select(x => x.Value).ToListAsync(), x => x == "Febi Bilstein");
        Assert.Single(fixture.Db.BrandMergeEvents);
        fixture.Db.Remove(await fixture.Db.BrandMergeEvents.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Merge_RejectsSkuIdentityCollision()
    {
        using var fixture = new SqliteTestContext();
        var category = Category.Create("Merge collision", null);
        var source = ProductBrand.Create("Old name");
        var target = ProductBrand.Create("New name");
        var first = Part.Create("SAME-1", "First", null, category.Id, null, 1, 0, null);
        var second = Part.Create("SAME-1", "Second", null, category.Id, null, 1, 0, null);
        first.SetBrand(source); second.SetBrand(target);
        fixture.Db.AddRange(category, source, target, first, second);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InventStock.Application.Common.Exceptions.ConflictException>(() =>
            new BrandRepository(fixture.Db).MergeAsync(source.Id, target.Id, "Duplicate cleanup"));
        Assert.Empty(fixture.Db.BrandMergeEvents);
    }
}
