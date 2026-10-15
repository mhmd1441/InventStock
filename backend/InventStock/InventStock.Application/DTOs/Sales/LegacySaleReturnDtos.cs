using InventStock.Domain.Entities;
using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Sales;

public sealed record PostLegacySaleReturnRequest
{
    public Guid OperationId { get; init; }
    public Guid? CustomerId { get; init; }
    public DateOnly? OriginalSaleDate { get; init; }
    public string? OriginalReference { get; init; }
    public string Reason { get; init; } = "";
    public decimal ConfirmedTotalUsd { get; init; }
    public decimal AccountReductionUsd { get; init; }
    public IReadOnlyList<LegacySaleReturnLineRequest> Lines { get; init; } = [];
    public IReadOnlyList<SaleTenderRequest> Refunds { get; init; } = [];
}

public sealed record LegacySaleReturnLineRequest(Guid PartId, int Quantity, decimal RefundUsd,
    ReturnCondition Condition, bool PhysicalSeparationConfirmed = false, string? IssueReason = null);
public sealed record LegacySaleReturnLineDto(Guid Id, Guid PartId, int Position, string PartNumber,
    string Name, string Brand, string Variant, StockUnit StockUnit, int Quantity, decimal RefundUsd,
    ReturnCondition Condition, string? IssueReason);
public sealed record LegacySaleRefundTenderDto(Guid Id, Guid CashAccountId, string AccountName,
    MoneyCurrency Currency, decimal NativeAmount, decimal ConfirmedUsdAmount);

// Counter-safe by construction: this projection contains no cost or supplier fields.
public sealed record LegacySaleReturnDto(Guid Id, Guid OperationId, Guid ActorId, Guid? CustomerId,
    string? CustomerName, DateOnly? OriginalSaleDate, string? OriginalReference, DateTime PostedAtUtc,
    string Reason, decimal TotalUsd, decimal AccountReductionUsd,
    IReadOnlyList<LegacySaleReturnLineDto> Lines, IReadOnlyList<LegacySaleRefundTenderDto> Refunds);
