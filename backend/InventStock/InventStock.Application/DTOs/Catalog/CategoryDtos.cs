namespace InventStock.Application.DTOs.Catalog;

public sealed record CategoryDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int PartCount { get; init; }
}

public sealed record SaveCategoryRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}
