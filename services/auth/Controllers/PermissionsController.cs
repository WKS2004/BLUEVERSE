using Microsoft.AspNetCore.Mvc;
using Blueverse.Auth.Authorization;
using Blueverse.Auth.Dtos;
using Blueverse.Auth.Services;

namespace Blueverse.Auth.Controllers;

[ApiController]
[Route("api/auth/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly IAuthService _authService;

    public PermissionsController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    [HasPermission("auth.permission.read")]
    [ProducesResponseType(typeof(List<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _authService.GetAllPermissionsAsync();
        return Ok(permissions);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("auth.permission.read")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissionById(Guid id)
    {
        var permission = await _authService.GetPermissionByIdAsync(id);
        if (permission == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Permission Not Found",
                Detail = $"Permission with ID '{id}' was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(permission);
    }
}
