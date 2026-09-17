namespace InventStock.Application.DTOs.Auth;

public sealed record LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed record RegisterUserRequest
{
    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}

public sealed record ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;
}

public sealed record UserDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// The refresh token is intentionally absent: it is delivered to the browser as an
/// HttpOnly cookie so client-side script can never read it.
/// </summary>
public sealed record AuthResult
{
    public string AccessToken { get; init; } = string.Empty;

    public DateTime AccessTokenExpiresAtUtc { get; init; }

    public UserDto User { get; init; } = new();
}

public sealed record AuthResultWithRefreshToken
{
    public AuthResult Result { get; init; } = new();

    public string RefreshToken { get; init; } = string.Empty;

    public DateTime RefreshTokenExpiresAtUtc { get; init; }
}
