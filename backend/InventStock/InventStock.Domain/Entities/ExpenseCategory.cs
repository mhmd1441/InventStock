using InventStock.Domain.Common;

namespace InventStock.Domain.Entities;

public sealed class ExpenseCategory : AuditableEntity
{
    public const int NameMaxLength = 100;
    private ExpenseCategory() { }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public static ExpenseCategory Create(string name) => new()
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength),
        NormalizedName = Normalize(name)
    };

    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength);
        NormalizedName = Normalize(name);
    }
    public void SetActive(bool isActive) => IsActive = isActive;
    private static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
