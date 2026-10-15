using System.Text.Json;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Customers;
using InventStock.Application.DTOs.Sales;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InventStock.Tests.Infrastructure;

[Collection("PostgreSQL schema migrations")]
public sealed class PostgresCustomerTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    private PostCustomerPaymentRequest Payment(Guid? operation = null, decimal usd = 10, decimal excess = 0) => new()
    {
        OperationId = operation ?? Guid.NewGuid(), ConfirmedUsdCredit = usd, ConfirmedExcessCreditUsd = excess,
        Payments = [new(fixture.Accounts["USD"].Id, MoneyCurrency.USD, usd, usd)]
    };

    [Fact]
    public async Task Profiles_are_audited_revisioned_and_allow_distinct_name_twins()
    {
        await using var db = fixture.Open();
        var service = fixture.CustomerService(db);
        var name = "Ahmad-" + Guid.NewGuid().ToString("N");
        var request = new SaveCustomerRequest(Guid.NewGuid(), name, "Garage one");
        var first = await service.CreateAsync(request);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(await service.CreateAsync(request)));
        var second = await service.CreateAsync(request with { OperationId = Guid.NewGuid(), DistinguishingLabel = "Garage two" });
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, (await service.SearchAsync(new CustomerQuery { Search = name })).TotalCount);
        var changed = await service.UpdateAsync(first.Id, new(Guid.NewGuid(), name, "Garage one", "+96170000000", "Beirut", first.Revision));
        Assert.Equal(first.Revision + 1, changed.Revision);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(first.Id, new(Guid.NewGuid(), name, ExpectedRevision: first.Revision)));
        Assert.Equal(2, await db.CustomerProfileEvents.CountAsync(x => x.CustomerId == first.Id));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.CustomerService(db, fixture.OtherActor).CreateAsync(request));
    }

    [Fact]
    public async Task Opening_is_admin_only_precedes_transactions_and_keeps_historical_as_of_date()
    {
        var customer = await fixture.Customer();
        var asOf = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = new PostCustomerOpeningRequest(Guid.NewGuid(), 500, asOf, "Verified old book");
        await using var db = fixture.Open();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.CustomerService(db).OpenAccountAsync(customer.Id, request));
        var service = fixture.CustomerService(db, fixture.AdminActor);
        var posted = await service.OpenAccountAsync(customer.Id, request);
        Assert.Equal(JsonSerializer.Serialize(posted), JsonSerializer.Serialize(await service.OpenAccountAsync(customer.Id, request)));
        Assert.Equal(500, (await service.StatementAsync(customer.Id, new())).Summary.BalanceUsd);
        Assert.Equal(asOf, posted.AsOfUtc);
        await Assert.ThrowsAsync<ConflictException>(() => service.OpenAccountAsync(customer.Id, request with { OperationId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Opening_correction_preserves_later_transactions_and_is_actor_scoped()
    {
        var customer = await fixture.Customer();
        var original = new PostCustomerOpeningRequest(Guid.NewGuid(), 500,
            new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Old book review");
        await using var db = fixture.Open();
        var admin = fixture.CustomerService(db, fixture.AdminActor);
        var opening = await admin.OpenAccountAsync(customer.Id, original);
        var part = await fixture.Part(2);
        await using (var saleDb = fixture.Open())
            await fixture.Service(saleDb).PostAsync(fixture.CashSale(part) with
                { CustomerId = customer.Id, Payments = [], UnpaidDebtUsd = 10 });
        Assert.Equal(510, (await admin.StatementAsync(customer.Id, new())).Summary.BalanceUsd);
        Assert.Equal(500, (await admin.OpeningStatusAsync(customer.Id))!.CurrentOpeningBalanceUsd);
        var request = new CorrectCustomerOpeningRequest(Guid.NewGuid(), 500, 50, 60, "Opening entered with extra zero");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.CustomerService(db).CorrectOpeningAsync(opening.Id, request));
        var corrected = await admin.CorrectOpeningAsync(opening.Id, request);
        Assert.Equal(-450, corrected.DeltaUsd);
        Assert.Equal(60, corrected.BalanceAfterUsd);
        Assert.Equal(opening.Id, corrected.OriginalOpeningEntryId);
        Assert.Equal(JsonSerializer.Serialize(corrected), JsonSerializer.Serialize(await admin.CorrectOpeningAsync(opening.Id, request)));
        Assert.Equal(JsonSerializer.Serialize(corrected), JsonSerializer.Serialize(await admin.GetOpeningCorrectionOperationAsync(request.OperationId)));
        await using (var reusedOperationDb = fixture.Open())
            await Assert.ThrowsAsync<ConflictException>(() => fixture.Service(reusedOperationDb).PostAsync(
                fixture.CashSale(part) with { OperationId = request.OperationId }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.CustomerService(db, fixture.OtherActor).CorrectOpeningAsync(opening.Id, request));
        await Assert.ThrowsAsync<ConflictException>(() => admin.CorrectOpeningAsync(opening.Id, request with { CorrectedOpeningBalanceUsd = 51 }));
        var statement = await admin.StatementAsync(customer.Id, new());
        Assert.Equal(60, statement.Summary.BalanceUsd);
        Assert.Contains(statement.Entries.Items, x => x.Kind == "SaleCharge" && x.AmountUsd == 10);
        Assert.Contains(statement.Entries.Items, x => x.Kind == "Opening" && x.AmountUsd == 500);
        Assert.Contains(statement.Entries.Items, x => x.Kind == "OpeningCorrection" && x.AmountUsd == -450 &&
            x.OriginalOpeningEntryId == opening.Id && x.CorrectedOpeningBalanceUsd == 50);
        Assert.Equal(50, (await admin.OpeningStatusAsync(customer.Id))!.CurrentOpeningBalanceUsd);
        var second = await admin.CorrectOpeningAsync(opening.Id,
            new CorrectCustomerOpeningRequest(Guid.NewGuid(), 50, 40, 50, "Rechecked old book"));
        Assert.Equal(-10, second.DeltaUsd);
        Assert.Equal(50, (await admin.StatementAsync(customer.Id, new())).Summary.BalanceUsd);
    }

    [Fact]
    public async Task Concurrent_opening_corrections_cannot_both_use_a_stale_confirmed_balance()
    {
        var customer = await fixture.Customer();
        Guid openingId;
        await using (var setup = fixture.Open())
            openingId = (await fixture.CustomerService(setup, fixture.AdminActor).OpenAccountAsync(customer.Id,
                new PostCustomerOpeningRequest(Guid.NewGuid(), 100, DateTime.UtcNow.AddDays(-1), "Reviewed"))).Id;
        var requests = new[]
        {
            new CorrectCustomerOpeningRequest(Guid.NewGuid(), 100, 90, 90, "Count A"),
            new CorrectCustomerOpeningRequest(Guid.NewGuid(), 100, 80, 80, "Count B")
        };
        var results = await Task.WhenAll(requests.Select(async request =>
        {
            try
            {
                await using var context = fixture.Open();
                await fixture.CustomerService(context, fixture.AdminActor).CorrectOpeningAsync(openingId, request);
                return true;
            }
            catch (ConflictException) { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        await using var check = fixture.Open();
        Assert.Equal(1, await check.CustomerAccountEntries.CountAsync(x => x.OriginalOpeningEntryId == openingId));
    }

    [Fact]
    public async Task Confirmed_excess_becomes_credit_and_employee_can_correct_exact_native_movements()
    {
        var customer = await fixture.Customer(5);
        var request = new PostCustomerPaymentRequest
        {
            OperationId = Guid.NewGuid(), ConfirmedUsdCredit = 10, ConfirmedExcessCreditUsd = 5,
            Payments = [new(fixture.Accounts["LBP"].Id, MoneyCurrency.LBP, 900000, 10)]
        };
        await using var db = fixture.Open();
        var service = fixture.CustomerService(db);
        var paid = await service.PayAsync(customer.Id, request);
        Assert.Equal(-5, paid.BalanceAfterUsd);
        Assert.Equal(5, paid.ExcessCreditUsd);
        Assert.Equal(JsonSerializer.Serialize(paid), JsonSerializer.Serialize(await service.PayAsync(customer.Id, request)));
        Assert.Equal(JsonSerializer.Serialize(paid), JsonSerializer.Serialize(await service.GetPaymentOperationAsync(request.OperationId)));
        await Assert.ThrowsAsync<ConflictException>(() => service.PayAsync(customer.Id, request with { ConfirmedUsdCredit = 9 }));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.CustomerService(db, fixture.OtherActor).PayAsync(customer.Id, request));
        var correctionRequest = new CorrectCustomerPaymentRequest(Guid.NewGuid(), "Payment entered against wrong customer", 5);
        var corrected = await service.CorrectPaymentAsync(paid.Id, correctionRequest);
        Assert.Equal(5, corrected.BalanceAfterUsd);
        Assert.Equal(JsonSerializer.Serialize(corrected), JsonSerializer.Serialize(await service.CorrectPaymentAsync(paid.Id, correctionRequest)));
        await Assert.ThrowsAsync<ConflictException>(() => service.CorrectPaymentAsync(paid.Id, correctionRequest with { OperationId = Guid.NewGuid() }));
        Assert.Equal(5, (await service.StatementAsync(customer.Id, new())).Summary.BalanceUsd);
        var native = await db.CashMovements.Where(x => x.CustomerPaymentTenderId == paid.Tenders[0].Id).Select(x => x.AmountChange).ToListAsync();
        Assert.Equal(2, native.Count);
        Assert.Equal(0, native.Sum());
        Assert.Contains(900000, native);
        Assert.Contains(-900000, native);
    }

    [Fact]
    public async Task Concurrent_account_payments_and_sale_serialize_on_customer_without_double_spending_credit()
    {
        var customer = await fixture.Customer(10);
        var a = Payment();
        var b = Payment();
        var results = await Task.WhenAll(new[] { a, b }.Select(async request =>
        {
            try { await using var db = fixture.Open(); await fixture.CustomerService(db).PayAsync(customer.Id, request); return true; }
            catch (ConflictException) { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        await using var check = fixture.Open();
        Assert.Equal(0, await check.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
        Assert.Single(await check.CustomerPayments.Where(x => x.CustomerId == customer.Id).ToListAsync());

        // Credit posted explicitly can then be spent once by a sale, using the same customer lock.
        var requestCredit = Payment(usd: 10, excess: 10);
        await fixture.CustomerService(check).PayAsync(customer.Id, requestCredit);
        var part = await fixture.Part(1);
        await using var saleDb = fixture.Open();
        await fixture.Service(saleDb).PostAsync(fixture.CashSale(part) with
            { CustomerId = customer.Id, Payments = [], CustomerCreditUsedUsd = 10 });
        Assert.Equal(0, await check.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
    }

    private sealed class FailAfterCustomerSave : SaveChangesInterceptor
    {
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (eventData.Context is AppDbContext db && db.ChangeTracker.Entries().Any(x => x.Entity is InventStock.Domain.Entities.CustomerPayment))
                throw new InvalidOperationException("Simulated failure after INSERTs and before COMMIT");
            return await base.SavedChangesAsync(eventData, result, ct);
        }
    }

    [Fact]
    public async Task Failure_after_sql_writes_rolls_back_customer_and_cash_together()
    {
        var customer = await fixture.Customer(10);
        var request = Payment();
        await using (var failing = fixture.Open(new FailAfterCustomerSave()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CustomerService(failing).PayAsync(customer.Id, request));
        await using var db = fixture.Open();
        Assert.Empty(await db.CustomerPayments.Where(x => x.OperationId == request.OperationId).ToListAsync());
        Assert.Empty(await db.CashMovements.Where(x => db.CustomerPaymentTenders.Where(t =>
                db.CustomerPayments.Where(p => p.OperationId == request.OperationId).Select(p => p.Id).Contains(t.CustomerPaymentId))
            .Select(t => t.Id).Contains(x.CustomerPaymentTenderId!.Value)).ToListAsync());
        Assert.Equal(10, await db.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
    }
}
