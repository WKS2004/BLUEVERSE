using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Blueverse.Auth.Authorization;
using Blueverse.Auth.Dtos;
using Blueverse.Auth.Services;

namespace Blueverse.Auth.Controllers;

[ApiController]
[Route("api/auth/roles")]
public class RolesController : ControllerBase
{
    private readonly IAuthService _authService;

    public RolesController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    [HasPermission("auth.role.read")]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _authService.GetAllRolesAsync();
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("auth.role.read")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoleById(Guid id)
    {
        var role = await _authService.GetRoleByIdAsync(id);
        if (role == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Role Not Found",
                Detail = $"Role with ID '{id}' was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(role);
    }

    [HttpPost]
    [HasPermission("auth.role.manage")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto dto)
    {
        try
        {
            var role = await _authService.CreateRoleAsync(dto);
            return StatusCode(StatusCodes.Status201Created, role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role Creation Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission("auth.role.manage")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleDto dto)
    {
        try
        {
            var role = await _authService.UpdateRoleAsync(id, dto);
            if (role == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Role Not Found",
                    Detail = $"Role with ID '{id}' was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role Update Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("auth.role.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        try
        {
            var deleted = await _authService.DeleteRoleAsync(id);
            if (!deleted)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Role Not Found",
                    Detail = $"Role with ID '{id}' was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role Deletion Prohibited",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPost("{id:guid}/permissions")]
    [HasPermission("auth.role.manage")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignPermissions(Guid id, [FromBody] AssignPermissionsDto dto)
    {
        var actorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(actorIdClaim, out var actorUserId))
        {
            return Unauthorized();
        }

        try
        {
            var role = await _authService.AssignPermissionsToRoleAsync(actorUserId, id, dto);
            if (role == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Role Not Found",
                    Detail = $"Role with ID '{id}' was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(role);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Permission Assignment Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
