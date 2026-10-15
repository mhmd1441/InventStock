using InventStock.Domain.Entities;
using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Sales;

public sealed record PostSaleReturnRequest
{
    public Guid OperationId { get; init; }
    public Guid SaleId { get; init; }
    public string Reason { get; init; } = "";
    public decimal ConfirmedTotalUsd { get; init; }
    public decimal AccountReductionUsd { get; init; }
    public IReadOnlyList<SaleReturnLineRequest> Lines { get; init; } = [];
    public IReadOnlyList<SaleTenderRequest> Refunds { get; init; } = [];
}

public sealed record SaleReturnLineRequest(Guid SaleLineId, int Quantity, ReturnCondition Condition,
    bool PhysicalSeparationConfirmed = false, string? IssueReason = null);
public sealed record SaleReturnLineDto(Guid Id, Guid SaleLineId, Guid PartId, int Quantity,
    decimal RefundUsd, ReturnCondition Condition, string? IssueReason);
public sealed record SaleRefundTenderDto(Guid Id, Guid CashAccountId, string AccountName,
    MoneyCurrency Currency, decimal NativeAmount, decimal ConfirmedUsdAmount);
// Public/counter result intentionally has no supplier or cost fields.
public sealed record SaleReturnDto(Guid Id, Guid OperationId, Guid ActorId, Guid SaleId,
    Guid? CustomerId, DateTime PostedAtUtc, string Reason, decimal TotalUsd,
    decimal AccountReductionUsd, IReadOnlyList<SaleReturnLineDto> Lines,
    IReadOnlyList<SaleRefundTenderDto> Refunds);
public sealed record ReturnableSaleDto(SaleDto Sale, IReadOnlyList<ReturnableSaleLineDto> Lines);
public sealed record ReturnableSaleLineDto(Guid SaleLineId, int ReturnedQuantity, int RemainingQuantity,
    decimal RefundedUsd, decimal RemainingRefundUsd);
