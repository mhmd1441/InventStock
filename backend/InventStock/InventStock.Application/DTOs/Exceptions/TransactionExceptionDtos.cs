using InventStock.Application.Common.Models;
using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Exceptions;

public static class TransactionExceptionKinds
{
    public const string LinkedReturn = "LinkedReturn";
    public const string LegacyReturn = "LegacyReturn";
    public const string StockShortage = "StockShortage";
    public const string UncountedSale = "UncountedSale";
    public const string CustomerPaymentCorrection = "CustomerPaymentCorrection";
    public const string CustomerOpeningCorrection = "CustomerOpeningCorrection";
    public const string OutgoingPaymentReversal = "OutgoingPaymentReversal";
    public const string SaleTenderAccountCorrection = "SaleTenderAccountCorrection";
    public const string PaperRecovery = "PaperRecovery";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        LinkedReturn, LegacyReturn, StockShortage, UncountedSale,
        CustomerPaymentCorrection, CustomerOpeningCorrection, OutgoingPaymentReversal, SaleTenderAccountCorrection, PaperRecovery
    };
}

public sealed class TransactionExceptionQuery : PagedQuery
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Kind { get; set; }
}

/// <summary>Admin-only operational exception. Contains no supplier or purchase-cost data.</summary>
public sealed record TransactionExceptionDto(Guid Id, string Kind, DateTime OccurredAtUtc,
    Guid ActorId, string ActorName, Guid OperationId, decimal? AmountUsd, string Reason,
    Guid? SaleId, Guid? CustomerId, string? CustomerName, Guid? PartId, string? PartNumber,
    int? Quantity, StockUnit? StockUnit, int? ItemCount);
