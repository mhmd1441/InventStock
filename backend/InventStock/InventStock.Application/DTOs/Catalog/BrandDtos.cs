namespace InventStock.Application.DTOs.Catalog;

public sealed record BrandDto(Guid Id, string Name, bool IsActive, IReadOnlyList<string> Aliases);

public sealed record SaveBrandRequest(string Name, bool ConfirmPossibleDuplicate = false);

public sealed record SaveBrandAliasRequest(string Value);
