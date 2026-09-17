using InventStock.Application.Common;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Auth;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class UsersController : ControllerBase
{
    private readonly IIdentityService _identity;
    private readonly ICurrentUserService _currentUser;

    public UsersController(IIdentityService identity, ICurrentUserService currentUser)
    {
        _identity = identity;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _identity.ListUsersAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _identity.GetUserAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _identity.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpPatch("{id:guid}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        if (!isActive && _currentUser.UserId == id)
        {
            throw new ConflictException("You cannot deactivate your own account.");
        }

        await _identity.SetUserActiveAsync(id, isActive, cancellationToken);
        return NoContent();
    }
}
