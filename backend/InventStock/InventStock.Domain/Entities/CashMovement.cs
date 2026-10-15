using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Append-only native-currency movement. Positive adds funds; negative removes funds.</summary>
public sealed class CashMovement : AuditableEntity
{
    private CashMovement() { }

    public Guid CashAccountId { get; private set; }
    public CashAccount CashAccount { get; private set; } = null!;
    public CashMovementType MovementType { get; private set; }
    public decimal AmountChange { get; private set; }
    public Guid PerformedByUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid? OpeningBalanceId { get; private set; }
    public Guid? OutgoingPaymentId { get; private set; }
    public Guid? OutgoingPaymentReversalId { get; private set; }
    public Guid? SaleTenderId { get; private set; }
    public Guid? CustomerPaymentTenderId { get; private set; }
    public Guid? CustomerPaymentCorrectionId { get; private set; }
    public Guid? SaleRefundTenderId { get; private set; }
    public Guid? LegacySaleRefundTenderId { get; private set; }

    public static CashMovement LegacySaleRefund(LegacySaleRefundTender tender, Guid actor, DateTime time)
    {
        var movement = Create(tender.CashAccountId, CashMovementType.LegacySaleRefund,
            -tender.NativeAmount, actor, time, null, null, null);
        movement.LegacySaleRefundTenderId = tender.Id;
        return movement;
    }

    public static CashMovement SaleRefund(SaleRefundTender tender, Guid actor, DateTime time)
    {
        var movement = Create(tender.CashAccountId, CashMovementType.SaleRefund,
            -tender.NativeAmount, actor, time, null, null, null);
        movement.SaleRefundTenderId = tender.Id;
        return movement;
    }

    public static CashMovement CustomerTender(CustomerPaymentTender tender, Guid actor, DateTime time, Guid? correctionId = null)
    {
        var amount = tender.IsChange ? -tender.NativeAmount : tender.NativeAmount;
        var type = tender.IsChange ? CashMovementType.CustomerChange : CashMovementType.CustomerPayment;
        if (correctionId.HasValue) { amount = -amount; type = CashMovementType.CustomerPaymentCorrection; }
        var movement = Create(tender.CashAccountId, type, amount, actor, time, null, null, null);
        movement.CustomerPaymentTenderId = tender.Id;
        movement.CustomerPaymentCorrectionId = correctionId;
        return movement;
    }

    public static CashMovement SaleTender(SaleTender tender, Guid actorId, DateTime postedAtUtc)
    {
        var movement = Create(tender.CashAccountId,
            tender.IsChange ? CashMovementType.SaleChange : CashMovementType.SalePayment,
            tender.IsChange ? -tender.NativeAmount : tender.NativeAmount, actorId, postedAtUtc, null, null, null);
        movement.SaleTenderId = tender.Id;
        return movement;
    }

    public static CashMovement Opening(Guid accountId, decimal amount, Guid actorId, DateTime occurredAtUtc, Guid openingId) =>
        Create(accountId, CashMovementType.OpeningBalance, amount, actorId, occurredAtUtc, openingId, null, null);

    public static CashMovement Payment(Guid accountId, decimal amount, Guid actorId, DateTime occurredAtUtc, Guid paymentId) =>
        Create(accountId, CashMovementType.OutgoingPayment, -amount, actorId, occurredAtUtc, null, paymentId, null);

    public static CashMovement Reversal(Guid accountId, decimal amount, Guid actorId, DateTime occurredAtUtc, Guid reversalId) =>
        Create(accountId, CashMovementType.OutgoingPaymentReversal, amount, actorId, occurredAtUtc, null, null, reversalId);

    private static CashMovement Create(Guid accountId, CashMovementType type, decimal amountChange, Guid actorId,
        DateTime occurredAtUtc, Guid? openingId, Guid? paymentId, Guid? reversalId)
    {
        if (accountId == Guid.Empty || actorId == Guid.Empty) throw new DomainException("Account and actor are required.");
        if (amountChange == 0 && type != CashMovementType.OpeningBalance)
            throw new DomainException("Cash movement amount must not be zero.");
        return new CashMovement
        {
            CashAccountId = accountId, MovementType = type, AmountChange = amountChange,
            PerformedByUserId = actorId,
            OccurredAtUtc = occurredAtUtc.Kind == DateTimeKind.Utc ? occurredAtUtc : occurredAtUtc.ToUniversalTime(),
            OpeningBalanceId = openingId, OutgoingPaymentId = paymentId, OutgoingPaymentReversalId = reversalId
        };
    }
}
