using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.Interfaces;

namespace InventStock.Tests.Api;

public class CounterPrivacyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public CounterPrivacyTests(ApiFactory factory) => _factory = factory;

    private static readonly HashSet<string> PrivateFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "unitPrice", "stockValue", "supplierId", "supplierName", "totalSuppliers", "totalStockValue"
    };

    private static void AssertCounterJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, PrivateFields);
                AssertCounterJson(property.Value);
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) AssertCounterJson(item);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CounterRoutes_NeverSerializePrivateFields_EvenForAdmin(bool admin)
    {
        using var client = admin ? await _factory.CreateAdminClientAsync() : await _factory.CreateStaffClientAsync();
        var page = await client.GetFromJsonAsync<PagedResult<CounterPartDto>>("/api/parts");
        var part = page!.Items[0];
        foreach (var path in new[] { "/api/parts", "/api/parts/low-stock", $"/api/parts/{part.Id}",
                     $"/api/parts/by-number/{Uri.EscapeDataString(part.PartNumber)}", $"/api/parts/{part.Id}/references", "/api/dashboard" })
        {
            using var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            AssertCounterJson(json.RootElement);
        }
    }

    [Theory]
    [InlineData("/api/suppliers")]
    [InlineData("/api/suppliers/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/admin/parts")]
    [InlineData("/api/admin/parts/low-stock")]
    [InlineData("/api/admin/parts/by-number/BRK-1001")]
    [InlineData("/api/admin/parts/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/admin/dashboard")]
    public async Task Staff_CannotReadPrivateRoutes(string path)
    {
        using var client = await _factory.CreateStaffClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("sortBy=UnitPrice")]
    [InlineData("supplierId=00000000-0000-0000-0000-000000000001")]
    public async Task CounterRoutes_RejectPrivateInferenceFilters(string query)
    {
        using var client = await _factory.CreateStaffClientAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/parts?{query}")).StatusCode);
    }

    [Fact]
    public async Task Staff_CannotCorrectReferences_AndStockOutRequiresReason()
    {
        using var staff = await _factory.CreateStaffClientAsync();
        var parts = await staff.GetFromJsonAsync<PagedResult<CounterPartDto>>("/api/parts");
        var id = parts!.Items[0].Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync($"/api/parts/{id}/stock/out", new { quantity = 1, reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PatchAsJsonAsync($"/api/parts/{id}/references/{Guid.NewGuid()}/state", new { isActive = false, reason = "Wrong", expectedRevision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/parts/{id}/references/all")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/parts/{id}/references/{Guid.NewGuid()}/history")).StatusCode);
    }

    [Fact]
    public async Task ReferenceWrites_RequireAdmin_AndAreSearchableByStaff()
    {
        using var admin = await _factory.CreateAdminClientAsync();
        using var staff = await _factory.CreateStaffClientAsync();
        var parts = await staff.GetFromJsonAsync<PagedResult<CounterPartDto>>("/api/parts");
        var id = parts!.Items[0].Id;
        var path = $"/api/parts/{id}/references";
        var request = new { kind = "MercedesOE", issuer = "", value = "A 204 501 27 82" };
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync(path, request)).StatusCode);
        var added = await admin.PostAsJsonAsync(path, request);
        Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync());
        var references = await staff.GetFromJsonAsync<List<ReferenceDto>>(path);
        Assert.Contains(references!, r => r.Value == request.value);
        var results = await staff.GetFromJsonAsync<PagedResult<CounterPartDto>>("/api/parts?search=2045012782&searchMode=Exact");
        Assert.Contains(results!.Items, p => p.Id == id);
    }

    [Fact]
    public async Task Admin_CanReadCostsOnlyThroughPrivateRoute_WithNoStore()
    {
        using var client = await _factory.CreateAdminClientAsync();
        using var response = await client.GetAsync("/api/admin/parts");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var parts = await response.Content.ReadFromJsonAsync<PagedResult<PartDto>>();
        Assert.Contains(parts!.Items, x => x.UnitPrice > 0 && x.SupplierName != null);
    }
}
