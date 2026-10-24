using InventStock.Domain.Common;

namespace InventStock.Domain.Entities;

/// <summary>
/// Immutable link proving that a return and its replacement sale were posted as one checkout.
/// The child records remain normal Sale/SaleReturn records so existing stock, cash and audit
/// projections continue to have one source of truth.
/// </summary>
public sealed class SaleExchange : AuditableEntity
{
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public Guid ActorId { get; private set; }
    public Guid OriginalSaleId { get; private set; }
    public Guid SaleReturnId { get; private set; }
    public SaleReturn SaleReturn { get; private set; } = null!;
    public Guid ReplacementSaleId { get; private set; }
    public Sale ReplacementSale { get; private set; } = null!;
    public Guid? CustomerId { get; private set; }
    public DateTime PostedAtUtc { get; private set; }
    public decimal ReturnTotalUsd { get; private set; }
    public decimal ReturnAppliedUsd { get; private set; }
    public decimal RefundUsd { get; private set; }
    public decimal AccountReductionUsd { get; private set; }
    public decimal ReplacementTotalUsd { get; private set; }

    public static SaleExchange Create(Guid operationId, string fingerprint, Guid actorId, Sale originalSale,
        SaleReturn saleReturn, Sale replacementSale, decimal returnAppliedUsd, decimal refundUsd,
        decimal accountReductionUsd)
    {
        var now = DateTime.UtcNow;
        return new SaleExchange
        {
            OperationId = operationId,
            PayloadFingerprint = fingerprint,
            ActorId = actorId,
            OriginalSaleId = originalSale.Id,
            SaleReturnId = saleReturn.Id,
            SaleReturn = saleReturn,
            ReplacementSaleId = replacementSale.Id,
            ReplacementSale = replacementSale,
            CustomerId = originalSale.CustomerId,
            PostedAtUtc = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc),
            ReturnTotalUsd = saleReturn.TotalUsd,
            ReturnAppliedUsd = returnAppliedUsd,
            RefundUsd = refundUsd,
            AccountReductionUsd = accountReductionUsd,
            ReplacementTotalUsd = replacementSale.TotalUsd
        };
    }
}
