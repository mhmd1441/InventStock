using System.Security.Cryptography;
using System.Text;
using InventStock.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Data;

/// <summary>Shared transaction-scoped operation namespace across stock, receipts, cash and sales.</summary>
public static class OperationGuard
{
    public static async Task LockAsync(AppDbContext db, Guid id, string kind, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new ConflictException("An operation ID is required.");
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Operation guard requires an owned transaction.");
        if (db.Database.IsNpgsql())
        {
            var key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString("N"))), 0);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        }
        if ((kind != "CustomerProfile" && await db.CustomerProfileEvents.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "CustomerOpening" && await db.CustomerAccountEntries.AnyAsync(x => x.OperationId == id && x.Kind == "Opening", ct)) ||
            (kind != "CustomerOpeningCorrection" && await db.CustomerAccountEntries.AnyAsync(x => x.OperationId == id && x.Kind == "OpeningCorrection", ct)) ||
            (kind != "CustomerPayment" && await db.CustomerPayments.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "CustomerCorrection" && await db.CustomerPaymentCorrections.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "Sale" && await db.Sales.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "SaleTenderAccountCorrection" && await db.SaleTenderAccountCorrections.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "SaleTenderAccountCorrectionReversal" && await db.SaleTenderAccountCorrectionReversals.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "SaleReturn" && await db.SaleReturns.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "LegacySaleReturn" && await db.LegacySaleReturns.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "SaleExchange" && await db.SaleExchanges.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "Stock" && await db.StockMovements.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "Hold" && await db.StockHoldMovements.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "Receipt" && await db.PurchaseReceipts.AnyAsync(x => x.OperationId == id, ct)) ||
            (kind != "Cash" && (await db.CashOpeningBalances.AnyAsync(x => x.OperationId == id, ct) ||
                await db.OutgoingPayments.AnyAsync(x => x.OperationId == id, ct) ||
                await db.OutgoingPaymentReversals.AnyAsync(x => x.OperationId == id, ct) ||
                await db.CashReconciliations.AnyAsync(x => x.OperationId == id, ct) ||
                await db.CashReconciliationCorrections.AnyAsync(x => x.OperationId == id, ct) ||
                await db.SupplierPayments.AnyAsync(x => x.OperationId == id, ct) ||
                await db.SupplierPaymentReversals.AnyAsync(x => x.OperationId == id, ct) ||
                await db.CashTransfers.AnyAsync(x => x.OperationId == id, ct) ||
                await db.CashTransferReversals.AnyAsync(x => x.OperationId == id, ct))))
            throw new ConflictException("This operation ID is already used by another command.");
    }
}
