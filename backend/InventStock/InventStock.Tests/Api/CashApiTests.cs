using System.Net;
using System.Net.Http.Json;
using InventStock.Application.DTOs.Cash;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventStock.Tests.Api;

public sealed class CashApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Lbp_outgoing_is_idempotent_and_reversal_restores_native_cash_sum()
    {
        Guid accountId;
        Guid categoryId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            accountId = await db.CashAccounts.Where(x => x.Code == "SAFE_LBP").Select(x => x.Id).SingleAsync();
            categoryId = await db.ExpenseCategories.Where(x => x.Name.Contains("Utilities")).Select(x => x.Id).SingleAsync();
        }

        using var admin = await factory.CreateAdminClientAsync();
        using var opening = await admin.PostAsJsonAsync($"/api/admin/cash/accounts/{accountId}/opening-balance",
            new PostCashOpeningBalanceRequest { OperationId = Guid.NewGuid(), CountedAmount = 2_000_000m, CountedAtUtc = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.OK, opening.StatusCode);

        var operationId = Guid.NewGuid();
        var request = new PostOutgoingPaymentRequest
        {
            OperationId = operationId, ExpenseCategoryId = categoryId,
            FundingSource = PaymentFundingSource.ShopAccount, CashAccountId = accountId,
            OriginalAmount = 900_000m, Currency = MoneyCurrency.LBP, AgreedUsdValue = 10m,
            PaidOn = DateOnly.FromDateTime(DateTime.UtcNow), Description = "Electricity"
        };
        using var firstResponse = await admin.PostAsJsonAsync("/api/admin/cash/outgoings", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<OutgoingPaymentDto>();
        Assert.NotNull(first);
        Assert.Equal(900_000m, first.OriginalAmount);
        Assert.Equal(10m, first.AgreedUsdValue);

        using var replayResponse = await admin.PostAsJsonAsync("/api/admin/cash/outgoings", request);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        var replay = await replayResponse.Content.ReadFromJsonAsync<OutgoingPaymentDto>();
        Assert.Equal(first.Id, replay!.Id);

        using var reversalResponse = await admin.PostAsJsonAsync($"/api/admin/cash/outgoings/{first.Id}/reverse",
            new ReverseOutgoingPaymentRequest { OperationId = Guid.NewGuid(), Reason = "Entered against wrong bill", ReversedAtUtc = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.OK, reversalResponse.StatusCode);
        var reversed = await reversalResponse.Content.ReadFromJsonAsync<OutgoingPaymentDto>();
        Assert.True(reversed!.IsReversed);

        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verify.OutgoingPayments.CountAsync(x => x.OperationId == operationId));
        var changes = await verify.CashMovements.Where(x => x.CashAccountId == accountId).Select(x => x.AmountChange).ToListAsync();
        Assert.Equal(2_000_000m, changes.Sum());
    }

    [Fact]
    public async Task Cash_routes_are_admin_only_and_personal_payment_does_not_touch_shop_cash()
    {
        using var staff = await factory.CreateStaffClientAsync();
        using var forbidden = await staff.GetAsync("/api/admin/cash/accounts");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var reconciliationForbidden = await staff.GetAsync("/api/admin/cash/reconciliation?businessDate=2026-01-02");
        Assert.Equal(HttpStatusCode.Forbidden, reconciliationForbidden.StatusCode);
        using var supplierPaymentsForbidden = await staff.GetAsync("/api/admin/cash/supplier-payments?year=2026&month=1");
        Assert.Equal(HttpStatusCode.Forbidden, supplierPaymentsForbidden.StatusCode);

        Guid categoryId;
        int movementCount;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            categoryId = await db.ExpenseCategories.Where(x => x.Name.Contains("government")).Select(x => x.Id).SingleAsync();
            movementCount = await db.CashMovements.CountAsync();
        }

        using var admin = await factory.CreateAdminClientAsync();
        using var response = await admin.PostAsJsonAsync("/api/admin/cash/outgoings", new PostOutgoingPaymentRequest
        {
            OperationId = Guid.NewGuid(), ExpenseCategoryId = categoryId,
            FundingSource = PaymentFundingSource.PersonalPocket, CashAccountId = null,
            OriginalAmount = 2100m, Currency = MoneyCurrency.USD, AgreedUsdValue = 2100m,
            PaidOn = DateOnly.FromDateTime(DateTime.UtcNow), ForPeriod = "2023", Payee = "Municipality"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(movementCount, await verify.CashMovements.CountAsync());
    }
}
