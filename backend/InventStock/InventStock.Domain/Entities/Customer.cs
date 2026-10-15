using InventStock.Domain.Common;

namespace InventStock.Domain.Entities;

public sealed class Customer : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? DistinguishingLabel { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public int Revision { get; private set; }
    public bool IsActive { get; private set; } = true;
    public static Customer Create(string name, string? distinguishingLabel = null) => new()
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), 200),
        DistinguishingLabel = Guard.AgainstTooLong(distinguishingLabel, nameof(DistinguishingLabel), 200)
    };

    public void UpdateProfile(string name, string? label, string? phone, string? address)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), 200);
        DistinguishingLabel = Guard.AgainstTooLong(label?.Trim(), nameof(DistinguishingLabel), 200);
        Phone = Guard.AgainstTooLong(phone?.Trim(), nameof(Phone), 50);
        Address = Guard.AgainstTooLong(address?.Trim(), nameof(Address), 500);
        Revision++;
    }
}

/// <summary>Immutable profile snapshot, including creation. Duplicate names are never auto-merged.</summary>
public sealed class CustomerProfileEvent : AuditableEntity
{
    public Guid CustomerId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid ActorId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? DistinguishingLabel { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public int Revision { get; private set; }
    public static CustomerProfileEvent Create(Customer customer, Guid operation, Guid actor, string fingerprint) => new()
    {
        CustomerId = customer.Id, OperationId = operation, ActorId = actor, PayloadFingerprint = fingerprint,
        Name = customer.Name, DistinguishingLabel = customer.DistinguishingLabel, Phone = customer.Phone,
        Address = customer.Address, Revision = customer.Revision
    };
}

/// <summary>Positive USD means customer owes the shop; negative means shop holds customer credit.</summary>
public sealed class CustomerAccountEntry : AuditableEntity
{
    public Guid CustomerId { get; private set; }
    public Guid? SaleId { get; private set; }
    public Guid? SaleReturnId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Kind { get; private set; } = null!;
    public decimal AmountUsd { get; private set; }
    public Guid? CustomerPaymentId { get; private set; }
    public Guid? CustomerPaymentCorrectionId { get; private set; }
    public Guid? OriginalOpeningEntryId { get; private set; }
    public decimal? CorrectedOpeningBalanceUsd { get; private set; }
    public decimal? BalanceAfterUsd { get; private set; }
    public string? Reason { get; private set; }
    public string? PayloadFingerprint { get; private set; }
    public DateTime? EffectiveAtUtc { get; private set; }
    public static CustomerAccountEntry SaleCharge(Guid customerId, Guid saleId, Guid operationId, Guid actorId, decimal total) =>
        Create(customerId, saleId, operationId, actorId, "SaleCharge", total);
    public static CustomerAccountEntry SalePayment(Guid customerId, Guid saleId, Guid operationId, Guid actorId, decimal netPayment) =>
        Create(customerId, saleId, operationId, actorId, "SalePayment", -netPayment);
    public static CustomerAccountEntry ReturnReduction(Guid customerId, Guid saleReturnId, Guid operationId,
        Guid actorId, decimal amount)
    {
        var entry = Create(customerId, null, operationId, actorId, "ReturnReduction", -amount);
        entry.SaleReturnId = saleReturnId;
        return entry;
    }
    public static CustomerAccountEntry Opening(Guid customerId, Guid operationId, Guid actorId, decimal balance) =>
        Create(customerId, null, operationId, actorId, "Opening", balance);
    public void DescribeOpening(string reason, DateTime effectiveAtUtc, string fingerprint)
    {
        Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(Reason), 500);
        EffectiveAtUtc = effectiveAtUtc;
        PayloadFingerprint = fingerprint;
    }
    public static CustomerAccountEntry OpeningCorrection(CustomerAccountEntry opening, Guid operationId, Guid actorId,
        decimal correctedOpeningBalance, decimal delta, decimal balanceAfter, string reason, string fingerprint)
    {
        var entry = Create(opening.CustomerId, null, operationId, actorId, "OpeningCorrection", delta);
        entry.OriginalOpeningEntryId = opening.Id;
        entry.CorrectedOpeningBalanceUsd = correctedOpeningBalance;
        entry.BalanceAfterUsd = balanceAfter;
        entry.Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(Reason), 500);
        entry.PayloadFingerprint = fingerprint;
        entry.EffectiveAtUtc = opening.EffectiveAtUtc;
        return entry;
    }
    public static CustomerAccountEntry Payment(Guid customer, Guid payment, Guid operation, Guid actor, decimal usd)
    {
        var entry = Create(customer, null, operation, actor, "AccountPayment", -usd);
        entry.CustomerPaymentId = payment;
        return entry;
    }
    public static CustomerAccountEntry PaymentCorrection(CustomerPaymentCorrection correction)
    {
        var entry = Create(correction.CustomerId, null, correction.OperationId, correction.ActorId, "PaymentCorrection", correction.RestoredDebtUsd);
        entry.CustomerPaymentCorrectionId = correction.Id;
        entry.CustomerPaymentId = correction.OriginalPaymentId;
        entry.Reason = correction.Reason;
        return entry;
    }
    private static CustomerAccountEntry Create(Guid customer, Guid? sale, Guid operation, Guid actor, string kind, decimal amount) => new()
    { CustomerId = customer, SaleId = sale, OperationId = operation, ActorId = actor, Kind = kind, AmountUsd = amount };
}
