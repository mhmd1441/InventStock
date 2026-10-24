using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Cash settlement made to a supplier. This deliberately does not create a supplier payable balance.</summary>
public sealed class SupplierPayment : AuditableEntity
{
    public const int TextMaxLength = 500;
    public const int ReferenceMaxLength = 300;
    public const int FingerprintMaxLength = 64;
    private readonly List<SupplierPaymentLine> _lines = new();
    private SupplierPayment() { }

    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public Guid CashAccountId { get; private set; }
    public CashAccount CashAccount { get; private set; } = null!;
    public DateOnly PaidOn { get; private set; }
    public string? Note { get; private set; }
    public string? ReceiptReference { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;
    public IReadOnlyCollection<SupplierPaymentLine> Lines => _lines.AsReadOnly();

    public static SupplierPayment Create(Guid supplierId, Guid cashAccountId, DateOnly paidOn, string? note,
        string? receiptReference, Guid actorId, Guid operationId, string fingerprint)
    {
        if (supplierId == Guid.Empty || cashAccountId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty)
            throw new DomainException("Supplier, cash account, actor and operation are required.");
        return new SupplierPayment
        {
            SupplierId = supplierId, CashAccountId = cashAccountId, PaidOn = paidOn,
            Note = Guard.AgainstTooLong(note, nameof(Note), TextMaxLength),
            ReceiptReference = Guard.AgainstTooLong(receiptReference, nameof(ReceiptReference), ReferenceMaxLength),
            RecordedByUserId = actorId, OperationId = operationId,
            PayloadFingerprint = Guard.AgainstNullOrWhiteSpace(fingerprint, nameof(PayloadFingerprint), FingerprintMaxLength)
        };
    }

    public SupplierPaymentLine AddLine(SupplierPaymentLineKind kind, decimal originalAmount, MoneyCurrency currency,
        decimal agreedUsdValue)
    {
        var line = SupplierPaymentLine.Create(Id, kind, originalAmount, currency, agreedUsdValue);
        _lines.Add(line);
        return line;
    }
}
