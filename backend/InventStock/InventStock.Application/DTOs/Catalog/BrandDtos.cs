namespace InventStock.Application.DTOs.Catalog;

public sealed record BrandDto(Guid Id, string Name, bool IsActive, IReadOnlyList<string> Aliases);

public sealed record SaveBrandRequest(string Name, bool ConfirmPossibleDuplicate = false);

public sealed record SaveBrandAliasRequest(string Value);

public sealed record MergeBrandRequest(Guid TargetBrandId, string Reason);

public sealed record BrandMergeDto(Guid Id, Guid SourceBrandId, Guid TargetBrandId, string SourceName,
    string TargetName, string Reason, int ReassignedPartCount, Guid? ActorId, DateTime CreatedAtUtc);
