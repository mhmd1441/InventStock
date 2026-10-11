using System.Security.Claims;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Auth;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string RefreshCookieName = "inventstock_refresh";

    private readonly IIdentityService _identity;

    public AuthController(IIdentityService identity)
    {
        _identity = identity;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var issued = await _identity.LoginAsync(request, ClientIp(), cancellationToken);
        SetRefreshCookie(issued);
        return Ok(issued.Result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> Refresh(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new AuthenticationFailedException("No refresh token was supplied.");
        }

        var issued = await _identity.RefreshAsync(token, ClientIp(), cancellationToken);
        SetRefreshCookie(issued);
        return Ok(issued.Result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest? request, CancellationToken cancellationToken)
    {
        if (request?.SessionId is Guid sessionId)
        {
            await _identity.RevokeSessionAsync(sessionId, cancellationToken);
        }
        else
        {
        var token = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(token))
        {
            await _identity.RevokeRefreshTokenAsync(token, cancellationToken);
        }
        }

        Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(DateTime.UnixEpoch));
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _identity.GetUserAsync(userId, cancellationToken));
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _identity.ChangePasswordAsync(userId, request, cancellationToken);

        Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(DateTime.UnixEpoch));
        return NoContent();
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private void SetRefreshCookie(AuthResultWithRefreshToken issued) =>
        Response.Cookies.Append(
            RefreshCookieName,
            issued.RefreshToken,
            BuildCookieOptions(issued.RefreshTokenExpiresAtUtc));

    /// <summary>
    /// HttpOnly keeps the refresh token out of reach of any script on the page, and the browser
    /// only returns it to the refresh endpoint itself.
    /// </summary>
    private static CookieOptions BuildCookieOptions(DateTime expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Expires = expiresAt,
        Path = "/api/auth",
        IsEssential = true
    };
}
