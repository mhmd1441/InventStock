using InventStock.Domain.Common;

namespace InventStock.Domain.Entities;

public sealed class ProductBrand : AuditableEntity
{
    public static readonly Guid UnknownId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public const int NameMaxLength = 80;
    public const int AliasMaxLength = 80;

    private ProductBrand() { }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public bool IsActive { get; private set; } = true;

    public ICollection<BrandAlias> Aliases { get; private set; } = new List<BrandAlias>();

    public ICollection<Part> Parts { get; private set; } = new List<Part>();

    public static ProductBrand Create(string name)
    {
        var display = NormalizeSpacing(Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength));
        return new ProductBrand { Name = display, NormalizedName = Normalize(display) };
    }

    public void Rename(string name)
    {
        var display = NormalizeSpacing(Guard.AgainstNullOrWhiteSpace(name, nameof(Name), NameMaxLength));
        Name = display;
        NormalizedName = Normalize(display);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public static string Normalize(string value) => NormalizeSpacing(
        Guard.AgainstNullOrWhiteSpace(value, nameof(value), NameMaxLength)).ToUpperInvariant();

    public static string CandidateKey(string value) =>
        string.Concat(Normalize(value).Where(char.IsLetterOrDigit));

    private static string NormalizeSpacing(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public sealed class BrandAlias : AuditableEntity
{
    private BrandAlias() { }

    public Guid ProductBrandId { get; private set; }

    public ProductBrand ProductBrand { get; private set; } = null!;

    public string Value { get; private set; } = null!;

    public string NormalizedValue { get; private set; } = null!;

    public static BrandAlias Create(Guid productBrandId, string value)
    {
        var display = string.Join(' ', Guard.AgainstNullOrWhiteSpace(value, nameof(Value), ProductBrand.AliasMaxLength)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return new BrandAlias
        {
            ProductBrandId = productBrandId,
            Value = display,
            NormalizedValue = ProductBrand.Normalize(display)
        };
    }
}
