using InventStock.Domain.Common;
using InventStock.Domain.Enums;

namespace InventStock.Domain.Entities;

/// <summary>Immutable return against a posted sale. Refund, account effect and stock post together.</summary>
public sealed class SaleReturn : AuditableEntity
{
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public Guid ActorId { get; private set; }
    public Guid SaleId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public DateTime PostedAtUtc { get; private set; }
    public string Reason { get; private set; } = null!;
    public decimal TotalUsd { get; private set; }
    public decimal AccountReductionUsd { get; private set; }
    public List<SaleReturnLine> Lines { get; private set; } = [];
    public List<SaleRefundTender> Refunds { get; private set; } = [];

    public static SaleReturn Create(Sale sale, Guid operationId, Guid actorId, string fingerprint,
        string reason, decimal totalUsd, decimal accountReductionUsd)
    {
        var now = DateTime.UtcNow;
        return new()
        {
            SaleId = sale.Id, CustomerId = sale.CustomerId, OperationId = operationId,
            ActorId = actorId, PayloadFingerprint = fingerprint, Reason = reason.Trim(),
            PostedAtUtc = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc),
            TotalUsd = totalUsd, AccountReductionUsd = accountReductionUsd
        };
    }
}

public enum ReturnCondition { Sellable = 1, Quarantined = 2 }

public sealed class SaleReturnLine : AuditableEntity
{
    public Guid SaleReturnId { get; private set; }
    public Guid SaleLineId { get; private set; }
    public Guid PartId { get; private set; }
    public int Position { get; private set; }
    public int Quantity { get; private set; }
    public decimal RefundUsd { get; private set; }
    public ReturnCondition Condition { get; private set; }
    public Guid StockMovementId { get; private set; }
    public Guid? StockHoldMovementId { get; private set; }
    public string? IssueReason { get; private set; }

    public static SaleReturnLine Create(Guid saleReturnId, SaleLine original, int position, int quantity,
        decimal refundUsd, ReturnCondition condition, Guid stockMovementId, Guid? holdId, string? issueReason) => new()
    {
        SaleReturnId = saleReturnId, SaleLineId = original.Id, PartId = original.PartId,
        Position = position, Quantity = quantity, RefundUsd = refundUsd, Condition = condition,
        StockMovementId = stockMovementId, StockHoldMovementId = holdId, IssueReason = issueReason?.Trim()
    };
}

public sealed class SaleRefundTender : AuditableEntity
{
    public Guid SaleReturnId { get; private set; }
    public int Position { get; private set; }
    public Guid CashAccountId { get; private set; }
    public string AccountName { get; private set; } = null!;
    public MoneyCurrency Currency { get; private set; }
    public decimal NativeAmount { get; private set; }
    public decimal ConfirmedUsdAmount { get; private set; }

    public static SaleRefundTender Create(Guid saleReturnId, int position, CashAccount account,
        decimal nativeAmount, decimal confirmedUsd) => new()
    {
        SaleReturnId = saleReturnId, Position = position, CashAccountId = account.Id,
        AccountName = account.Name, Currency = account.Currency,
        NativeAmount = nativeAmount, ConfirmedUsdAmount = confirmedUsd
    };
}
