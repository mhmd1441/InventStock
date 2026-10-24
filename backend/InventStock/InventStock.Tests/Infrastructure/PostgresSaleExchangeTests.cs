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
public sealed class PostgresSaleExchangeTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    private PostSaleExchangeRequest Request(SaleDto original, Part replacement, Guid? operation = null) => new()
    {
        OperationId = operation ?? Guid.NewGuid(), OriginalSaleId = original.Id,
        Reason = "Customer exchanged for another part", ConfirmedReturnTotalUsd = original.Lines[0].LineTotalUsd,
        ReturnAppliedUsd = original.Lines[0].LineTotalUsd,
        ReturnLines = [new(original.Lines[0].Id, 1, ReturnCondition.Sellable)],
        ConfirmedReplacementTotalUsd = original.Lines[0].LineTotalUsd,
        ReplacementLines = [new(replacement.Id, 1, original.Lines[0].LineTotalUsd)]
    };

    [Fact]
    public async Task Exchange_posts_return_replacement_and_net_settlement_once()
    {
        var returned = await fixture.Part(1);
        var replacement = await fixture.Part(1);
        var customer = await fixture.Customer();
        SaleDto original;
        await using (var db = fixture.Open())
            original = await fixture.Service(db).PostAsync(fixture.CashSale(returned) with { CustomerId = customer.Id });

        var request = Request(original, replacement) with
        {
            ReturnAppliedUsd = 8, AccountReductionUsd = 2,
            ConfirmedReplacementTotalUsd = 15,
            ReplacementLines = [new(replacement.Id, 1, 15)],
            Payments = [new(fixture.Accounts["USD"].Id, MoneyCurrency.USD, 7, 7)]
        };
        SaleExchangeDto result;
        await using (var db = fixture.Open())
        {
            result = await fixture.ExchangeService(db).PostAsync(request);
            Assert.Equal(JsonSerializer.Serialize(result),
                JsonSerializer.Serialize(await fixture.ExchangeService(db).PostAsync(request)));
            Assert.Equal(JsonSerializer.Serialize(result),
                JsonSerializer.Serialize(await fixture.ExchangeService(db).GetOperationAsync(request.OperationId)));
            await Assert.ThrowsAsync<ConflictException>(() => fixture.ExchangeService(db, fixture.OtherActor).PostAsync(request));
            await Assert.ThrowsAsync<ConflictException>(() => fixture.ExchangeService(db).PostAsync(request with { Reason = "Changed" }));
        }

        await using var check = fixture.Open();
        Assert.Equal(1, await check.SaleExchanges.CountAsync(x => x.OperationId == request.OperationId));
        Assert.Equal(1, await check.StockMovements.Where(x => x.PartId == returned.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(0, await check.StockMovements.Where(x => x.PartId == replacement.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(-2, await check.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
    }

    [Fact]
    public async Task Exchange_supports_explicit_refund_and_quarantine_without_releasing_held_goods()
    {
        var returned = await fixture.Part(1);
        var replacement = await fixture.Part(1);
        SaleDto original;
        await using (var db = fixture.Open())
            original = await fixture.Service(db).PostAsync(fixture.CashSale(returned));
        var request = Request(original, replacement) with
        {
            ReturnAppliedUsd = 5,
            Refunds = [new(fixture.Accounts["WHISH"].Id, MoneyCurrency.USD, 5, 5)],
            ReturnLines = [new(original.Lines[0].Id, 1, ReturnCondition.Quarantined, true, "Broken; physically tagged")],
            ConfirmedReplacementTotalUsd = 5,
            ReplacementLines = [new(replacement.Id, 1, 5)]
        };
        await using (var db = fixture.Open()) await fixture.ExchangeService(db).PostAsync(request);
        await using var check = fixture.Open();
        Assert.Equal(1, await check.StockHoldMovements.Where(x => x.PartId == returned.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(-5, await check.CashMovements.Where(x => x.CashAccountId == fixture.Accounts["WHISH"].Id && x.SaleRefundTenderId != null).SumAsync(x => x.AmountChange));
    }

    private sealed class FailAfterExchangeSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result,
            CancellationToken ct = default) => data.Context is AppDbContext db &&
            db.ChangeTracker.Entries().Any(x => x.Entity is SaleExchange)
                ? ValueTask.FromException<int>(new InvalidOperationException("Simulated failure before commit"))
                : ValueTask.FromResult(result);
    }

    [Fact]
    public async Task Failure_after_sql_writes_rolls_back_both_exchange_legs()
    {
        var returned = await fixture.Part(1);
        var replacement = await fixture.Part(1);
        SaleDto original;
        await using (var db = fixture.Open()) original = await fixture.Service(db).PostAsync(fixture.CashSale(returned));
        var request = Request(original, replacement);
        await using (var db = fixture.Open(new FailAfterExchangeSave()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExchangeService(db).PostAsync(request));
        await using var check = fixture.Open();
        Assert.False(await check.SaleExchanges.AnyAsync(x => x.OperationId == request.OperationId));
        Assert.False(await check.Sales.AnyAsync(x => x.OperationId != original.OperationId && x.Lines.Any(l => l.PartId == replacement.Id)));
        Assert.False(await check.SaleReturns.AnyAsync(x => x.SaleId == original.Id));
        // The replacement fixture starts with one physical-count unit. A failed
        // exchange must preserve that opening quantity rather than deducting it.
        Assert.Equal(1, await check.StockMovements.Where(x => x.PartId == replacement.Id).SumAsync(x => x.QuantityChange));
    }
}
