using System.Collections.Concurrent;
using System.Text.Json;
using FluentValidation;
using InventStock.Application.Common;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using InventStock.Application.Services;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;
using InventStock.Infrastructure.Data;
using InventStock.Infrastructure.Identity;
using InventStock.Infrastructure.Repositories;
using InventStock.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;

namespace InventStock.Tests.Infrastructure;

public sealed class PostgresSaleFixture : IAsyncLifetime
{
    private readonly string schema = "sale_test_" + Guid.NewGuid().ToString("N");
    private string connection = "";
    private bool created;
    public Guid Actor { get; } = Guid.NewGuid();
    public Guid OtherActor { get; } = Guid.NewGuid();
    public Guid AdminActor { get; } = Guid.NewGuid();
    public Guid CategoryId { get; private set; }
    public Dictionary<string, CashAccount> Accounts { get; private set; } = [];
    public AppDbContext Open(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connection, x => x.MigrationsHistoryTable("__EFMigrationsHistory", schema))
        .AddInterceptors(interceptors).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("INVENTSTOCK_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidOperationException("Real PostgreSQL is required: set INVENTSTOCK_TEST_POSTGRES to disposable inventstock_test.");
        var builder = new NpgsqlConnectionStringBuilder(configured);
        if (builder.Database != "inventstock_test") throw new InvalidOperationException("Only disposable inventstock_test is allowed.");
        await TestPostgresExtensions.EnsurePublicAsync(configured);
        builder.SearchPath = schema + ",public";
        builder.CommandTimeout = 15;
        builder.Timeout = 10;
        connection = builder.ConnectionString;
        await using var db = Open();
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{schema}\"");
#pragma warning restore EF1002
        created = true;
        await db.Database.MigrateAsync();
        var role = new ApplicationRole(RoleNames.Staff) { Id = Guid.NewGuid() };
        db.Roles.Add(role);
        var adminRole = new ApplicationRole(RoleNames.Admin) { Id = Guid.NewGuid() };
        db.Roles.Add(adminRole);
        foreach (var id in new[] { Actor, OtherActor })
        {
            db.Users.Add(new ApplicationUser { Id = id, UserName = id.ToString(), NormalizedUserName = id.ToString(), FullName = "Counter staff", IsActive = true });
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = id, RoleId = role.Id });
        }
        db.Users.Add(new ApplicationUser { Id = AdminActor, UserName = AdminActor.ToString(), NormalizedUserName = AdminActor.ToString(), FullName = "Admin", IsActive = true });
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = AdminActor, RoleId = adminRole.Id });
        var category = Category.Create("Sale tests", null);
        db.Categories.Add(category);
        CategoryId = category.Id;
        var accounts = new[]
        {
            CashAccount.Create("USD", "USD safe", MoneyCurrency.USD, CashAccountKind.PhysicalSafe),
            CashAccount.Create("LBP", "LBP safe", MoneyCurrency.LBP, CashAccountKind.PhysicalSafe),
            CashAccount.Create("EUR", "EUR safe", MoneyCurrency.EUR, CashAccountKind.PhysicalSafe),
            CashAccount.Create("WHISH", "Whish USD", MoneyCurrency.USD, CashAccountKind.Whish)
        };
        db.CashAccounts.AddRange(accounts);
        Accounts = accounts.ToDictionary(x => x.Code);
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        if (!created) return;
        await using var db = Open();
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"DROP SCHEMA \"{schema}\" CASCADE");
#pragma warning restore EF1002
    }
    public SaleService Service(AppDbContext db, IStockNotifier? notifier = null, Guid? actor = null)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(actor ?? Actor);
        user.FullName.Returns("Counter staff");
        return new(db, user, notifier ?? Substitute.For<IStockNotifier>(), NullLogger<SaleService>.Instance);
    }
    public SaleReturnService ReturnService(AppDbContext db, Guid? actor = null)
    {
        var current = Substitute.For<ICurrentUserService>();
        current.UserId.Returns(actor ?? Actor);
        current.FullName.Returns("Counter staff");
        return new(db, current);
    }
    public CustomerService CustomerService(AppDbContext db, Guid? actor = null)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(actor ?? Actor);
        user.FullName.Returns("Counter staff");
        return new(db, user);
    }
    public async Task<Part> Part(int quantity, bool counted = true, int held = 0)
    {
        await using var db = Open();
        var part = InventStock.Domain.Entities.Part.Create("SALE-" + Guid.NewGuid().ToString("N"), "Hose", null, CategoryId, null, 0, 0, null);
        db.Parts.Add(part);
        db.StockMovements.Add(counted ? StockMovement.PhysicalCount(part.Id, quantity, 0, Actor, "Opening count") :
            StockMovement.StockIn(part.Id, quantity, Actor, "Receipt", null));
        if (held > 0) db.StockHoldMovements.Add(StockHoldMovement.Create(part.Id, StockHoldAction.Hold, held, "Faulty", true, Actor, Guid.NewGuid(), "test"));
        await db.SaveChangesAsync();
        return part;
    }
    public async Task<Customer> Customer(decimal opening = 0)
    {
        await using var db = Open();
        var customer = InventStock.Domain.Entities.Customer.Create("Ahmad", Guid.NewGuid().ToString());
        db.Customers.Add(customer);
        if (opening != 0) db.CustomerAccountEntries.Add(CustomerAccountEntry.Opening(customer.Id, Guid.NewGuid(), Actor, opening));
        await db.SaveChangesAsync();
        return customer;
    }
    public PostSaleRequest CashSale(Part part, int quantity = 1, decimal price = 10) => new()
    {
        OperationId = Guid.NewGuid(), Lines = [new(part.Id, quantity, price)],
        ConfirmedTotalUsd = decimal.Round(quantity * price, 2, MidpointRounding.AwayFromZero),
        Payments = [new(Accounts["USD"].Id, MoneyCurrency.USD, decimal.Round(quantity * price, 2, MidpointRounding.AwayFromZero), decimal.Round(quantity * price, 2, MidpointRounding.AwayFromZero))]
    };
}

