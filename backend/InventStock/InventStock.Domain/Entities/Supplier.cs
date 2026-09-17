using InventStock.Domain.Common;

namespace InventStock.Domain.Entities;

public class Supplier : AuditableEntity
{
    public const int NameMaxLength = 150;
    public const int ContactNameMaxLength = 150;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 40;
    public const int AddressMaxLength = 300;

    private readonly List<Part> _parts = new();

    private Supplier()
    {
    }

    public string Name { get; private set; } = null!;

    public string? ContactName { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Address { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<Part> Parts => _parts.AsReadOnly();

    public static Supplier Create(string name, string? contactName, string? email, string? phone, string? address)
    {
        return new Supplier
        {
            Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength),
            ContactName = Guard.AgainstTooLong(contactName, nameof(ContactName), ContactNameMaxLength),
            Email = Guard.AgainstTooLong(email, nameof(Email), EmailMaxLength),
            Phone = Guard.AgainstTooLong(phone, nameof(Phone), PhoneMaxLength),
            Address = Guard.AgainstTooLong(address, nameof(Address), AddressMaxLength)
        };
    }

    public void Update(string name, string? contactName, string? email, string? phone, string? address)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength);
        ContactName = Guard.AgainstTooLong(contactName, nameof(ContactName), ContactNameMaxLength);
        Email = Guard.AgainstTooLong(email, nameof(Email), EmailMaxLength);
        Phone = Guard.AgainstTooLong(phone, nameof(Phone), PhoneMaxLength);
        Address = Guard.AgainstTooLong(address, nameof(Address), AddressMaxLength);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
