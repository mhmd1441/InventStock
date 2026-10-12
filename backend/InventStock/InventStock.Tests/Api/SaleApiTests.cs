using System.Net;
using System.Net.Http.Json;
using InventStock.Application.DTOs.Sales;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventStock.Tests.Api;

[Collection("PostgreSQL schema migrations")]
public sealed class SaleApiTests(PostgresSaleApiFactory factory) : IClassFixture<PostgresSaleApiFactory>
{
    [Fact]
    public async Task Staff_can_post_and_recover_a_public_sale_but_cannot_access_cash_administration()
    {
        Guid partId;
        Guid accountId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = await db.Categories.FirstAsync();
            var actor = await db.Users.Where(x => x.Email == ApiFactory.StaffEmail).Select(x => x.Id).SingleAsync();
            var part = Part.Create("HTTP-" + Guid.NewGuid().ToString("N"), "Counter hose", null, category.Id, null, 765.43m, 0, null);
            partId = part.Id;
            db.Parts.Add(part);
            db.StockMovements.Add(StockMovement.PhysicalCount(partId, 2, 0, actor, "Test count"));
            await db.SaveChangesAsync();
            accountId = await db.CashAccounts.Where(x => x.Code == "SAFE_USD").Select(x => x.Id).SingleAsync();
        }
        using var staff = await factory.CreateStaffClientAsync();
        var request = new PostSaleRequest
        {
            OperationId = Guid.NewGuid(), ConfirmedTotalUsd = 10,
            Lines = [new(partId, 1, 10)], Payments = [new(accountId, MoneyCurrency.USD, 10, 10)]
        };
        using var post = await staff.PostAsJsonAsync("/api/sales", request);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var json = await post.Content.ReadAsStringAsync();
        foreach (var secret in new[] { "cost", "supplier", "765.43" }) Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
        using var status = await staff.GetAsync($"/api/sales/operations/{request.OperationId}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var recovered = await status.Content.ReadFromJsonAsync<SaleOperationDto>();
        Assert.Equal(request.OperationId, recovered!.Sale.OperationId);
        using var accounts = await staff.GetAsync("/api/sales/payment-accounts");
        Assert.Equal(HttpStatusCode.OK, accounts.StatusCode);
        Assert.DoesNotContain("opening", await accounts.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var privateCash = await staff.GetAsync("/api/admin/cash/accounts");
        Assert.Equal(HttpStatusCode.Forbidden, privateCash.StatusCode);
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.PostAsJsonAsync("/api/sales", request);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var admin = await factory.CreateAdminClientAsync();
        using var otherActor = await admin.PostAsJsonAsync("/api/sales", request);
        Assert.Equal(HttpStatusCode.Conflict, otherActor.StatusCode);
        // This bearer token still carries Staff. Current database authorization must override it.
        using var authorizationScope = factory.Services.CreateScope();
        var authorizationDb = authorizationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await authorizationDb.Users.SingleAsync(x => x.Email == ApiFactory.StaffEmail);
        user.IsActive = false;
        await authorizationDb.SaveChangesAsync();
        try
        {
            using var replayDenied = await staff.PostAsJsonAsync("/api/sales", request);
            using var statusDenied = await staff.GetAsync($"/api/sales/operations/{request.OperationId}");
            Assert.Equal(HttpStatusCode.Forbidden, replayDenied.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, statusDenied.StatusCode);
        }
        finally { user.IsActive = true; await authorizationDb.SaveChangesAsync(); }
    }
}
