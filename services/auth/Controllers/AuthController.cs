using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.Auth.Dtos;
using Blueverse.Auth.Services;

namespace Blueverse.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // Public gateway contract: POST /api/auth/login, POST /api/auth/refresh,
    // GET /api/auth/me, GET /api/auth/sessions, POST /api/auth/logout and
    // POST /api/auth/logout-all-devices.
    private const string DeviceIdCookieName = "blueverse_device_id";
    private const string DeviceKeyCookieName = "blueverse_device_key";
    private const string AccessTokenCookieName = "blueverse_access_token";
    private const string RefreshTokenCookieName = "blueverse_refresh_token";

    private readonly IAuthService _authService;
    private readonly IHostEnvironment _environment;

    public AuthController(IAuthService authService, IHostEnvironment environment)
    {
        _authService = authService;
        _environment = environment;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        ApplyDeviceCookies(dto);

        try
        {
            var response = await _authService.RegisterAsync(dto);
            return AuthResponse(response, StatusCodes.Status201Created, dto.UseCookies);
        }
        catch (DeviceAccountLimitExceededException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Device Account Limit Reached",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Registration Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        ApplyDeviceCookies(dto);

        try
        {
            var response = await _authService.LoginAsync(dto);
            return AuthResponse(response, StatusCodes.Status200OK, dto.UseCookies);
        }
        catch (DeviceAccountLimitExceededException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Device Account Limit Reached",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status401Unauthorized
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
    {
        if (dto.UseCookies || string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            dto.UseCookies = true;
            dto.RefreshToken = Request.Cookies[RefreshTokenCookieName];
            dto.DeviceId ??= Request.Cookies[DeviceIdCookieName];
            dto.DeviceKey ??= Request.Cookies[DeviceKeyCookieName];
        }

        try
        {
            var response = await _authService.RefreshAsync(dto);
            return AuthResponse(response, StatusCodes.Status200OK, dto.UseCookies);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Refresh Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status401Unauthorized
            });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        if (!TryGetCurrentSession(out var userId, out var sessionId))
        {
            return Unauthorized();
        }

        var loggedOut = await _authService.LogoutCurrentDeviceAsync(userId, sessionId);
        if (loggedOut)
        {
            ClearAuthCookies();
        }

        return loggedOut ? NoContent() : Unauthorized();
    }

    [Authorize]
    [HttpPost("logout/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAccount(Guid id)
    {
        if (!TryGetCurrentSession(out var actorUserId, out var sessionId))
        {
            return Unauthorized();
        }

        var loggedOut = await _authService.LogoutAccountOnCurrentDeviceAsync(actorUserId, sessionId, id);
        if (loggedOut && id == actorUserId)
        {
            ClearAuthCookies();
        }

        return loggedOut ? NoContent() : Unauthorized();
    }

    [Authorize]
    [HttpPost("logout-all-devices")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAllDevices()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var loggedOut = await _authService.LogoutAllDevicesAsync(userId);
        if (loggedOut)
        {
            ClearAuthCookies();
        }

        return loggedOut ? NoContent() : Unauthorized();
    }

    [Authorize]
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSessions()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        Guid? currentSessionId = TryGetCurrentSessionId(out var sessionId) ? sessionId : null;
        return Ok(await _authService.GetSessionsAsync(userId, currentSessionId));
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var revoked = await _authService.RevokeSessionAsync(userId, sessionId);
        if (!revoked)
        {
            return NotFound();
        }

        if (TryGetCurrentSessionId(out var currentSessionId) && currentSessionId == sessionId)
        {
            ClearAuthCookies();
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var changed = await _authService.ChangePasswordAsync(userId, dto);
            if (changed)
            {
                ClearAuthCookies();
            }

            return changed ? NoContent() : Unauthorized();
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password Change Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileDto dto)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.UpdateProfileAsync(userId, dto);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [Authorize]
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var deleted = await _authService.DeleteSelfAsync(userId);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Account Deletion Prohibited",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out userId);
    }

    private bool TryGetCurrentSession(out Guid userId, out Guid sessionId)
    {
        if (!TryGetCurrentUserId(out userId))
        {
            sessionId = Guid.Empty;
            return false;
        }

        return TryGetCurrentSessionId(out sessionId);
    }

    private bool TryGetCurrentSessionId(out Guid sessionId)
    {
        return Guid.TryParse(User.FindFirstValue("session_id"), out sessionId);
    }

    private IActionResult AuthResponse(AuthResponseDto response, int statusCode, bool useCookies)
    {
        AppendInstallationCookies(response);

        if (!useCookies)
        {
            return StatusCode(statusCode, response);
        }

        AppendAuthCookies(response);
        response.Token = null;
        response.DeviceKey = null;
        response.RefreshToken = null;
        return StatusCode(statusCode, response);
    }

    private void ApplyDeviceCookies(LoginRequestDto dto)
    {
        dto.DeviceId ??= Request.Cookies[DeviceIdCookieName];
        dto.DeviceKey ??= Request.Cookies[DeviceKeyCookieName];
    }

    private void ApplyDeviceCookies(RegisterRequestDto dto)
    {
        dto.DeviceId ??= Request.Cookies[DeviceIdCookieName];
        dto.DeviceKey ??= Request.Cookies[DeviceKeyCookieName];
    }

    private CookieOptions InstallationCookieOptions(AuthResponseDto response)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing")),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = response.SessionExpiresAt
        };
    }

    private void AppendInstallationCookies(AuthResponseDto response)
    {
        var sessionCookieOptions = InstallationCookieOptions(response);

        Response.Cookies.Append(DeviceIdCookieName, response.DeviceId, sessionCookieOptions);
        if (!string.IsNullOrWhiteSpace(response.DeviceKey))
        {
            Response.Cookies.Append(DeviceKeyCookieName, response.DeviceKey, sessionCookieOptions);
        }
    }

    private void AppendAuthCookies(AuthResponseDto response)
    {
        var accessCookieOptions = InstallationCookieOptions(response);
        accessCookieOptions.Expires = response.ExpiresAt;
        Response.Cookies.Append(AccessTokenCookieName, response.Token!, accessCookieOptions);

        var sessionCookieOptions = InstallationCookieOptions(response);
        if (!string.IsNullOrWhiteSpace(response.RefreshToken))
        {
            Response.Cookies.Append(RefreshTokenCookieName, response.RefreshToken, sessionCookieOptions);
        }
    }

    private void ClearAuthCookies()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing")),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch
        };

        Response.Cookies.Delete(DeviceIdCookieName, cookieOptions);
        Response.Cookies.Delete(DeviceKeyCookieName, cookieOptions);
        Response.Cookies.Delete(AccessTokenCookieName, cookieOptions);
        Response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
    }
}
