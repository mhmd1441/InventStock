using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Repositories;
using InventStock.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Tests.Infrastructure;

public class ReferenceTests
{
    [Fact]
    public async Task GenericIdentity_PreservesSeparators_WhileSearchReturnsBothCandidates()
    {
        using var context = new SqliteTestContext();
        var category = Category.Create("Test", null);
        var part = Part.Create("SKU-1", "Test", null, category.Id, null, 1, 0, null);
        context.Db.AddRange(category, part);
        await context.Db.SaveChangesAsync();
        var service = new ReferenceService(context.Db, context.CurrentUser);
        var first = await service.AddAsync(part.Id, new(ReferenceKind.Manufacturer, "Maker", "AB-12"));
        var second = await service.AddAsync(part.Id, new(ReferenceKind.Manufacturer, "Maker", "AB12"));
        var repeat = await service.AddAsync(part.Id, new(ReferenceKind.Manufacturer, " maker ", " ab-12 "));
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(2, await context.Db.Set<CatalogueReference>().CountAsync());
        Assert.Single((await new PartRepository(context.Db).SearchCounterAsync(new() { Search = "AB12" })).Items);
    }

    [Fact]
    public async Task UnlinkRestore_IsScopedAuditedAndRejectsStaleChanges()
    {
        using var context = new SqliteTestContext();
        var category = Category.Create("Test", null);
        var first = Part.Create("FIRST", "Test", null, category.Id, null, 1, 0, null);
        var second = Part.Create("SECOND", "Test", null, category.Id, null, 1, 0, null);
        context.Db.AddRange(category, first, second);
        await context.Db.SaveChangesAsync();
        var service = new ReferenceService(context.Db, context.CurrentUser);
        var reference = await service.AddAsync(first.Id, new(ReferenceKind.OE, null, "REF-123"));
        await service.AddAsync(second.Id, new(ReferenceKind.OE, null, "REF-123"));
        await Assert.ThrowsAsync<ConflictException>(() => service.SetStateAsync(first.Id, reference.Id, new(false, " ", reference.Revision)));
        await service.SetStateAsync(first.Id, reference.Id, new(false, "Wrong hose", reference.Revision));
        Assert.Empty(await service.ListAsync(first.Id));
        Assert.False(Assert.Single(await service.ListAllAsync(first.Id)).IsActive);
        Assert.Equal(second.Id, Assert.Single((await new PartRepository(context.Db).SearchCounterAsync(new() { Search = "REF123" })).Items).Id);
        await Assert.ThrowsAsync<ConflictException>(() => service.AddAsync(first.Id, new(ReferenceKind.OE, null, "REF-123")));
        await Assert.ThrowsAsync<ConflictException>(() => service.SetStateAsync(first.Id, reference.Id, new(true, "Stale edit", reference.Revision)));
        await service.SetStateAsync(first.Id, reference.Id, new(true, "Verified against catalogue", reference.Revision + 1));
        Assert.Equal(2, (await new PartRepository(context.Db).SearchCounterAsync(new() { Search = "REF123" })).TotalCount);
        var history = await service.HistoryAsync(first.Id, reference.Id);
        Assert.Equal(3, history.Count);
        Assert.Equal("Wrong hose", history[1].Reason);
        Assert.All(history, entry => Assert.Equal(context.CurrentUser.UserId, entry.ActorId));
        Assert.Single(await context.Db.Set<CatalogueReference>().ToListAsync());
        context.Db.Remove(await context.Db.Set<ReferenceLinkEvent>().FirstAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("AA2045012782")]
    [InlineData("B2045012782")]
    [InlineData("A204501278")]
    [InlineData("A20450127822")]
    public void Mercedes_RejectsArbitraryOrMalformedPrefixes(string number) =>
        Assert.Throws<InventStock.Domain.Exceptions.DomainException>(() => CatalogueReference.Create(ReferenceKind.MercedesOE, null, number));

    [Fact]
    public async Task SharedNumber_SeparatesBrandsAndRejectsAmbiguousLookup()
    {
        using var context = new SqliteTestContext();
        var category = Category.Create("Hoses", null);
        context.Db.Add(category);
        var brands = new Dictionary<string, Guid>();
        foreach (var brand in new[] { "FEBI", "MATRIX" })
        {
            var brandRecord = ProductBrand.Create(brand);
            brands[brand] = brandRecord.Id;
            context.Db.Add(brandRecord);
            var part = Part.Create("2045012782", "Hose", null, category.Id, null, 15, 0, null);
            part.SetIdentity(brandRecord.NormalizedName, null);
            part.SetBrand(brandRecord);
            context.Db.Add(part);
        }
        await context.Db.SaveChangesAsync();
        var repository = new PartRepository(context.Db);
        var result = await repository.SearchCounterAsync(new() { Search = "2045012782" });
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Select(p => p.Brand).Distinct().Count());
        await Assert.ThrowsAsync<ConflictException>(() => repository.GetCounterByNumberAsync("2045012782"));
        await Assert.ThrowsAsync<ConflictException>(() => repository.GetDtoByPartNumberAsync("2045012782"));
        Assert.True(await repository.PartNumberExistsAsync("2045012782", brands["FEBI"], "", null));
        Assert.False(await repository.PartNumberExistsAsync("2045012782", Guid.NewGuid(), "", null));
    }

    [Fact]
    public async Task References_DeduplicateSpellingsAndLinks_AndSearchAcrossSkus()
    {
        using var context = new SqliteTestContext();
        var category = Category.Create("Hoses", null);
        var first = Part.Create("FE-433", "Hose", null, category.Id, null, 15, 0, null);
        var second = Part.Create("MX-22", "Hose", null, category.Id, null, 12, 0, null);
        context.Db.AddRange(category, first, second);
        await context.Db.SaveChangesAsync();
        var service = new ReferenceService(context.Db, context.CurrentUser);
        foreach (var value in new[] { "A 204 501 27 82", "A2045012782", "204-501-27-82" })
            await service.AddAsync(first.Id, new(ReferenceKind.MercedesOE, null, value));
        await service.AddAsync(second.Id, new(ReferenceKind.MercedesOE, null, "2045012782"));
        Assert.Equal(1, await context.Db.Set<CatalogueReference>().CountAsync());
        Assert.Equal(2, await context.Db.Set<SkuReference>().CountAsync());
        var repository = new PartRepository(context.Db);
        foreach (var value in new[] { "2045012782", "A2045012782" })
        {
            var results = await repository.SearchCounterAsync(new() { Search = value, SearchMode = PartSearchMode.Exact });
            Assert.Equal(2, results.TotalCount);
        }
        Assert.Equal(2, (await repository.SearchCounterAsync(new() { Search = "~/12782" })).TotalCount);
        Assert.Single(await service.ListAsync(first.Id));
    }

    [Fact]
    public void OnlyMercedesRule_IgnoresAPrefix_AndZerosRemainSignificant()
    {
        var ordinary = CatalogueReference.Create(ReferenceKind.OE, "Example", "A00123");
        Assert.Equal("A00123", ordinary.MatchValue);
        Assert.Equal("00123", CatalogueReference.Create(ReferenceKind.WVA, null, "00-123").SearchValue);
    }
}
