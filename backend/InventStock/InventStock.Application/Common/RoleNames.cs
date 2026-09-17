namespace InventStock.Application.Common;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = new[] { Admin, Staff };

    public static bool IsValid(string? role) =>
        role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);
}
