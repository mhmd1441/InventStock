using InventStock.Domain.Common;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>Settlement and transfer fee are separate immutable money facts.</summary>
public sealed class SupplierPaymentLine : AuditableEntity
{
    private SupplierPaymentLine() { }
    public Guid SupplierPaymentId { get; private set; }
    public SupplierPayment SupplierPayment { get; private set; } = null!;
    public SupplierPaymentLineKind Kind { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public MoneyCurrency Currency { get; private set; }
    public decimal AgreedUsdValue { get; private set; }
    public Guid CashMovementId { get; private set; }

    public static SupplierPaymentLine Create(Guid supplierPaymentId, SupplierPaymentLineKind kind, decimal originalAmount,
        MoneyCurrency currency, decimal agreedUsdValue)
    {
        if (supplierPaymentId == Guid.Empty || originalAmount <= 0 || agreedUsdValue <= 0)
            throw new DomainException("Supplier payment line amounts must be positive.");
        return new SupplierPaymentLine
        {
            SupplierPaymentId = supplierPaymentId, Kind = kind, OriginalAmount = originalAmount,
            Currency = currency, AgreedUsdValue = agreedUsdValue
        };
    }

    public void LinkMovement(Guid movementId) => CashMovementId = movementId;
}
