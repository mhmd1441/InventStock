using FluentValidation;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Exceptions;
using InventStock.Application.Interfaces;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Services;

public sealed class TransactionExceptionService(AppDbContext db) : ITransactionExceptionService
{
    public async Task<PagedResult<TransactionExceptionDto>> ListAsync(TransactionExceptionQuery query,
        CancellationToken ct = default)
    {
        if (query.Page > 1_000_000) throw new ValidationException("Page is too large.");
        var to = Normalize(query.ToUtc ?? DateTime.UtcNow.AddMinutes(1));
        var fromUtc = Normalize(query.FromUtc ?? to.AddDays(-30));
        if (fromUtc >= to) throw new ValidationException("From date must be earlier than to date.");
        if (to - fromUtc > TimeSpan.FromDays(366)) throw new ValidationException("Exception review is limited to 366 days at a time.");
        var kind = string.IsNullOrWhiteSpace(query.Kind) ? null : query.Kind.Trim();
        if (kind is not null && !TransactionExceptionKinds.All.Contains(kind))
            throw new ValidationException("Unknown transaction exception kind.");

        IQueryable<ExceptionRow>? combined = null;
        void Add(string branchKind, IQueryable<ExceptionRow> branch)
        {
            if (kind is not null && kind != branchKind) return;
            combined = combined is null ? branch : combined.Concat(branch);
        }

        Add(TransactionExceptionKinds.LinkedReturn,
            from item in db.SaleReturns.AsNoTracking()
            join sale in db.Sales.AsNoTracking() on item.SaleId equals sale.Id
            where item.PostedAtUtc >= fromUtc && item.PostedAtUtc < to
            select new ExceptionRow
            {
                Id = item.Id, Kind = TransactionExceptionKinds.LinkedReturn,
                OccurredAtUtc = item.PostedAtUtc, ActorId = item.ActorId,
                OperationId = item.OperationId, AmountUsd = item.TotalUsd, Reason = item.Reason,
                SaleId = item.SaleId, CustomerId = item.CustomerId, CustomerName = sale.CustomerName,
                PartId = null, PartNumber = null, Quantity = null, StockUnit = null,
                ItemCount = item.Lines.Count
            });

        Add(TransactionExceptionKinds.LegacyReturn,
            from item in db.LegacySaleReturns.AsNoTracking()
            where item.PostedAtUtc >= fromUtc && item.PostedAtUtc < to
            select new ExceptionRow
            {
                Id = item.Id, Kind = TransactionExceptionKinds.LegacyReturn,
                OccurredAtUtc = item.PostedAtUtc, ActorId = item.ActorId,
                OperationId = item.OperationId, AmountUsd = item.TotalUsd, Reason = item.Reason,
                SaleId = null, CustomerId = item.CustomerId, CustomerName = item.CustomerName,
                PartId = null, PartNumber = null, Quantity = null, StockUnit = null,
                ItemCount = item.Lines.Count
            });

        Add(TransactionExceptionKinds.StockShortage,
            from line in db.SaleLines.AsNoTracking()
            join sale in db.Sales.AsNoTracking() on line.SaleId equals sale.Id
            where line.StockShortage && sale.PostedAtUtc >= fromUtc && sale.PostedAtUtc < to
            select new ExceptionRow
            {
                Id = line.Id, Kind = TransactionExceptionKinds.StockShortage,
                OccurredAtUtc = sale.PostedAtUtc, ActorId = sale.ActorId,
                OperationId = sale.OperationId, AmountUsd = line.LineTotalUsd,
                Reason = line.StockExceptionReason ?? "Confirmed shortage sale", SaleId = sale.Id,
                CustomerId = sale.CustomerId, CustomerName = sale.CustomerName, PartId = line.PartId,
                PartNumber = line.PartNumber, Quantity = line.Quantity,
                StockUnit = line.StockUnit == InventStock.Domain.Enums.StockUnit.Piece ? "Piece" : "Centimeter",
                ItemCount = 1
            });

        Add(TransactionExceptionKinds.UncountedSale,
            from line in db.SaleLines.AsNoTracking()
            join sale in db.Sales.AsNoTracking() on line.SaleId equals sale.Id
            where line.StockException && !line.StockShortage && sale.PostedAtUtc >= fromUtc && sale.PostedAtUtc < to
            select new ExceptionRow
            {
                Id = line.Id, Kind = TransactionExceptionKinds.UncountedSale,
                OccurredAtUtc = sale.PostedAtUtc, ActorId = sale.ActorId,
                OperationId = sale.OperationId, AmountUsd = line.LineTotalUsd,
                Reason = line.StockExceptionReason ?? "Uncounted-stock sale", SaleId = sale.Id,
                CustomerId = sale.CustomerId, CustomerName = sale.CustomerName, PartId = line.PartId,
                PartNumber = line.PartNumber, Quantity = line.Quantity,
                StockUnit = line.StockUnit == InventStock.Domain.Enums.StockUnit.Piece ? "Piece" : "Centimeter",
                ItemCount = 1
            });

        Add(TransactionExceptionKinds.CustomerPaymentCorrection,
            from item in db.CustomerPaymentCorrections.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on item.CustomerId equals customer.Id
            where item.CorrectedAtUtc >= fromUtc && item.CorrectedAtUtc < to
            select new ExceptionRow
            {
                Id = item.Id, Kind = TransactionExceptionKinds.CustomerPaymentCorrection,
                OccurredAtUtc = item.CorrectedAtUtc, ActorId = item.ActorId,
                OperationId = item.OperationId, AmountUsd = item.RestoredDebtUsd, Reason = item.Reason,
                SaleId = null, CustomerId = item.CustomerId, CustomerName = customer.Name,
                PartId = null, PartNumber = null, Quantity = null, StockUnit = null, ItemCount = null
            });

        Add(TransactionExceptionKinds.CustomerOpeningCorrection,
            from item in db.CustomerAccountEntries.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on item.CustomerId equals customer.Id
            where item.Kind == "OpeningCorrection" && item.CreatedAtUtc >= fromUtc && item.CreatedAtUtc < to
            select new ExceptionRow
            {
                Id = item.Id, Kind = TransactionExceptionKinds.CustomerOpeningCorrection,
                OccurredAtUtc = item.CreatedAtUtc, ActorId = item.ActorId,
                OperationId = item.OperationId, AmountUsd = item.AmountUsd,
                Reason = item.Reason ?? "Opening-balance correction", SaleId = null,
                CustomerId = item.CustomerId, CustomerName = customer.Name, PartId = null,
                PartNumber = null, Quantity = null, StockUnit = null, ItemCount = null
            });

        Add(TransactionExceptionKinds.OutgoingPaymentReversal,
            from item in db.OutgoingPaymentReversals.AsNoTracking()
            join payment in db.OutgoingPayments.AsNoTracking() on item.OutgoingPaymentId equals payment.Id
            where item.ReversedAtUtc >= fromUtc && item.ReversedAtUtc < to
            select new ExceptionRow
            {
                Id = item.Id, Kind = TransactionExceptionKinds.OutgoingPaymentReversal,
                OccurredAtUtc = item.ReversedAtUtc, ActorId = item.ReversedByUserId,
                OperationId = item.OperationId, AmountUsd = payment.AgreedUsdValue, Reason = item.Reason,
                SaleId = null, CustomerId = null, CustomerName = payment.Payee, PartId = null,
                PartNumber = null, Quantity = null, StockUnit = null, ItemCount = null
            });

        Add(TransactionExceptionKinds.SaleTenderAccountCorrection,
            from item in db.SaleTenderAccountCorrections.AsNoTracking()
            join tender in db.SaleTenders.AsNoTracking() on item.OriginalSaleTenderId equals tender.Id
            join sale in db.Sales.AsNoTracking() on tender.SaleId equals sale.Id
            where item.CorrectedAtUtc >= fromUtc && item.CorrectedAtUtc < to
            select new ExceptionRow
            {
                Id = item.Id, Kind = TransactionExceptionKinds.SaleTenderAccountCorrection,
                OccurredAtUtc = item.CorrectedAtUtc, ActorId = item.CorrectedByUserId,
                OperationId = item.OperationId, AmountUsd = item.ConfirmedUsdAmount, Reason = item.Reason,
                SaleId = sale.Id, CustomerId = sale.CustomerId, CustomerName = sale.CustomerName,
                PartId = null, PartNumber = null, Quantity = null, StockUnit = null, ItemCount = null
            });

        Add(TransactionExceptionKinds.PaperRecovery,
            from item in db.PaperRecoveryEntries.AsNoTracking()
            join sale in db.Sales.AsNoTracking() on item.SaleId equals sale.Id
            where item.SaleId != null && item.EnteredAtUtc >= fromUtc && item.EnteredAtUtc < to
            select new ExceptionRow
            {
                Id = item.Id, Kind = TransactionExceptionKinds.PaperRecovery,
                OccurredAtUtc = item.EnteredAtUtc, ActorId = item.EnteredByUserId,
                OperationId = sale.OperationId, AmountUsd = sale.TotalUsd,
                Reason = "Paper " + item.PaperReference + ": " + item.Reason,
                SaleId = sale.Id, CustomerId = sale.CustomerId, CustomerName = sale.CustomerName,
                PartId = null, PartNumber = null, Quantity = null, StockUnit = null, ItemCount = sale.Lines.Count
            });

        Add(TransactionExceptionKinds.PaperRecovery,
            from item in db.PaperRecoveryEntries.AsNoTracking()
            join payment in db.CustomerPayments.AsNoTracking() on item.CustomerPaymentId equals payment.Id
            where item.CustomerPaymentId != null && item.EnteredAtUtc >= fromUtc && item.EnteredAtUtc < to
            select new ExceptionRow
            {
                Id = item.Id, Kind = TransactionExceptionKinds.PaperRecovery,
                OccurredAtUtc = item.EnteredAtUtc, ActorId = item.EnteredByUserId,
                OperationId = payment.OperationId, AmountUsd = payment.UsdCredit,
                Reason = "Paper " + item.PaperReference + ": " + item.Reason,
                SaleId = null, CustomerId = payment.CustomerId, CustomerName = payment.CustomerName,
                PartId = null, PartNumber = null, Quantity = null, StockUnit = null, ItemCount = null
            });

        if (combined is null) return new([], 0, query.Page, query.PageSize);
        var count = await combined.CountAsync(ct);
        var page = combined.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
            .Skip(query.Skip).Take(query.PageSize);
        // Apply the set operation to a fully server-translatable primitive projection first. Actor
        // display names are joined only after paging; correlated name subqueries in every branch make
        // Npgsql treat the branch projection as client-side and reject Concat.
        var rawRows = await (from item in page
            join actor in db.Users.AsNoTracking() on item.ActorId equals actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            select new ExceptionWithActorRow
            {
                Id = item.Id, Kind = item.Kind, OccurredAtUtc = item.OccurredAtUtc,
                ActorId = item.ActorId, ActorName = actor == null ? "Unknown user" : actor.FullName,
                OperationId = item.OperationId, AmountUsd = item.AmountUsd, Reason = item.Reason,
                SaleId = item.SaleId, CustomerId = item.CustomerId, CustomerName = item.CustomerName,
                PartId = item.PartId, PartNumber = item.PartNumber, Quantity = item.Quantity,
                StockUnit = item.StockUnit, ItemCount = item.ItemCount
            }).ToListAsync(ct);
        var rows = rawRows.Select(item => new TransactionExceptionDto(item.Id, item.Kind,
            item.OccurredAtUtc, item.ActorId, item.ActorName, item.OperationId, item.AmountUsd,
            item.Reason, item.SaleId, item.CustomerId, item.CustomerName, item.PartId,
            item.PartNumber, item.Quantity,
            Enum.TryParse<InventStock.Domain.Enums.StockUnit>(item.StockUnit, out var stockUnit) ? stockUnit : null,
            item.ItemCount)).ToList();
        return new(rows, count, query.Page, query.PageSize);
    }

    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed class ExceptionRow
    {
        public Guid Id { get; init; }
        public string Kind { get; init; } = string.Empty;
        public DateTime OccurredAtUtc { get; init; }
        public Guid ActorId { get; init; }
        public Guid OperationId { get; init; }
        public decimal AmountUsd { get; init; }
        public string Reason { get; init; } = string.Empty;
        public Guid? SaleId { get; init; }
        public Guid? CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public Guid? PartId { get; init; }
        public string? PartNumber { get; init; }
        public int? Quantity { get; init; }
        public string? StockUnit { get; init; }
        public int? ItemCount { get; init; }
    }

    private sealed class ExceptionWithActorRow
    {
        public Guid Id { get; init; }
        public string Kind { get; init; } = string.Empty;
        public DateTime OccurredAtUtc { get; init; }
        public Guid ActorId { get; init; }
        public string ActorName { get; init; } = string.Empty;
        public Guid OperationId { get; init; }
        public decimal AmountUsd { get; init; }
        public string Reason { get; init; } = string.Empty;
        public Guid? SaleId { get; init; }
        public Guid? CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public Guid? PartId { get; init; }
        public string? PartNumber { get; init; }
        public int? Quantity { get; init; }
        public string? StockUnit { get; init; }
        public int? ItemCount { get; init; }
    }
}
