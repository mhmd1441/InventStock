using System.Net;
using System.Net.Http.Json;
using InventStock.Application.DTOs.Customers;
using InventStock.Application.DTOs.Sales;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventStock.Tests.Api;

[Collection("PostgreSQL schema migrations")]
public sealed class CustomerApiTests(PostgresSaleApiFactory factory) : IClassFixture<PostgresSaleApiFactory>
{
    [Fact]
    public async Task Staff_can_manage_customer_payments_but_not_opening_balances_or_private_fields()
    {
        using var staff = await factory.CreateStaffClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        using var anonymous = factory.CreateClient();
        var create = new SaveCustomerRequest(Guid.NewGuid(), "Ahmad Garage", "Second garage", "+96170000001");
        using var createdResponse = await staff.PostAsJsonAsync("/api/customers", create);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<CustomerProfileDto>();
        Assert.NotNull(created);
        var opening = new PostCustomerOpeningRequest(Guid.NewGuid(), 20, new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Reviewed book");
        using var staffOpening = await staff.PostAsJsonAsync($"/api/customers/{created.Id}/opening-balance", opening);
        Assert.Equal(HttpStatusCode.Forbidden, staffOpening.StatusCode);
        using var adminOpening = await admin.PostAsJsonAsync($"/api/customers/{created.Id}/opening-balance", opening);
        Assert.Equal(HttpStatusCode.OK, adminOpening.StatusCode);
        var postedOpening = await adminOpening.Content.ReadFromJsonAsync<CustomerOpeningDto>();
        Assert.NotNull(postedOpening);
        using var staffOpeningStatus = await staff.GetAsync($"/api/customers/{created.Id}/opening-balance");
        Assert.Equal(HttpStatusCode.Forbidden, staffOpeningStatus.StatusCode);
        Guid accountId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            accountId = await db.CashAccounts.Where(x => x.Code == "SAFE_LBP").Select(x => x.Id).SingleAsync();
        }
        var payment = new PostCustomerPaymentRequest
        {
            OperationId = Guid.NewGuid(), ConfirmedUsdCredit = 10,
            Payments = [new SaleTenderRequest(accountId, MoneyCurrency.LBP, 900000, 10)]
        };
        using var postedResponse = await staff.PostAsJsonAsync($"/api/customers/{created.Id}/payments", payment);
        Assert.Equal(HttpStatusCode.Created, postedResponse.StatusCode);
        var responseText = await postedResponse.Content.ReadAsStringAsync();
        foreach (var secret in new[] { "cost", "supplier", "unitPrice" }) Assert.DoesNotContain(secret, responseText, StringComparison.OrdinalIgnoreCase);
        var posted = await postedResponse.Content.ReadFromJsonAsync<CustomerPaymentDto>();
        Assert.Equal(10, posted!.BalanceAfterUsd);
        using var statement = await staff.GetAsync($"/api/customers/{created.Id}/statement");
        Assert.Equal(HttpStatusCode.OK, statement.StatusCode);
        Assert.DoesNotContain("supplier", await statement.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var status = await staff.GetAsync($"/api/customers/operations/payments/{payment.OperationId}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var adminStatus = await admin.GetAsync($"/api/customers/operations/payments/{payment.OperationId}");
        Assert.Equal(HttpStatusCode.NotFound, adminStatus.StatusCode);
        using var anonymousStatus = await anonymous.GetAsync($"/api/customers/operations/payments/{payment.OperationId}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus.StatusCode);
        var correction = new CorrectCustomerPaymentRequest(Guid.NewGuid(), "Wrong account", 20);
        using var corrected = await staff.PostAsJsonAsync($"/api/customers/payments/{posted.Id}/corrections", correction);
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        var openingCorrection = new CorrectCustomerOpeningRequest(Guid.NewGuid(), 20, 5, 5, "Old book typo");
        using var staffCorrectOpening = await staff.PostAsJsonAsync(
            $"/api/customers/opening-balances/{postedOpening.Id}/corrections", openingCorrection);
        Assert.Equal(HttpStatusCode.Forbidden, staffCorrectOpening.StatusCode);
        using var adminCorrectOpening = await admin.PostAsJsonAsync(
            $"/api/customers/opening-balances/{postedOpening.Id}/corrections", openingCorrection);
        Assert.Equal(HttpStatusCode.OK, adminCorrectOpening.StatusCode);
        using var adminCorrectionStatus = await admin.GetAsync(
            $"/api/customers/operations/opening-corrections/{openingCorrection.OperationId}");
        Assert.Equal(HttpStatusCode.OK, adminCorrectionStatus.StatusCode);
        using var staffCorrectionStatus = await staff.GetAsync(
            $"/api/customers/operations/opening-corrections/{openingCorrection.OperationId}");
        Assert.Equal(HttpStatusCode.Forbidden, staffCorrectionStatus.StatusCode);
        using var adminOpeningStatus = await admin.GetAsync($"/api/customers/{created.Id}/opening-balance");
        Assert.Equal(5, (await adminOpeningStatus.Content.ReadFromJsonAsync<CustomerOpeningStatusDto>())!.CurrentOpeningBalanceUsd);
    }
}
