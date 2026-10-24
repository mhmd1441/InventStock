using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Immutable posted sale. All related ledger entries commit in its transaction.</summary>
public sealed class Sale : AuditableEntity
{
    public Guid OperationId { get; private set; }
    /// <summary>Internal sequential document number; not a legal tax-invoice number.</summary>
    public long ReceiptNumber { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public Guid ActorId { get; private set; }
    public DateTime PostedAtUtc { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public decimal TotalUsd { get; private set; }
    public decimal PaymentsUsd { get; private set; }
    public decimal ChangeUsd { get; private set; }
    public decimal ExchangeCreditUsd { get; private set; }
    public decimal CustomerCreditUsedUsd { get; private set; }
    public decimal UnpaidDebtUsd { get; private set; }
    public List<SaleLine> Lines { get; private set; } = [];
    public List<SaleTender> Tenders { get; private set; } = [];

    public static Sale Create(Guid operationId, string fingerprint, Guid actorId, Customer? customer,
        decimal total, decimal payments, decimal change, decimal credit, decimal debt,
        decimal exchangeCredit = 0, DateTime? postedAtUtc = null) => new()
    {
        OperationId = operationId, PayloadFingerprint = fingerprint, ActorId = actorId,
        PostedAtUtc = postedAtUtc.HasValue ? NormalizeTimestamp(postedAtUtc.Value) : PostingTimestamp(),
        CustomerId = customer?.Id, CustomerName = customer?.Name, TotalUsd = total, PaymentsUsd = payments,
        ChangeUsd = change, ExchangeCreditUsd = exchangeCredit,
        CustomerCreditUsedUsd = credit, UnpaidDebtUsd = debt
    };

    private static DateTime PostingTimestamp()
    {
        var now = DateTime.UtcNow;
        // PostgreSQL persists microseconds; make the first outcome identical to later recovery.
        return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }

    private static DateTime NormalizeTimestamp(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new DomainException("Posted time must be UTC.");
        return new DateTime(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
    }

    /// <summary>Only used by non-PostgreSQL test providers; production numbering is database generated.</summary>
    public void AssignReceiptNumberForNonPostgres(long receiptNumber)
    {
        if (receiptNumber < 1) throw new DomainException("Receipt number must be positive.");
        ReceiptNumber = receiptNumber;
    }
}

public sealed class SaleLine : AuditableEntity
{
    public Guid SaleId { get; private set; }
    public Guid PartId { get; private set; }
    public int Position { get; private set; }
    public string PartNumber { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Brand { get; private set; } = null!;
    public string Variant { get; private set; } = null!;
    public StockUnit StockUnit { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitSellingPriceUsd { get; private set; }
    // Public counter-safe suggestion captured at posting; no supplier cost or rate is exposed.
    public decimal? SuggestedUnitPriceUsd { get; private set; }
    public bool PriceOverridden { get; private set; }
    public decimal LineTotalUsd { get; private set; }
    public Guid StockMovementId { get; private set; }
    public bool StockException { get; private set; }
    public bool StockShortage { get; private set; }
    public string? StockExceptionReason { get; private set; }
    // Private, nullable last actual purchase evidence; not a valuation or guaranteed margin.
    public Guid? CostObservationId { get; private set; }
    public decimal? EstimatedBaseUnitCost { get; private set; }
    public MoneyCurrency? CostCurrency { get; private set; }
    public TaxTreatment? CostTaxTreatment { get; private set; }
    public static SaleLine Create(Guid saleId, Part part, int position, int quantity, decimal price,
        decimal total, Guid movementId, bool exception, bool shortage, string? reason, SupplierCostObservation? cost,
        decimal? suggestedUnitPriceUsd) => new()
    {
        SaleId = saleId, PartId = part.Id, Position = position, PartNumber = part.PartNumber, Name = part.Name,
        Brand = part.Brand, Variant = part.Variant, StockUnit = part.StockUnit, Quantity = quantity,
        UnitSellingPriceUsd = price, SuggestedUnitPriceUsd = suggestedUnitPriceUsd,
        PriceOverridden = suggestedUnitPriceUsd.HasValue && price != suggestedUnitPriceUsd.Value,
        LineTotalUsd = total, StockMovementId = movementId,
        StockException = exception, StockShortage = shortage, StockExceptionReason = exception ? reason?.Trim() : null,
        CostObservationId = cost?.Id,
        EstimatedBaseUnitCost = cost is null ? null : decimal.Round(cost.UnitCost / cost.PackQuantity, 6, MidpointRounding.AwayFromZero),
        CostCurrency = cost?.Currency, CostTaxTreatment = cost?.TaxTreatment
    };
}

public sealed class SaleTender : AuditableEntity
{
    public Guid SaleId { get; private set; }
    public int Position { get; private set; }
    public Guid CashAccountId { get; private set; }
    public string AccountName { get; private set; } = null!;
    public MoneyCurrency Currency { get; private set; }
    public bool IsChange { get; private set; }
    public decimal NativeAmount { get; private set; }
    public decimal ConfirmedUsdAmount { get; private set; }
    public static SaleTender Create(Guid saleId, int position, CashAccount account, bool change, decimal amount, decimal usd) => new()
    {
        SaleId = saleId, Position = position, CashAccountId = account.Id, AccountName = account.Name,
        Currency = account.Currency, IsChange = change, NativeAmount = amount, ConfirmedUsdAmount = usd
    };
}

/// <summary>
/// Immutable correction that moves an already-posted tender between the shop's
/// own accounts. The sale and customer ledger never change.
/// </summary>
public sealed class SaleTenderAccountCorrection : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    public const int FingerprintMaxLength = 64;

    private SaleTenderAccountCorrection() { }

    public Guid OriginalSaleTenderId { get; private set; }
    public Guid OriginalCashAccountId { get; private set; }
    public Guid CorrectCashAccountId { get; private set; }
    public MoneyCurrency Currency { get; private set; }
    public decimal NativeAmount { get; private set; }
    public decimal ConfirmedUsdAmount { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateTime CorrectedAtUtc { get; private set; }
    public Guid CorrectedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;

    public static SaleTenderAccountCorrection Create(SaleTender original, CashAccount correctedAccount,
        string reason, Guid actor, Guid operationId, string fingerprint, DateTime nowUtc)
    {
        if (correctedAccount.Id == original.CashAccountId)
            throw new DomainException("The corrected account must differ from the original tender account.");
        if (correctedAccount.Currency != original.Currency)
            throw new DomainException("A sale tender correction cannot change currency.");
        return new SaleTenderAccountCorrection
        {
            OriginalSaleTenderId = original.Id,
            OriginalCashAccountId = original.CashAccountId,
            CorrectCashAccountId = correctedAccount.Id,
            Currency = original.Currency,
            NativeAmount = original.NativeAmount,
            ConfirmedUsdAmount = original.ConfirmedUsdAmount,
            Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(reason), ReasonMaxLength),
            CorrectedAtUtc = nowUtc.ToUniversalTime(), CorrectedByUserId = actor,
            OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(fingerprint), FingerprintMaxLength)
        };
    }
}

public sealed class SaleTenderAccountCorrectionReversal : AuditableEntity
{
    public const int ReasonMaxLength = 500;
    private SaleTenderAccountCorrectionReversal() { }
    public Guid SaleTenderAccountCorrectionId { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateTime ReversedAtUtc { get; private set; }
    public Guid ReversedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public static SaleTenderAccountCorrectionReversal Create(Guid correctionId, string reason, Guid actor, Guid operationId, string fingerprint, DateTime nowUtc) => new()
    {
        SaleTenderAccountCorrectionId = correctionId,
        Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(reason), ReasonMaxLength),
        ReversedAtUtc = nowUtc.ToUniversalTime(), ReversedByUserId = actor, OperationId = operationId,
        PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(fingerprint), SaleTenderAccountCorrection.FingerprintMaxLength)
    };
}
