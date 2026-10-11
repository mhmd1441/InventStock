using InventStock.Application.DTOs.Auth;

namespace InventStock.Application.Interfaces;

public interface IIdentityService
{
    Task<UserDto> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    Task<AuthResultWithRefreshToken> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResultWithRefreshToken> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task<UserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
