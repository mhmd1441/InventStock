using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Cash;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Tests.Infrastructure;

[Collection("PostgreSQL schema migrations")]
public sealed class PostgresCashTransferTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    [Fact]
    public async Task Whish_cashout_fee_is_separate_replays_and_reversal_restores_both_accounts()
    {
        var date = new DateOnly(2026, 3, 10);
        var whish = fixture.Accounts["WHISH"];
        var safe = fixture.Accounts["USD"];
        await using var db = fixture.Open();
        var service = fixture.CashService(db, fixture.AdminActor);
        var request = new PostCashTransferRequest
        {
            OperationId = Guid.NewGuid(), SourceCashAccountId = whish.Id, DestinationCashAccountId = safe.Id,
            TransferredOn = date, SourceAmount = 1_000m, SourceCurrency = MoneyCurrency.USD,
            DestinationAmount = 990m, DestinationCurrency = MoneyCurrency.USD, AgreedRate = 1m,
            FeeCashAccountId = whish.Id, FeeAmount = 10m, FeeCurrency = MoneyCurrency.USD, Note = "Whish cash-out"
        };
        var posted = await service.PostTransferAsync(request);
        Assert.Equal(posted.Id, (await service.PostTransferAsync(request)).Id);
        Assert.Equal(-1_010m, await db.CashMovements.Where(x => x.CashTransferId == posted.Id && x.CashAccountId == whish.Id).SumAsync(x => x.AmountChange));
        Assert.Equal(990m, await db.CashMovements.Where(x => x.CashTransferId == posted.Id && x.CashAccountId == safe.Id).SumAsync(x => x.AmountChange));
        Assert.Equal(3, await db.CashMovements.CountAsync(x => x.CashTransferId == posted.Id));

        var reversed = await service.ReverseTransferAsync(posted.Id, new ReverseCashTransferRequest
        {
            OperationId = Guid.NewGuid(), Reason = "Cash-out was cancelled", ReversedAtUtc = DateTime.UtcNow
        });
        Assert.True(reversed.IsReversed);
        Assert.Equal(0m, await db.CashMovements.Where(x => x.CashTransferId == posted.Id || x.CashTransferReversalId == reversed.ReversalId).SumAsync(x => x.AmountChange));
    }

    [Fact]
    public async Task Currency_exchange_keeps_native_amounts_rate_and_rejects_changed_payload_replay()
    {
        var usd = fixture.Accounts["USD"];
        var lbp = fixture.Accounts["LBP"];
        await using var db = fixture.Open();
        var service = fixture.CashService(db, fixture.AdminActor);
        var request = new PostCashTransferRequest
        {
            OperationId = Guid.NewGuid(), SourceCashAccountId = usd.Id, DestinationCashAccountId = lbp.Id,
            TransferredOn = new DateOnly(2026, 3, 11), SourceAmount = 100m, SourceCurrency = MoneyCurrency.USD,
            DestinationAmount = 9_000_000m, DestinationCurrency = MoneyCurrency.LBP, AgreedRate = 90_000m
        };
        var posted = await service.PostTransferAsync(request);
        Assert.Equal(100m, posted.SourceAmount); Assert.Equal(9_000_000m, posted.DestinationAmount); Assert.Equal(90_000m, posted.AgreedRate);
        await Assert.ThrowsAsync<ConflictException>(() => service.PostTransferAsync(request with { DestinationAmount = 8_900_000m }));
    }

    [Fact]
    public async Task Eur_exchange_preserves_both_eur_to_usd_and_eur_to_lbp_native_legs()
    {
        var eur = fixture.Accounts["EUR"];
        var usd = fixture.Accounts["USD"];
        var lbp = fixture.Accounts["LBP"];
        await using var db = fixture.Open();
        var service = fixture.CashService(db, fixture.AdminActor);
        var eurToUsd = await service.PostTransferAsync(new PostCashTransferRequest
        {
            OperationId = Guid.NewGuid(), SourceCashAccountId = eur.Id, DestinationCashAccountId = usd.Id,
            TransferredOn = new DateOnly(2026, 3, 12), SourceAmount = 100m, SourceCurrency = MoneyCurrency.EUR,
            DestinationAmount = 117m, DestinationCurrency = MoneyCurrency.USD, AgreedRate = 1.17m, Note = "EUR exchange"
        });
        var eurToLbp = await service.PostTransferAsync(new PostCashTransferRequest
        {
            OperationId = Guid.NewGuid(), SourceCashAccountId = eur.Id, DestinationCashAccountId = lbp.Id,
            TransferredOn = new DateOnly(2026, 3, 12), SourceAmount = 50m, SourceCurrency = MoneyCurrency.EUR,
            DestinationAmount = 5_265_000m, DestinationCurrency = MoneyCurrency.LBP, AgreedRate = 105_300m, Note = "EUR to LBP exchange"
        });
        Assert.Equal(MoneyCurrency.EUR, eurToUsd.SourceCurrency);
        Assert.Equal(MoneyCurrency.USD, eurToUsd.DestinationCurrency);
        Assert.Equal(MoneyCurrency.EUR, eurToLbp.SourceCurrency);
        Assert.Equal(MoneyCurrency.LBP, eurToLbp.DestinationCurrency);
        Assert.Equal(-150m, await db.CashMovements.Where(x => x.CashAccountId == eur.Id &&
            (x.CashTransferId == eurToUsd.Id || x.CashTransferId == eurToLbp.Id)).SumAsync(x => x.AmountChange));
    }
}
