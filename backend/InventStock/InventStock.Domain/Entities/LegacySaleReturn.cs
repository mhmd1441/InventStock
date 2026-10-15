using InventStock.Domain.Common;
using InventStock.Domain.Enums;

namespace InventStock.Domain.Entities;

/// <summary>
/// Immutable evidence for a return whose original sale predates InventStock. Kept separate from
/// linked returns so manually entered values can never weaken posted-sale refund caps.
/// </summary>
public sealed class LegacySaleReturn : AuditableEntity
{
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public Guid ActorId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public DateOnly? OriginalSaleDate { get; private set; }
    public string? OriginalReference { get; private set; }
    public DateTime PostedAtUtc { get; private set; }
    public string Reason { get; private set; } = null!;
    public decimal TotalUsd { get; private set; }
    public decimal AccountReductionUsd { get; private set; }
    public List<LegacySaleReturnLine> Lines { get; private set; } = [];
    public List<LegacySaleRefundTender> Refunds { get; private set; } = [];

    public static LegacySaleReturn Create(Guid operationId, Guid actorId, string fingerprint,
        Customer? customer, DateOnly? originalSaleDate, string? originalReference, string reason,
        decimal totalUsd, decimal accountReductionUsd)
    {
        var now = DateTime.UtcNow;
        return new()
        {
            OperationId = operationId,
            ActorId = actorId,
            PayloadFingerprint = fingerprint,
            CustomerId = customer?.Id,
            CustomerName = customer?.Name,
            OriginalSaleDate = originalSaleDate,
            OriginalReference = originalReference?.Trim(),
            PostedAtUtc = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc),
            Reason = reason.Trim(),
            TotalUsd = totalUsd,
            AccountReductionUsd = accountReductionUsd
        };
    }
}

public sealed class LegacySaleReturnLine : AuditableEntity
{
    public Guid LegacySaleReturnId { get; private set; }
    public Guid PartId { get; private set; }
    public int Position { get; private set; }
    public string PartNumber { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Brand { get; private set; } = null!;
    public string Variant { get; private set; } = null!;
    public StockUnit StockUnit { get; private set; }
    public int Quantity { get; private set; }
    public decimal RefundUsd { get; private set; }
    public ReturnCondition Condition { get; private set; }
    public Guid StockMovementId { get; private set; }
    public Guid? StockHoldMovementId { get; private set; }
    public string? IssueReason { get; private set; }

    public static LegacySaleReturnLine Create(Guid returnId, Part part, int position, int quantity,
        decimal refundUsd, ReturnCondition condition, Guid stockMovementId, Guid? holdId,
        string? issueReason) => new()
    {
        LegacySaleReturnId = returnId,
        PartId = part.Id,
        Position = position,
        PartNumber = part.PartNumber,
        Name = part.Name,
        Brand = part.Brand,
        Variant = part.Variant,
        StockUnit = part.StockUnit,
        Quantity = quantity,
        RefundUsd = refundUsd,
        Condition = condition,
        StockMovementId = stockMovementId,
        StockHoldMovementId = holdId,
        IssueReason = issueReason?.Trim()
    };
}

public sealed class LegacySaleRefundTender : AuditableEntity
{
    public Guid LegacySaleReturnId { get; private set; }
    public int Position { get; private set; }
    public Guid CashAccountId { get; private set; }
    public string AccountName { get; private set; } = null!;
    public MoneyCurrency Currency { get; private set; }
    public decimal NativeAmount { get; private set; }
    public decimal ConfirmedUsdAmount { get; private set; }

    public static LegacySaleRefundTender Create(Guid returnId, int position, CashAccount account,
        decimal nativeAmount, decimal confirmedUsd) => new()
    {
        LegacySaleReturnId = returnId,
        Position = position,
        CashAccountId = account.Id,
        AccountName = account.Name,
        Currency = account.Currency,
        NativeAmount = nativeAmount,
        ConfirmedUsdAmount = confirmedUsd
    };
}
