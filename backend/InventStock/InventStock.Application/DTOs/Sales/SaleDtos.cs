using InventStock.Domain.Enums;

namespace InventStock.Application.DTOs.Sales;

public sealed record PostSaleRequest
{
    public Guid OperationId { get; init; }
    public Guid? CustomerId { get; init; }
    public decimal ConfirmedTotalUsd { get; init; }
    public decimal CustomerCreditUsedUsd { get; init; }
    public decimal UnpaidDebtUsd { get; init; }
    public IReadOnlyList<SaleLineRequest> Lines { get; init; } = [];
    public IReadOnlyList<SaleTenderRequest> Payments { get; init; } = [];
    public IReadOnlyList<SaleTenderRequest> Change { get; init; } = [];
}

// Quantity and price are both per immutable stock base unit (piece or centimeter).
public sealed record SaleLineRequest(Guid PartId, int Quantity, decimal UnitSellingPriceUsd,
    bool PhysicalGoodsConfirmed = false, string? StockExceptionReason = null);
public sealed record SaleTenderRequest(Guid CashAccountId, MoneyCurrency Currency, decimal NativeAmount, decimal ConfirmedUsdAmount);

// Counter DTOs deliberately have no cost observation, purchase price, or supplier fields.
public sealed record SaleDto(Guid Id, Guid OperationId, Guid ActorId, DateTime PostedAtUtc,
    Guid? CustomerId, string? CustomerName, decimal TotalUsd, decimal PaymentsUsd, decimal ChangeUsd,
    decimal CustomerCreditUsedUsd, decimal UnpaidDebtUsd,
    IReadOnlyList<SaleLineDto> Lines, IReadOnlyList<SaleTenderDto> Tenders);
public sealed record SaleLineDto(Guid Id, Guid PartId, int Position, string PartNumber, string Name,
    string Brand, string Variant, StockUnit StockUnit, int Quantity, decimal UnitSellingPriceUsd, decimal LineTotalUsd,
    bool StockException, string? StockExceptionReason);
public sealed record SaleTenderDto(Guid Id, Guid CashAccountId, string AccountName, MoneyCurrency Currency,
    bool IsChange, decimal NativeAmount, decimal ConfirmedUsdAmount);
public sealed record SaleOperationDto(string Status, SaleDto Sale);
public sealed record SalePaymentAccountDto(Guid Id, string Name, MoneyCurrency Currency, CashAccountKind Kind);
