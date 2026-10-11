namespace InventStock.Infrastructure.Identity;

/// <summary>
/// Only the SHA-256 hash of the token is persisted, so a database leak does not hand out
/// usable refresh tokens. Rotation is tracked via <see cref="ReplacedByTokenHash"/>.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid SessionId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? CreatedByIp { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedByIp { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    public bool IsActive => !IsRevoked && !IsExpired;
}
