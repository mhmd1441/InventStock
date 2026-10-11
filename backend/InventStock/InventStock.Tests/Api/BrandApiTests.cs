using System.Net;
using System.Net.Http.Json;
using InventStock.Application.DTOs.Catalog;

namespace InventStock.Tests.Api;

public sealed class BrandApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Staff_CanListBrands_ButCannotCreateThem()
    {
        var client = await factory.CreateStaffClientAsync();
        var list = await client.GetAsync("/api/brands");
        var create = await client.PostAsJsonAsync("/api/brands", new SaveBrandRequest("Unauthorized brand"));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Admin_CreatesCanonicalBrand_AndPartSelectsItsId()
    {
        var client = await factory.CreateAdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var brandResponse = await client.PostAsJsonAsync("/api/brands", new SaveBrandRequest($"  Test   Brand {suffix}  "));
        brandResponse.EnsureSuccessStatusCode();
        var brand = await brandResponse.Content.ReadFromJsonAsync<BrandDto>();
        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");

        var partResponse = await client.PostAsJsonAsync("/api/parts", new SavePartRequest
        {
            PartNumber = $"BRAND-{suffix}", BrandId = brand!.Id, Name = "Controlled-brand part",
            CategoryId = categories![0].Id, UnitPrice = 2m
        });
        partResponse.EnsureSuccessStatusCode();
        var part = await partResponse.Content.ReadFromJsonAsync<PartDto>();

        Assert.Equal(brand.Id, part!.BrandId);
        Assert.Equal(brand.Name.ToUpperInvariant(), part.Brand);
    }
}
