using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Blueverse.Auth.Authorization;
using Blueverse.Auth.Dtos;
using Blueverse.Auth.Services;

namespace Blueverse.Auth.Controllers;

[ApiController]
[Route("api/auth/users")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.UserRead)]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.UserRead)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _authService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "User Not Found",
                Detail = $"User with ID '{id}' was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(user);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.UserRead)]
    [HasPermission(PermissionCodes.UserCreate)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserDto dto)
    {
        var actorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(actorIdClaim, out var actorUserId))
        {
            return Unauthorized();
        }

        try
        {
            var user = await _authService.AdminCreateUserAsync(actorUserId, dto);
            return StatusCode(StatusCodes.Status201Created, user);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User Creation Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.UserRead)]
    [HasPermission(PermissionCodes.UserUpdate)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] AdminUpdateUserDto dto)
    {
        var callerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(callerIdClaim, out var callerAdminId))
        {
            return Unauthorized();
        }

        try
        {
            var user = await _authService.AdminUpdateUserAsync(id, callerAdminId, dto);
            if (user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User Not Found",
                    Detail = $"User with ID '{id}' was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User Update Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.UserRead)]
    [HasPermission(PermissionCodes.UserDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var callerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(callerIdClaim, out var callerAdminId))
        {
            return Unauthorized();
        }

        try
        {
            var deleted = await _authService.AdminDeleteUserAsync(id, callerAdminId);
            if (!deleted)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User Not Found",
                    Detail = $"User with ID '{id}' was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User Deletion Prohibited",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPost("{id:guid}/roles")]
    [HasPermission(PermissionCodes.UserRead)]
    [HasPermission(PermissionCodes.UserUpdate)]
    [HasPermission(PermissionCodes.RoleRead)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesDto dto)
    {
        var actorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(actorIdClaim, out var actorUserId))
        {
            return Unauthorized();
        }

        try
        {
            var user = await _authService.AssignRolesToUserAsync(actorUserId, id, dto);
            if (user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User Not Found",
                    Detail = $"User with ID '{id}' was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(user);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role Assignment Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
