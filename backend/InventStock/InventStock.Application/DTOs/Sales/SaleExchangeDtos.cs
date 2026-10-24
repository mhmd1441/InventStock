namespace InventStock.Application.DTOs.Sales;

public sealed record PostSaleExchangeRequest
{
    public Guid OperationId { get; init; }
    public Guid OriginalSaleId { get; init; }
    public string Reason { get; init; } = "";
    public decimal ConfirmedReturnTotalUsd { get; init; }
    public decimal ReturnAppliedUsd { get; init; }
    public decimal AccountReductionUsd { get; init; }
    public IReadOnlyList<SaleReturnLineRequest> ReturnLines { get; init; } = [];
    public IReadOnlyList<SaleTenderRequest> Refunds { get; init; } = [];
    public decimal ConfirmedReplacementTotalUsd { get; init; }
    public decimal CustomerCreditUsedUsd { get; init; }
    public decimal UnpaidDebtUsd { get; init; }
    public IReadOnlyList<SaleLineRequest> ReplacementLines { get; init; } = [];
    public IReadOnlyList<SaleTenderRequest> Payments { get; init; } = [];
    public IReadOnlyList<SaleTenderRequest> Change { get; init; } = [];
}

// Counter-safe: the nested projections contain no cost observation or supplier fields.
public sealed record SaleExchangeDto(Guid Id, Guid OperationId, Guid ActorId, Guid OriginalSaleId,
    Guid? CustomerId, DateTime PostedAtUtc, decimal ReturnTotalUsd, decimal ReturnAppliedUsd,
    decimal RefundUsd, decimal AccountReductionUsd, decimal ReplacementTotalUsd,
    SaleReturnDto Return, SaleDto ReplacementSale);

