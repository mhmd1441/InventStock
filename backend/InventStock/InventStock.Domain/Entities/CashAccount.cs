using InventStock.Domain.Common;
using InventStock.Domain.Enums;

namespace InventStock.Domain.Entities;

public sealed class CashAccount : AuditableEntity
{
    public const int CodeMaxLength = 40;
    public const int NameMaxLength = 100;
    private CashAccount() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public MoneyCurrency Currency { get; private set; }
    public CashAccountKind Kind { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static CashAccount Create(string code, string name, MoneyCurrency currency, CashAccountKind kind) => new()
    {
        Code = Guard.AgainstNullOrWhiteSpace(code, nameof(Code), CodeMaxLength).ToUpperInvariant(),
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength),
        Currency = currency,
        Kind = kind
    };
}
