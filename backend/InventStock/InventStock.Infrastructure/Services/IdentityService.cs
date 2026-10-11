using InventStock.Application.Common;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Auth;
using InventStock.Application.Interfaces;
using InventStock.Infrastructure.Data;
using InventStock.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventStock.Infrastructure.Services;

public sealed class IdentityService : IIdentityService
{
    private const string InvalidCredentials = "Email or password is incorrect.";
    private const string LockedOut = "This account is temporarily locked after repeated failed sign-in attempts.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ITokenService tokens,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<UserDto> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        if (!RoleNames.IsValid(request.Role))
        {
            throw new ConflictException($"Role '{request.Role}' does not exist.");
        }

        var email = request.Email.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException($"An account already exists for '{email}'.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            EmailConfirmed = true
        };

        var created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new ConflictException(Describe(created));
        }

        var assigned = await _userManager.AddToRoleAsync(user, request.Role);
        if (!assigned.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            throw new ConflictException(Describe(assigned));
        }

        _logger.LogInformation("Created user {UserId} with role {Role}.", user.Id, request.Role);

        return await MapAsync(user);
    }

    public async Task<AuthResultWithRefreshToken> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());

        // The same message is returned whether the account is missing, disabled or the password is
        // wrong, so the endpoint cannot be used to enumerate valid accounts.
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new AuthenticationFailedException(LockedOut);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);

            throw await _userManager.IsLockedOutAsync(user)
                ? new AuthenticationFailedException(LockedOut)
                : new AuthenticationFailedException(InvalidCredentials);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        return await IssueTokensAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthResultWithRefreshToken> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthenticationFailedException("A refresh token is required.");
        }

        var hash = _tokens.Hash(refreshToken);

        var stored = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        if (stored.IsRevoked)
        {
            // A revoked token being presented again means it was captured; drop the whole family.
            _logger.LogWarning("Refresh token reuse detected for user {UserId}.", stored.UserId);
            await RevokeAllForUserAsync(stored.UserId, ipAddress, cancellationToken);
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        if (!stored.IsActive || !stored.User.IsActive)
        {
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        var issued = await IssueTokensAsync(stored.User, ipAddress, cancellationToken, stored);
        return issued;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = _tokens.Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (stored is null || stored.IsRevoked)
        {
            return;
        }

        stored.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty) return;
        var now = DateTime.UtcNow;
        var active = await _db.RefreshTokens
            .Where(x => x.SessionId == sessionId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active) token.RevokedAtUtc = now;
        if (active.Count > 0) await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User", userId);

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ConflictException(Describe(result));
        }

        // Force every existing session to re-authenticate with the new password.
        await RevokeAllForUserAsync(userId, null, cancellationToken);
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(await MapAsync(user));
        }

        return result;
    }

    public async Task<UserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User", userId);

        return await MapAsync(user);
    }

    public async Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User", userId);

        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ConflictException(Describe(result));
        }

        if (!isActive)
        {
            await RevokeAllForUserAsync(userId, null, cancellationToken);
        }
    }

    private async Task<AuthResultWithRefreshToken> IssueTokensAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken,
        RefreshToken? rotatedFrom = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, accessExpiresAt) = _tokens.CreateAccessToken(user, roles);
        var (refreshToken, refreshHash, refreshExpiresAt) = _tokens.CreateRefreshToken();

        if (rotatedFrom is not null)
        {
            rotatedFrom.RevokedAtUtc = DateTime.UtcNow;
            rotatedFrom.RevokedByIp = ipAddress;
            rotatedFrom.ReplacedByTokenHash = refreshHash;
        }

        var sessionId = rotatedFrom?.SessionId ?? Guid.NewGuid();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            SessionId = sessionId,
            TokenHash = refreshHash,
            ExpiresAtUtc = refreshExpiresAt,
            CreatedByIp = ipAddress
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResultWithRefreshToken
        {
            Result = new AuthResult
            {
                AccessToken = accessToken,
                AccessTokenExpiresAtUtc = accessExpiresAt,
                SessionId = sessionId,
                User = Map(user, roles)
            },
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshExpiresAt
        };
    }

    private async Task RevokeAllForUserAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var active = await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.RevokedAtUtc = now;
            token.RevokedByIp = ipAddress;
        }

        if (active.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<UserDto> MapAsync(ApplicationUser user) => Map(user, await _userManager.GetRolesAsync(user));

    private static UserDto Map(ApplicationUser user, IEnumerable<string> roles) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FullName = user.FullName,
        IsActive = user.IsActive,
        Roles = roles.ToList()
    };

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
