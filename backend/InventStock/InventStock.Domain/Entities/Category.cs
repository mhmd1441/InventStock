using InventStock.Domain.Common;

namespace InventStock.Domain.Entities;

public class Category : AuditableEntity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private readonly List<Part> _parts = new();

    private Category()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public IReadOnlyCollection<Part> Parts => _parts.AsReadOnly();

    public static Category Create(string name, string? description)
    {
        return new Category
        {
            Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength),
            Description = Guard.AgainstTooLong(description, nameof(Description), DescriptionMaxLength)
        };
    }

    public void Update(string name, string? description)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength);
        Description = Guard.AgainstTooLong(description, nameof(Description), DescriptionMaxLength);
    }
}
