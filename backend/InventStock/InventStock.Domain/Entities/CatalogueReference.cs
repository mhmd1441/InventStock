using InventStock.Domain.Common;
using InventStock.Domain.Exceptions;
using System.Text.Json.Serialization;

namespace InventStock.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReferenceKind { OE, MercedesOE, WVA, Manufacturer, Other }

public sealed class CatalogueReference : AuditableEntity
{
    public ReferenceKind Kind { get; private set; }
    public string Issuer { get; private set; } = string.Empty;
    public string Value { get; private set; } = null!;
    public string IdentityKey { get; private set; } = null!;
    public string SearchValue { get; private set; } = null!;
    public string MatchValue { get; private set; } = null!;

    private CatalogueReference() { }

    public static CatalogueReference Create(ReferenceKind kind, string? issuer, string value)
    {
        if (!Enum.IsDefined(kind)) throw new DomainException("Unknown reference type.");
        var raw = Guard.AgainstNullOrWhiteSpace(value, nameof(Value), 64).Trim();
        var normalized = raw.Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (normalized.Length == 0 || normalized.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '/'))
            throw new DomainException("Reference numbers may contain letters, digits, spaces and . _ / - only.");
        var authority = Guard.AgainstTooLong(issuer?.Trim().ToUpperInvariant(), nameof(Issuer), 80) ?? "";
        var match = normalized;
        if (kind == ReferenceKind.MercedesOE)
        {
            match = normalized.StartsWith('A') ? normalized[1..] : normalized;
            if (match.Length != 10 || match.Any(c => c is < '0' or > '9'))
                throw new DomainException("Mercedes OE references require ten digits with an optional A prefix.");
            normalized = "A" + match;
            authority = "MERCEDES";
        }
        return new CatalogueReference { Kind = kind, Issuer = authority, Value = raw,
            IdentityKey = kind == ReferenceKind.MercedesOE ? normalized : raw.ToUpperInvariant(),
            SearchValue = normalized, MatchValue = match };
    }
}

public sealed class SkuReference
{
    public bool IsActive { get; set; } = true;
    public int Revision { get; set; }
    public Guid PartId { get; set; }
    public Part Part { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public CatalogueReference Reference { get; set; } = null!;
    public DateTime LinkedAtUtc { get; set; }
    public Guid? LinkedByUserId { get; set; }
}

public sealed class ReferenceLinkEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartId { get; set; }
    public Guid ReferenceId { get; set; }
    public int Revision { get; set; }
    public bool IsActive { get; set; }
    public string Reason { get; set; } = null!;
    public Guid ActorId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
