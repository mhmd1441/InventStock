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
public sealed class PostgresLegacySaleReturnTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    private PostLegacySaleReturnRequest Request(Part part, Guid? customerId = null, Guid? operationId = null,
        ReturnCondition condition = ReturnCondition.Sellable) => new()
    {
        OperationId = operationId ?? Guid.NewGuid(),
        CustomerId = customerId,
        OriginalSaleDate = new DateOnly(2025, 7, 12),
        OriginalReference = "Paper book page 41",
        Reason = "Sold before InventStock; customer returned the part",
        ConfirmedTotalUsd = 10,
        Lines = [new(part.Id, 1, 10, condition, condition == ReturnCondition.Quarantined,
            condition == ReturnCondition.Quarantined ? "Wrong length; tagged and separated" : null)],
        Refunds = [new(fixture.Accounts["USD"].Id, MoneyCurrency.USD, 10, 10)]
    };

    [Fact]
    public async Task Staff_can_post_audited_legacy_return_with_native_refund_and_account_reduction()
    {
        var part = await fixture.Part(0);
        var customer = await fixture.Customer(20);
        var request = Request(part, customer.Id, condition: ReturnCondition.Quarantined) with
        {
            ConfirmedTotalUsd = 10,
            AccountReductionUsd = 4,
            Refunds = [new(fixture.Accounts["LBP"].Id, MoneyCurrency.LBP, 540000, 6)]
        };

        LegacySaleReturnDto result;
        await using (var db = fixture.Open()) result = await fixture.ReturnService(db).PostLegacyAsync(request);

        Assert.Equal("Paper book page 41", result.OriginalReference);
        Assert.Equal(new DateOnly(2025, 7, 12), result.OriginalSaleDate);
        Assert.Equal(540000, result.Refunds[0].NativeAmount);
        Assert.Equal(6, result.Refunds[0].ConfirmedUsdAmount);
        Assert.DoesNotContain("supplier", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cost", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);

        await using var check = fixture.Open();
        Assert.Equal(1, await check.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(1, await check.StockHoldMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(-540000, await check.CashMovements
            .Where(x => result.Refunds.Select(r => r.Id).Contains(x.LegacySaleRefundTenderId!.Value))
            .SumAsync(x => x.AmountChange));
        Assert.Equal(16, await check.CustomerAccountEntries
            .Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
        Assert.Single(await check.CustomerAccountEntries
            .Where(x => x.LegacySaleReturnId == result.Id).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_duplicate_replays_one_outcome_and_changed_payload_or_actor_conflicts()
    {
        var part = await fixture.Part(0);
        var request = Request(part);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var db = fixture.Open();
            return await fixture.ReturnService(db).PostLegacyAsync(request);
        }));

        Assert.Single(outcomes.Select(x => x.Id).Distinct());
        Assert.Single(outcomes.Select(x => JsonSerializer.Serialize(x)).Distinct());
        await using (var changed = fixture.Open())
            await Assert.ThrowsAsync<ConflictException>(() => fixture.ReturnService(changed)
                .PostLegacyAsync(request with { Reason = "Different evidence" }));
        await using (var other = fixture.Open())
            await Assert.ThrowsAsync<ConflictException>(() => fixture.ReturnService(other, fixture.OtherActor)
                .PostLegacyAsync(request));
        await using var check = fixture.Open();
        Assert.Single(await check.LegacySaleReturns.Where(x => x.OperationId == request.OperationId).ToListAsync());
        Assert.Equal(1, await check.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
    }

    [Fact]
    public async Task Settlement_mismatch_unknown_part_and_anonymous_account_reduction_are_rejected()
    {
        var part = await fixture.Part(0);
        await using var db = fixture.Open();
        await Assert.ThrowsAsync<ConflictException>(() => fixture.ReturnService(db).PostLegacyAsync(
            Request(part) with { ConfirmedTotalUsd = 9 }));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.ReturnService(db).PostLegacyAsync(
            Request(part) with { AccountReductionUsd = 10, Refunds = [] }));
        var unknown = Request(part) with
        {
            OperationId = Guid.NewGuid(),
            Lines = [new(Guid.NewGuid(), 1, 10, ReturnCondition.Sellable)]
        };
        await Assert.ThrowsAsync<ConflictException>(() => fixture.ReturnService(db).PostLegacyAsync(unknown));
    }

    private sealed class FailAfterLegacyReturnSave : SaveChangesInterceptor
    {
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result,
            CancellationToken ct = default)
        {
            if (data.Context is AppDbContext context &&
                context.ChangeTracker.Entries().Any(x => x.Entity is LegacySaleReturn))
                throw new InvalidOperationException("Simulated failure after SQL writes before commit");
            return await base.SavedChangesAsync(data, result, ct);
        }
    }

    [Fact]
    public async Task Failure_after_sql_writes_rolls_back_stock_hold_cash_and_account()
    {
        var part = await fixture.Part(0);
        var customer = await fixture.Customer(20);
        var request = Request(part, customer.Id, condition: ReturnCondition.Quarantined) with
        { AccountReductionUsd = 5, Refunds = [new(fixture.Accounts["USD"].Id, MoneyCurrency.USD, 5, 5)] };
        decimal cashBefore;
        await using (var before = fixture.Open())
            cashBefore = await before.CashMovements.Where(x => x.CashAccountId == fixture.Accounts["USD"].Id)
                .SumAsync(x => x.AmountChange);

        await using (var failing = fixture.Open(new FailAfterLegacyReturnSave()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ReturnService(failing)
                .PostLegacyAsync(request));

        await using var check = fixture.Open();
        Assert.False(await check.LegacySaleReturns.AnyAsync(x => x.OperationId == request.OperationId));
        Assert.Equal(0, await check.StockMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(0, await check.StockHoldMovements.Where(x => x.PartId == part.Id).SumAsync(x => x.QuantityChange));
        Assert.Equal(cashBefore, await check.CashMovements
            .Where(x => x.CashAccountId == fixture.Accounts["USD"].Id).SumAsync(x => x.AmountChange));
        Assert.Equal(20, await check.CustomerAccountEntries.Where(x => x.CustomerId == customer.Id).SumAsync(x => x.AmountUsd));
    }

    [Fact]
    public async Task Legacy_return_does_not_clear_existing_recount_warning()
    {
        var part = await fixture.Part(0);
        await using (var setup = fixture.Open())
        {
            await fixture.Service(setup).PostAsync(fixture.CashSale(part) with
            {
                Lines = [new(part.Id, 1, 10, true, "Sellable item physically held")]
            });
        }
        await using (var posting = fixture.Open()) await fixture.ReturnService(posting).PostLegacyAsync(Request(part));
        await using var check = fixture.Open();
        Assert.True((await check.Parts.AsNoTracking().SingleAsync(x => x.Id == part.Id)).NeedsRecount);
    }
}
