using InventStock.Application.DTOs.Cash;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Tests.Infrastructure;

[Collection("PostgreSQL schema migrations")]
public sealed class PostgresSupplierPaymentTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    private async Task<Supplier> SupplierAsync()
    {
        await using var db = fixture.Open();
        var supplier = Supplier.Create("Korri " + Guid.NewGuid().ToString("N"), null, null, null, null);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    [Fact]
    public async Task Supplier_payment_fee_replay_and_reversal_keep_native_cash_lines_separate()
    {
        var supplier = await SupplierAsync();
        var account = fixture.Accounts["WHISH"];
        await using var db = fixture.Open();
        var service = fixture.CashService(db, fixture.AdminActor);
        var request = new PostSupplierPaymentRequest
        {
            OperationId = Guid.NewGuid(), SupplierId = supplier.Id, CashAccountId = account.Id,
            PaidOn = new DateOnly(2026, 1, 10), Note = "Battery order", ReceiptReference = "K-100",
            Lines =
            [
                new() { Kind = SupplierPaymentLineKind.Settlement, OriginalAmount = 1_000m, Currency = MoneyCurrency.USD, AgreedUsdValue = 1_000m },
                new() { Kind = SupplierPaymentLineKind.Fee, OriginalAmount = 10m, Currency = MoneyCurrency.USD, AgreedUsdValue = 10m }
            ]
        };
        var posted = await service.PostSupplierPaymentAsync(request);
        Assert.Equal(posted.Id, (await service.PostSupplierPaymentAsync(request)).Id);
        Assert.Equal(2, posted.Lines.Count);
        Assert.Equal(1_000m, posted.Lines.Single(x => x.Kind == SupplierPaymentLineKind.Settlement).OriginalAmount);
        Assert.Equal(10m, posted.Lines.Single(x => x.Kind == SupplierPaymentLineKind.Fee).OriginalAmount);
        Assert.Equal(-1_010m, await db.CashMovements.Where(x => x.SupplierPaymentLineId != null).SumAsync(x => x.AmountChange));

        var close = await service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = request.PaidOn, OpeningAmount = 2_000m,
            CountedAmount = 990m, CountedAtUtc = DateTime.UtcNow
        });
        Assert.Equal(990m, close.ExpectedClosingAmount);

        var reversed = await service.ReverseSupplierPaymentAsync(posted.Id, new ReverseSupplierPaymentRequest
        {
            OperationId = Guid.NewGuid(), Reason = "Whish transfer was not sent", ReversedAtUtc = DateTime.UtcNow
        });
        Assert.True(reversed.IsReversed);
        Assert.Equal(0m, await db.CashMovements.Where(x => x.SupplierPaymentLineId != null || x.SupplierPaymentReversalId != null)
            .SumAsync(x => x.AmountChange));
    }

    [Fact]
    public async Task Lbp_supplier_payment_preserves_native_amount_and_is_in_business_day_expected_cash()
    {
        var supplier = await SupplierAsync();
        var account = fixture.Accounts["LBP"];
        var date = new DateOnly(2026, 1, 12);
        await using var db = fixture.Open();
        var service = fixture.CashService(db, fixture.AdminActor);
        var payment = await service.PostSupplierPaymentAsync(new PostSupplierPaymentRequest
        {
            OperationId = Guid.NewGuid(), SupplierId = supplier.Id, CashAccountId = account.Id, PaidOn = date,
            Lines = [new() { Kind = SupplierPaymentLineKind.Settlement, OriginalAmount = 900_000m, Currency = MoneyCurrency.LBP, AgreedUsdValue = 10m }]
        });
        Assert.Equal(900_000m, payment.Lines.Single().OriginalAmount);
        Assert.Equal(10m, payment.Lines.Single().AgreedUsdValue);
        var close = await service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = date, OpeningAmount = 1_000_000m,
            CountedAmount = 100_000m, CountedAtUtc = DateTime.UtcNow
        });
        Assert.Equal(100_000m, close.ExpectedClosingAmount);
    }
}
