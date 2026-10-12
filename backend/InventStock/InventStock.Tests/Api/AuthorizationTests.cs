using System.Net;
using System.Net.Http.Json;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Auth;
using InventStock.Application.DTOs.Catalog;
using InventStock.Application.DTOs.Stock;

namespace InventStock.Tests.Api;

public class AuthorizationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthorizationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectAnonymousCallers()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/parts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = ApiFactory.AdminEmail,
            Password = "definitely-not-the-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_IssuesAnAccessTokenAndRefreshCookie()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = ApiFactory.AdminEmail,
            Password = ApiFactory.AdminPassword
        });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResult>();

        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.NotEqual(Guid.Empty, auth.SessionId);
        Assert.Contains("Admin", auth.User.Roles);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var refreshCookie = Assert.Single(cookies, c => c.StartsWith("inventstock_refresh"));
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", refreshCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticatedStaff_CanReadTheCatalogue()
    {
        var client = await _factory.CreateStaffClientAsync();

        var parts = await client.GetFromJsonAsync<PagedResult<PartDto>>("/api/parts");

        Assert.NotNull(parts);
        Assert.True(parts!.TotalCount > 0);
    }

    [Fact]
    public async Task Staff_CannotCreateParts()
    {
        var client = await _factory.CreateStaffClientAsync();

        var response = await client.PostAsJsonAsync("/api/parts", new SavePartRequest
        {
            PartNumber = "NEW-0001",
            Name = "Unauthorised part",
            CategoryId = Guid.NewGuid(),
            UnitPrice = 1m,
            LowStockThreshold = 0
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_CannotPostStockAdjustmentsOrPhysicalCounts()
    {
        var client = await _factory.CreateStaffClientAsync();
        var parts = await client.GetFromJsonAsync<PagedResult<PartDto>>("/api/parts");
        var partId = parts!.Items[0].Id;

        var response = await client.PostAsJsonAsync($"/api/parts/{partId}/stock/adjust", new StockAdjustmentRequest
        {
            OperationId = Guid.NewGuid(),
            QuantityChange = -1,
            Reason = "Shrinkage"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var countResponse = await client.PostAsJsonAsync($"/api/parts/{partId}/stock/count", new StockCountRequest
        {
            OperationId = Guid.NewGuid(),
            CountedQuantity = 1,
            ExpectedBookQuantity = 1
        });
        Assert.Equal(HttpStatusCode.Forbidden, countResponse.StatusCode);
    }

    [Fact]
    public async Task Staff_CanPostStockMovements()
    {
        var client = await _factory.CreateStaffClientAsync();
        var parts = await client.GetFromJsonAsync<PagedResult<PartDto>>("/api/parts");
        var part = parts!.Items[0];

        var response = await client.PostAsJsonAsync($"/api/parts/{part.Id}/stock/in", new StockInRequest
        {
            OperationId = Guid.NewGuid(),
            Quantity = 3,
            Reason = "Delivery"
        });

        response.EnsureSuccessStatusCode();
        var movement = await response.Content.ReadFromJsonAsync<StockMovementDto>();

        Assert.Equal(3, movement!.QuantityChange);
        Assert.Equal("Integration Staff", movement.PerformedByUserName);
    }

    [Fact]
    public async Task Staff_CanPhysicallyHoldStock_ButOnlyAdminCanReleaseIt()
    {
        var staff = await _factory.CreateStaffClientAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var parts = await staff.GetFromJsonAsync<PagedResult<CounterPartDto>>("/api/parts");
        var partId = parts!.Items[0].Id;
        await staff.PostAsJsonAsync($"/api/parts/{partId}/stock/in", new StockInRequest { OperationId = Guid.NewGuid(), Quantity = 5 });

        var hold = await staff.PostAsJsonAsync($"/api/parts/{partId}/stock/hold", new StockHoldRequest(
            Guid.NewGuid(), 3, "Suspected short hose", true));
        hold.EnsureSuccessStatusCode();
        var afterHold = await staff.GetFromJsonAsync<CounterPartDto>($"/api/parts/{partId}");
        Assert.Equal(3, afterHold!.QuarantinedQuantity);
        Assert.Equal(afterHold.PhysicalQuantityOnHand - 3, afterHold.QuantityOnHand);

        var blocked = await staff.PostAsJsonAsync($"/api/parts/{partId}/stock/out", new StockOutRequest {
            OperationId = Guid.NewGuid(), Quantity = afterHold.QuantityOnHand + 1, Reason = "Must not consume hold" });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var releaseRequest = new StockReleaseRequest(Guid.NewGuid(), 3, "Supplier confirmed correct");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/parts/{partId}/stock/release", releaseRequest)).StatusCode);
        (await admin.PostAsJsonAsync($"/api/parts/{partId}/stock/release", releaseRequest)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Admin_CanManageTheCatalogue()
    {
        var client = await _factory.CreateAdminClientAsync();

        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        var categoryId = categories![0].Id;

        var response = await client.PostAsJsonAsync("/api/parts", new SavePartRequest
        {
            PartNumber = "adm-0001",
            Name = "Administrator created part",
            CategoryId = categoryId,
            UnitPrice = 12.34m,
            LowStockThreshold = 2
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PartDto>();

        Assert.Equal("ADM-0001", created!.PartNumber);
        Assert.Equal(0, created.QuantityOnHand);
        Assert.Equal(InventStock.Domain.Enums.StockConfidence.NotCounted, created.StockConfidence);
        Assert.False(created.IsLowStock);
    }

    [Fact]
    public async Task InvalidPayload_ReturnsValidationProblemDetails()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/categories", new SaveCategoryRequest { Name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.True(problem!.Errors.ContainsKey("Name"));
    }

    [Fact]
    public async Task StockOut_BeyondAvailableStock_ReturnsConflict()
    {
        var client = await _factory.CreateStaffClientAsync();
        var parts = await client.GetFromJsonAsync<PagedResult<PartDto>>("/api/parts");
        var part = parts!.Items.First();

        var response = await client.PostAsJsonAsync($"/api/parts/{part.Id}/stock/out", new StockOutRequest
        {
            OperationId = Guid.NewGuid(),
            Quantity = part.QuantityOnHand + 1_000,
            Reason = "Oversell test"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPart_ReturnsNotFound()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.GetAsync($"/api/parts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class ValidationProblemResponse
    {
        public Dictionary<string, string[]> Errors { get; set; } = new();
    }
}
