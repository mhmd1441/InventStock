namespace InventStock.Application.DTOs.Catalog;

public sealed record SupplierDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? ContactName { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Address { get; init; }

    public bool IsActive { get; init; }

    public int PartCount { get; init; }
}

public sealed record SaveSupplierRequest
{
    public string Name { get; init; } = string.Empty;

    public string? ContactName { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Address { get; init; }
}