[Collection("PostgreSQL schema migrations")]
public sealed class PostgresSaleTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    private async Task<SaleDto> Post(PostSaleRequest request, Guid? actor = null, IStockNotifier? notifier = null, params IInterceptor[] interceptors)
    {
        await using var db = fixture.Open(interceptors);
        return await fixture.Service(db, notifier, actor).PostAsync(request);
    }

    [Fact]
    public async Task Competing_last_unit_sales_have_exactly_one_winner()
    {
        var part = await fixture.Part(1);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await Post(fixture.CashSale(part)); return true; }
            catch (InsufficientStockException) { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        await using var db = fixture.Open();
        Assert.Equal(0, await db.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(1, await db.SaleLines.CountAsync(x => x.PartId == part.Id));
    }

    [Fact]
    public async Task Concurrent_duplicates_replay_one_outcome_and_changed_payload_or_actor_conflicts()
    {
        var part = await fixture.Part(10);
        var request = fixture.CashSale(part);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Post(request)));
        Assert.Single(results.Select(x => x.Id).Distinct());
        Assert.Single(results.Select(x => JsonSerializer.Serialize(x)).Distinct());
        await Assert.ThrowsAsync<ConflictException>(() => Post(request with { Lines = [new(part.Id, 1, 11)], ConfirmedTotalUsd = 11 }));
        await Assert.ThrowsAsync<ConflictException>(() => Post(request, fixture.OtherActor));
        await using var db = fixture.Open();
        Assert.Equal(9, await db.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(1, await db.CashMovements.CountAsync(x => db.SaleTenders.Where(t => t.SaleId == results[0].Id).Select(t => t.Id).Contains(x.SaleTenderId!.Value)));
        var recovered = await fixture.Service(db).GetOperationAsync(request.OperationId);
        Assert.Equal(JsonSerializer.Serialize(results[0]), JsonSerializer.Serialize(recovered.Sale));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Service(db, actor: fixture.OtherActor).GetOperationAsync(request.OperationId));
    }

    [Fact]
    public async Task Mixed_currency_change_partial_debt_and_unknown_cost_are_exact()
    {
        var part = await fixture.Part(2);
        var customer = await fixture.Customer();
        var request = fixture.CashSale(part, price: 30) with
        {
            CustomerId = customer.Id, UnpaidDebtUsd = 5,
            Payments = [new(fixture.Accounts["LBP"].Id, MoneyCurrency.LBP, 900000, 10),
                new(fixture.Accounts["EUR"].Id, MoneyCurrency.EUR, 10, 12),
                new(fixture.Accounts["WHISH"].Id, MoneyCurrency.USD, 5, 5)],
            Change = [new(fixture.Accounts["USD"].Id, MoneyCurrency.USD, 2, 2)]
        };
        var result = await Post(request);
        Assert.Equal(5, result.UnpaidDebtUsd);
        await using var db = fixture.Open();
        Assert.Equal(5, await db.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
        var tenders = await db.SaleTenders.Where(x => x.SaleId == result.Id).ToDictionaryAsync(x => x.Id);
        var cash = await db.CashMovements.Where(x => x.SaleTenderId != null && tenders.Keys.Contains(x.SaleTenderId.Value)).ToListAsync();
        Assert.Equal(900000, cash.Single(x => x.CashAccountId == fixture.Accounts["LBP"].Id).AmountChange);
        Assert.Equal(-2, cash.Single(x => x.CashAccountId == fixture.Accounts["USD"].Id).AmountChange);
        Assert.Equal(5, cash.Single(x => x.CashAccountId == fixture.Accounts["WHISH"].Id).AmountChange);
        Assert.Null((await db.SaleLines.SingleAsync(x => x.SaleId == result.Id)).EstimatedBaseUnitCost);
    }

    [Fact]
    public async Task Customer_credit_cannot_be_double_spent_on_different_parts()
    {
        var a = await fixture.Part(1);
        var b = await fixture.Part(1);
        var customer = await fixture.Customer(-10);
        var results = await Task.WhenAll(new[] { a, b }.Select(async part =>
        {
            try
            {
                await Post(fixture.CashSale(part) with { CustomerId = customer.Id, Payments = [], CustomerCreditUsedUsd = 10 });
                return true;
            }
            catch (ConflictException) { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        await using var db = fixture.Open();
        Assert.Equal(0, await db.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
    }

    [Fact]
    public async Task Opposite_cart_order_and_repeated_lines_do_not_oversell_or_deadlock()
    {
        var a = await fixture.Part(10);
        var b = await fixture.Part(10);
        var first = fixture.CashSale(a) with { Lines = [new(a.Id, 1, 2), new(b.Id, 1, 5), new(a.Id, 1, 3)] };
        var second = first with { OperationId = Guid.NewGuid(), Lines = first.Lines.Reverse().ToArray() };
        await Task.WhenAll(Post(first), Post(second)).WaitAsync(TimeSpan.FromSeconds(20));
        await using var db = fixture.Open();
        Assert.Equal(6, await db.StockMovements.Where(x => x.PartId == a.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(8, await db.StockMovements.Where(x => x.PartId == b.Id).SumAsync(x => x.QuantityChange));
        var insufficient = await fixture.Part(1);
        await Assert.ThrowsAsync<InsufficientStockException>(() => Post(fixture.CashSale(insufficient) with
        { Lines = [new(insufficient.Id, 1, 5), new(insufficient.Id, 1, 5)] }));
    }

    private sealed class FailAfterSave(Guid operationId) : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default)
        {
            if (data.Context!.ChangeTracker.Entries<Sale>().Any(x => x.Entity.OperationId == operationId))
            { Triggered = true; throw new InvalidOperationException("Injected failure after SQL writes and before commit"); }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Failure_after_all_SQL_writes_rolls_back_every_ledger_and_emits_no_event()
    {
        var part = await fixture.Part(3);
        var customer = await fixture.Customer();
        var request = fixture.CashSale(part) with { CustomerId = customer.Id };
        var failure = new FailAfterSave(request.OperationId);
        var notifier = Substitute.For<IStockNotifier>();
        await using var db = fixture.Open();
        var cashBefore = await db.CashMovements.CountAsync();
        var tendersBefore = await db.SaleTenders.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Post(request, notifier: notifier, interceptors: [failure]));
        Assert.True(failure.Triggered);
        await notifier.DidNotReceiveWithAnyArgs().StockChangedAsync(default!, default);
        Assert.Equal(cashBefore, await db.CashMovements.CountAsync());
        Assert.Equal(tendersBefore, await db.SaleTenders.CountAsync());
        Assert.False(await db.Sales.AnyAsync(x => x.OperationId == request.OperationId));
        Assert.False(await db.SaleLines.AnyAsync(x => x.PartId == part.Id));
        Assert.False(await db.CustomerAccountEntries.AnyAsync(x => x.OperationId == request.OperationId));
        Assert.Equal(3, await db.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        var recovered = await Post(request);
        Assert.Single(recovered.Lines);
    }

    [Fact]
    public async Task Quarantine_is_not_bypassed_and_uncounted_sale_keeps_unknown_confidence()
    {
        var held = await fixture.Part(3, held: 3);
        await Assert.ThrowsAsync<InsufficientStockException>(() => Post(fixture.CashSale(held) with
        { Lines = [new(held.Id, 1, 10, true, "Holding goods")] }));
        var uncounted = await fixture.Part(3, counted: false);
        await Assert.ThrowsAsync<ConflictException>(() => Post(fixture.CashSale(uncounted)));
        var notifier = Substitute.For<IStockNotifier>();
        var sale = await Post(fixture.CashSale(uncounted) with { Lines = [new(uncounted.Id, 1, 10, true, "Good item physically in hand")] }, notifier: notifier);
        Assert.True(sale.Lines[0].StockException);
        await notifier.Received().StockChangedAsync(Arg.Is<StockChangedNotification>(x => x.StockConfidence == StockConfidence.NotCounted), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Physical_shortage_sale_leaves_negative_book_balance_and_requires_recount()
    {
        var part = await fixture.Part(0);
        var ordinary = fixture.CashSale(part);
        await Assert.ThrowsAsync<InsufficientStockException>(() => Post(ordinary));
        var request = ordinary with { OperationId = Guid.NewGuid(), Lines = [new(part.Id, 1, 10, true, "Found one sellable hose on shelf; full count pending")] };
        var posted = await Post(request);
        Assert.True(posted.Lines.Single().StockException);
        Assert.True(posted.Lines.Single().StockShortage);
        Assert.Equal(JsonSerializer.Serialize(posted), JsonSerializer.Serialize(await Post(request)));
        await using (var db = fixture.Open())
        {
            Assert.Equal(-1, await db.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
            Assert.False(await db.StockMovements.Where(x => x.PartId == part.Id && x.Reference == posted.Id.ToString()).AnyAsync(x => x.QuantityChange > 0));
            Assert.True((await db.Parts.SingleAsync(x => x.Id == part.Id)).NeedsRecount);
            Assert.Equal(StockConfidence.NeedsRecount, (await new PartRepository(db).GetCounterAsync(part.Id))!.StockConfidence);
        }
        await Assert.ThrowsAsync<InsufficientStockException>(() => Post(request with { OperationId = Guid.NewGuid() }));
        await using (var countDb = fixture.Open())
        {
            var user = Substitute.For<ICurrentUserService>();
            user.UserId.Returns(fixture.Actor);
            user.FullName.Returns("Counter staff");
            var counter = new StockMovementService(new PartRepository(countDb), new StockMovementRepository(countDb),
                new UnitOfWork(countDb), user, Substitute.For<IStockNotifier>(), NullLogger<StockMovementService>.Instance);
            await counter.CountAsync(part.Id, new StockCountRequest { OperationId = Guid.NewGuid(), ExpectedBookQuantity = -1,
                CountedQuantity = 0, Reason = "Recount after sale exception" });
        }
        await using var checkedDb = fixture.Open();
        Assert.False((await checkedDb.Parts.SingleAsync(x => x.Id == part.Id)).NeedsRecount);
        Assert.Equal(StockConfidence.Counted, (await new PartRepository(checkedDb).GetCounterAsync(part.Id))!.StockConfidence);
        Assert.Equal(0, await checkedDb.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
    }

    [Fact]
    public async Task Simultaneous_shortage_claims_have_one_winner_and_hold_is_never_sold()
    {
        var part = await fixture.Part(0);
        var claims = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            var request = fixture.CashSale(part) with { Lines = [new(part.Id, 1, 10, true, "Physically found sellable item")] };
            try { await Post(request); return true; }
            catch (InsufficientStockException) { return false; }
        }));
        Assert.Equal(1, claims.Count(x => x));
        var held = await fixture.Part(3, held: 3);
        await Assert.ThrowsAsync<InsufficientStockException>(() => Post(fixture.CashSale(held) with
            { Lines = [new(held.Id, 1, 10, true, "Claimed good item, but held stock exists")] }));
        await using var db = fixture.Open();
        Assert.Equal(-1, await db.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(3, await db.StockHoldMovements.Where(x => x.PartId == held.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(3, await db.StockMovements.Where(x => x.PartId == held.Id).SumAsync(x => x.QuantityChange));
    }

    [Fact]
    public async Task Failed_shortage_sale_does_not_leave_a_recount_marker()
    {
        var part = await fixture.Part(0);
        var request = fixture.CashSale(part) with { Lines = [new(part.Id, 1, 10, true, "Physically found sellable item")] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Post(request, interceptors: [new FailAfterSave(request.OperationId)]));
        await using var db = fixture.Open();
        Assert.False((await db.Parts.SingleAsync(x => x.Id == part.Id)).NeedsRecount);
        Assert.Equal(0, await db.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.False(await db.Sales.AnyAsync(x => x.OperationId == request.OperationId));
    }

    [Fact]
    public async Task Rounding_snapshots_original_line_total_and_validation_rejects_unbalanced_or_anonymous_debt()
    {
        var part = await fixture.Part(10);
        var sale = await Post(fixture.CashSale(part, 3, 0.335m));
        Assert.Equal(1.01m, sale.TotalUsd);
        Assert.Equal(1.01m, sale.Lines.Single().LineTotalUsd);
        await Assert.ThrowsAsync<ValidationException>(() => Post(fixture.CashSale(part) with { UnpaidDebtUsd = 1 }));
        await Assert.ThrowsAsync<ConflictException>(() => Post(fixture.CashSale(part) with { ConfirmedTotalUsd = 9 }));
        await Assert.ThrowsAsync<ValidationException>(() => Post(fixture.CashSale(part) with
        { Payments = [new(fixture.Accounts["USD"].Id, MoneyCurrency.USD, 9, 10)] }));
    }

    [Fact]
    public async Task Responses_and_committed_events_are_public_and_notification_failure_does_not_undo_sale()
    {
        var part = await fixture.Part(2);
        await using (var setup = fixture.Open())
        {
            var supplier = Supplier.Create("PRIVATE supplier", null, null, null, null);
            setup.Suppliers.Add(supplier);
            setup.SupplierCostObservations.Add(SupplierCostObservation.Create(part.Id, supplier.Id, 4.25m,
                MoneyCurrency.EUR, TaxTreatment.Unknown, CostSource.ActualPurchase, 1, "PRIVATE code", null, DateTime.UtcNow));
            await setup.SaveChangesAsync();
        }
        var notifier = Substitute.For<IStockNotifier>();
        var observed = new ConcurrentBag<StockChangedNotification>();
        notifier.StockChangedAsync(Arg.Any<StockChangedNotification>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await using var observer = fixture.Open();
            Assert.True(await observer.SaleLines.AnyAsync(x => x.PartId == part.Id)); // Independent connection sees committed data.
            observed.Add(call.Arg<StockChangedNotification>());
            throw new IOException("Simulated notification transport failure");
        });
        var sale = await Post(fixture.CashSale(part), notifier: notifier);
        var publicJson = JsonSerializer.Serialize(sale) + JsonSerializer.Serialize(observed.Single());
        foreach (var secret in new[] { "Cost", "Supplier", "4.25", "PRIVATE" }) Assert.DoesNotContain(secret, publicJson, StringComparison.OrdinalIgnoreCase);
        await using var db = fixture.Open();
        Assert.Equal(4.25m, (await db.SaleLines.SingleAsync(x => x.SaleId == sale.Id)).EstimatedBaseUnitCost);
    }

    [Fact]
    public async Task Disabled_user_and_removed_role_cannot_replay_or_recover_a_sale()
    {
        var part = await fixture.Part(2);
        var request = fixture.CashSale(part);
        await Post(request, fixture.OtherActor);
        await using var db = fixture.Open();
        var user = await db.Users.SingleAsync(x => x.Id == fixture.OtherActor);
        user.IsActive = false;
        await db.SaveChangesAsync();
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Post(request, fixture.OtherActor));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service(db, actor: fixture.OtherActor).GetOperationAsync(request.OperationId));
        }
        finally { user.IsActive = true; await db.SaveChangesAsync(); }
        var memberships = await db.UserRoles.Where(x => x.UserId == fixture.OtherActor).ToListAsync();
        db.UserRoles.RemoveRange(memberships);
        await db.SaveChangesAsync();
        try { await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Post(request, fixture.OtherActor)); }
        finally { db.UserRoles.AddRange(memberships); await db.SaveChangesAsync(); }
    }

    [Fact]
    public async Task Operation_id_cannot_be_reused_by_raw_stock_or_cash_commands()
    {
        var part = await fixture.Part(2);
        var request = fixture.CashSale(part);
        await Post(request);
        await using var db = fixture.Open();
        await using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<ConflictException>(() => OperationGuard.LockAsync(db, request.OperationId, "Stock", default));
        await Assert.ThrowsAsync<ConflictException>(() => OperationGuard.LockAsync(db, request.OperationId, "Cash", default));
    }
}
