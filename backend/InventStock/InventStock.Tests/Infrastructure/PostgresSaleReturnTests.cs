using System.Text.Json;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Sales;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InventStock.Tests.Infrastructure;

[Collection("PostgreSQL schema migrations")]
public sealed class PostgresSaleReturnTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    private PostSaleReturnRequest Request(Guid saleId, Guid lineId, int quantity, decimal total,
        decimal accountReduction = 0, Guid? operation = null, ReturnCondition condition = ReturnCondition.Sellable) => new()
    {
        OperationId = operation ?? Guid.NewGuid(), SaleId = saleId, Reason = "Customer brought part back",
        ConfirmedTotalUsd = total, AccountReductionUsd = accountReduction,
        Lines = [new(lineId, quantity, condition, condition == ReturnCondition.Quarantined,
            condition == ReturnCondition.Quarantined ? "Shorter than specified; physically tagged" : null)],
        Refunds = total > accountReduction
            ? [new(fixture.Accounts["USD"].Id, MoneyCurrency.USD, total - accountReduction, total - accountReduction)] : []
    };

    [Fact]
    public async Task Partial_returns_refund_exact_posted_cents_and_reject_overreturn()
    {
        var part = await fixture.Part(3);
        SaleDto sale;
        await using (var db = fixture.Open()) sale = await fixture.Service(db).PostAsync(fixture.CashSale(part, 3, .335m));
        Assert.Equal(1.01m, sale.TotalUsd);
        decimal[] amounts = [.34m, .33m, .34m];
        for (var i = 0; i < amounts.Length; i++)
        {
            await using var db = fixture.Open();
            var request = Request(sale.Id, sale.Lines[0].Id, 1, amounts[i]);
            var result = await fixture.ReturnService(db).PostAsync(request);
            Assert.Equal(amounts[i], result.Lines[0].RefundUsd);
            Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(await fixture.ReturnService(db).PostAsync(request)));
            Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(await fixture.ReturnService(db).GetOperationAsync(request.OperationId)));
            await Assert.ThrowsAsync<ConflictException>(() => fixture.ReturnService(db).PostAsync(request with { Reason = "Changed" }));
            await Assert.ThrowsAsync<ConflictException>(() => fixture.ReturnService(db, fixture.OtherActor).PostAsync(request));
        }
        await using var check = fixture.Open();
        Assert.Equal(1.01m, await check.SaleReturnLines.Where(x => x.SaleLineId == sale.Lines[0].Id).SumAsync(x => x.RefundUsd));
        Assert.Equal(3, await check.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.ReturnService(check).PostAsync(Request(sale.Id, sale.Lines[0].Id, 1, .34m)));
    }

    [Fact]
    public async Task Named_customer_can_choose_whish_refund_while_debt_remains_or_reduce_debt()
    {
        var part = await fixture.Part(2);
        var customer = await fixture.Customer(30);
        SaleDto first;
        SaleDto second;
        await using (var db = fixture.Open())
        {
            first = await fixture.Service(db).PostAsync(fixture.CashSale(part) with
                { CustomerId = customer.Id, Payments = [], UnpaidDebtUsd = 10 });
            second = await fixture.Service(db).PostAsync(fixture.CashSale(part) with
                { CustomerId = customer.Id, Payments = [], UnpaidDebtUsd = 10 });
        }
        await using var context = fixture.Open();
        var cashRequest = Request(first.Id, first.Lines[0].Id, 1, 10) with
        { Refunds = [new(fixture.Accounts["WHISH"].Id, MoneyCurrency.USD, 10, 10)] };
        await fixture.ReturnService(context).PostAsync(cashRequest);
        Assert.Equal(50, await context.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
        Assert.Equal(-10, await context.CashMovements.Where(x => x.SaleRefundTenderId != null && x.CashAccountId == fixture.Accounts["WHISH"].Id).SumAsync(x => x.AmountChange));
        await fixture.ReturnService(context).PostAsync(Request(second.Id, second.Lines[0].Id, 1, 10, accountReduction: 10));
        Assert.Equal(40, await context.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
    }

    [Fact]
    public async Task Faulty_return_is_held_and_a_shortage_return_keeps_recount_warning()
    {
        var good = await fixture.Part(1);
        SaleDto sale;
        await using (var db = fixture.Open()) sale = await fixture.Service(db).PostAsync(fixture.CashSale(good));
        await using (var db = fixture.Open())
            await fixture.ReturnService(db).PostAsync(Request(sale.Id, sale.Lines[0].Id, 1, 10,
                condition: ReturnCondition.Quarantined));
        await using (var db = fixture.Open())
        {
            Assert.Equal(1, await db.StockMovements.Where(x => x.PartId == good.Id).SumAsync(x => x.QuantityChange));
            Assert.Equal(1, await db.StockHoldMovements.Where(x => x.PartId == good.Id).SumAsync(x => x.QuantityChange));
        }

        var disputed = await fixture.Part(0);
        SaleDto shortage;
        await using (var db = fixture.Open()) shortage = await fixture.Service(db).PostAsync(
            fixture.CashSale(disputed) with { Lines = [new(disputed.Id, 1, 10, true, "Sellable item physically held")] });
        await using (var db = fixture.Open())
            await fixture.ReturnService(db).PostAsync(Request(shortage.Id, shortage.Lines[0].Id, 1, 10));
        await using var check = fixture.Open();
        Assert.True((await check.Parts.AsNoTracking().SingleAsync(x => x.Id == disputed.Id)).NeedsRecount);
        Assert.Equal(0, await check.StockMovements.Where(x => x.PartId == disputed.Id).SumAsync(x => x.QuantityChange));
    }

    [Fact]
    public async Task Competing_last_unit_returns_have_one_winner()
    {
        var part = await fixture.Part(1);
        SaleDto sale;
        await using (var db = fixture.Open()) sale = await fixture.Service(db).PostAsync(fixture.CashSale(part));
        var requests = new[] { Request(sale.Id, sale.Lines[0].Id, 1, 10), Request(sale.Id, sale.Lines[0].Id, 1, 10) };
        var results = await Task.WhenAll(requests.Select(async request =>
        {
            try { await using var db = fixture.Open(); await fixture.ReturnService(db).PostAsync(request); return true; }
            catch (ConflictException) { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        await using var check = fixture.Open();
        Assert.Single(await check.SaleReturnLines.Where(x => x.SaleLineId == sale.Lines[0].Id).ToListAsync());
    }

    private sealed class FailAfterReturnSave : SaveChangesInterceptor
    {
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (data.Context is AppDbContext db && db.ChangeTracker.Entries().Any(x => x.Entity is SaleReturn))
                throw new InvalidOperationException("Simulated failure after SQL writes before commit");
            return await base.SavedChangesAsync(data, result, ct);
        }
    }

    [Fact]
    public async Task Failure_after_sql_writes_rolls_back_return_stock_and_cash()
    {
        var part = await fixture.Part(1);
        SaleDto sale;
        await using (var db = fixture.Open()) sale = await fixture.Service(db).PostAsync(fixture.CashSale(part));
        var request = Request(sale.Id, sale.Lines[0].Id, 1, 10, condition: ReturnCondition.Quarantined);
        decimal cashBefore;
        await using (var before = fixture.Open())
            cashBefore = await before.CashMovements.Where(x => x.CashAccountId == fixture.Accounts["USD"].Id)
                .SumAsync(x => x.AmountChange);
        await using (var failing = fixture.Open(new FailAfterReturnSave()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ReturnService(failing).PostAsync(request));
        await using var check = fixture.Open();
        Assert.False(await check.SaleReturns.AnyAsync(x => x.OperationId == request.OperationId));
        Assert.Equal(0, await check.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(0, await check.StockHoldMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(cashBefore, await check.CashMovements.Where(x => x.CashAccountId == fixture.Accounts["USD"].Id)
            .SumAsync(x => x.AmountChange));
    }
}
