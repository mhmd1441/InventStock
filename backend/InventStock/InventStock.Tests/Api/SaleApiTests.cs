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
    public async Task Staff_linked_refund_is_public_safe_and_actor_scoped()
    {
        Guid partId;
        Guid accountId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = await db.Categories.FirstAsync();
            var actor = await db.Users.Where(x => x.Email == ApiFactory.StaffEmail).Select(x => x.Id).SingleAsync();
            var part = Part.Create("RET-" + Guid.NewGuid().ToString("N"), "Return hose", null, category.Id, null, 987.65m, 0, null);
            partId = part.Id;
            db.Parts.Add(part);
            db.StockMovements.Add(StockMovement.PhysicalCount(partId, 1, 0, actor, "Test count"));
            await db.SaveChangesAsync();
            accountId = await db.CashAccounts.Where(x => x.Code == "SAFE_USD").Select(x => x.Id).SingleAsync();
        }
        using var staff = await factory.CreateStaffClientAsync();
        var saleRequest = new PostSaleRequest { OperationId = Guid.NewGuid(), ConfirmedTotalUsd = 10,
            Lines = [new(partId, 1, 10)], Payments = [new(accountId, MoneyCurrency.USD, 10, 10)] };
        using var saleResponse = await staff.PostAsJsonAsync("/api/sales", saleRequest);
        Assert.Equal(HttpStatusCode.Created, saleResponse.StatusCode);
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleDto>();
        using var recent = await staff.GetAsync("/api/sales/recent");
        Assert.Equal(HttpStatusCode.OK, recent.StatusCode);
        var recentJson = await recent.Content.ReadAsStringAsync();
        Assert.Contains(sale!.Id.ToString(), recentJson, StringComparison.OrdinalIgnoreCase);
        foreach (var secret in new[] { "supplier", "cost", "987.65" })
            Assert.DoesNotContain(secret, recentJson, StringComparison.OrdinalIgnoreCase);
        using var detail = await staff.GetAsync($"/api/sales/{sale.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.DoesNotContain("cost", await detail.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var before = await staff.GetAsync($"/api/sale-returns/sales/{sale.Id}/availability");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeValue = await before.Content.ReadFromJsonAsync<ReturnableSaleDto>();
        Assert.Equal(1, beforeValue!.Lines.Single().RemainingQuantity);
        Assert.Equal(10m, beforeValue.Lines.Single().RemainingRefundUsd);
        foreach (var secret in new[] { "supplier", "cost", "987.65" })
            Assert.DoesNotContain(secret, await before.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var request = new PostSaleReturnRequest { OperationId = Guid.NewGuid(), SaleId = sale!.Id,
            Reason = "Customer changed mind", ConfirmedTotalUsd = 10,
            Lines = [new(sale.Lines[0].Id, 1, ReturnCondition.Sellable)],
            Refunds = [new(accountId, MoneyCurrency.USD, 10, 10)] };
        using var posted = await staff.PostAsJsonAsync("/api/sale-returns", request);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
        var json = await posted.Content.ReadAsStringAsync();
        foreach (var secret in new[] { "supplier", "cost", "987.65" }) Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
        using var status = await staff.GetAsync($"/api/sale-returns/operations/{request.OperationId}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.DoesNotContain("supplier", await status.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var after = await staff.GetAsync($"/api/sale-returns/sales/{sale.Id}/availability");
        var afterValue = await after.Content.ReadFromJsonAsync<ReturnableSaleDto>();
        Assert.Equal(0, afterValue!.Lines.Single().RemainingQuantity);
        Assert.Equal(0m, afterValue.Lines.Single().RemainingRefundUsd);
        using var admin = await factory.CreateAdminClientAsync();
        using var otherActor = await admin.GetAsync($"/api/sale-returns/operations/{request.OperationId}");
        Assert.Equal(HttpStatusCode.NotFound, otherActor.StatusCode);
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.PostAsJsonAsync("/api/sale-returns", request);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Staff_shortage_sale_is_flagged_and_catalogue_requires_recount()
    {
        Guid partId;
        Guid accountId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = await db.Categories.FirstAsync();
            var actor = await db.Users.Where(x => x.Email == ApiFactory.StaffEmail).Select(x => x.Id).SingleAsync();
            var part = Part.Create("SHORT-" + Guid.NewGuid().ToString("N"), "Confirmed hose", null, category.Id, null, 123.45m, 0, null);
            partId = part.Id;
            db.Parts.Add(part);
            db.StockMovements.Add(StockMovement.PhysicalCount(partId, 0, 0, actor, "Counted zero"));
            await db.SaveChangesAsync();
            accountId = await db.CashAccounts.Where(x => x.Code == "SAFE_USD").Select(x => x.Id).SingleAsync();
        }
        using var staff = await factory.CreateStaffClientAsync();
        var request = new PostSaleRequest
        {
            OperationId = Guid.NewGuid(), ConfirmedTotalUsd = 10,
            Lines = [new(partId, 1, 10, true, "Physically found separate sellable hose")],
            Payments = [new(accountId, MoneyCurrency.USD, 10, 10)]
        };
        using var posted = await staff.PostAsJsonAsync("/api/sales", request);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
        var result = await posted.Content.ReadFromJsonAsync<SaleDto>();
        Assert.True(result!.Lines.Single().StockShortage);
        using var catalogue = await staff.GetAsync($"/api/parts/{partId}");
        Assert.Equal(HttpStatusCode.OK, catalogue.StatusCode);
        var publicJson = await catalogue.Content.ReadAsStringAsync();
        Assert.Contains("NeedsRecount", publicJson);
        foreach (var secret in new[] { "supplier", "cost", "123.45" }) Assert.DoesNotContain(secret, publicJson, StringComparison.OrdinalIgnoreCase);
        using var counted = await staff.PostAsJsonAsync($"/api/parts/{partId}/stock/count",
            new InventStock.Application.DTOs.Stock.StockCountRequest { OperationId = Guid.NewGuid(), CountedQuantity = 1,
                ExpectedBookQuantity = -1, Reason = "Physical shelf recount after shortage" });
        Assert.Equal(HttpStatusCode.Created, counted.StatusCode);
        using var countedPart = await staff.GetAsync($"/api/parts/{partId}");
        Assert.DoesNotContain("NeedsRecount", await countedPart.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

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
