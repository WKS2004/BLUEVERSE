using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.Auth.Dtos;
using Blueverse.Auth.Security;
using Blueverse.Auth.Services;

namespace Blueverse.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // Public gateway contract: POST /api/auth/login, POST /api/auth/refresh,
    // GET /api/auth/me, GET /api/auth/sessions, POST /api/auth/logout and
    // POST /api/auth/logout-all-devices.
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
        if (dto.UseCookies && dto.AccountId is Guid accountId)
        {
            return await ActivateCookieAccountAsync(dto, accountId);
        }

        if (dto.UseCookies || string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            dto.UseCookies = true;
            dto.RefreshToken = GetSelectedRefreshToken();
            dto.DeviceId ??= Request.Cookies[AuthCookieNames.DeviceId];
            dto.DeviceKey ??= Request.Cookies[AuthCookieNames.DeviceKey];
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
            ClearAccountAuthCookies(userId, clearInstallationWhenNoOtherAccounts: true);
        }

        return loggedOut ? NoContent() : Unauthorized();
    }

    [Authorize]
    [HttpPost("logout/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAccount(Guid id)
    {
        if (!TryGetCurrentSession(out var actorUserId, out var sessionId))
        {
            return Unauthorized();
        }

        if (id != actorUserId)
        {
            return Forbid();
        }

        var loggedOut = await _authService.LogoutAccountOnCurrentDeviceAsync(actorUserId, sessionId, id);
        if (loggedOut)
        {
            ClearAccountAuthCookies(id, clearInstallationWhenNoOtherAccounts: id == actorUserId);
        }

        return loggedOut ? NoContent() : Unauthorized();
    }

    [Authorize]
    [HttpPost("logout-account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutSelectedAccount([FromBody] LogoutAccountRequestDto dto)
    {
        if (dto.UserId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Account",
                Detail = "Select an account to remove from this browser.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!TryGetCurrentSession(out var actorUserId, out var sessionId))
        {
            return Unauthorized();
        }

        if (dto.UserId != actorUserId)
        {
            return Forbid();
        }

        var loggedOut = await _authService.LogoutAccountOnCurrentDeviceAsync(actorUserId, sessionId, dto.UserId);
        if (loggedOut)
        {
            ClearAccountAuthCookies(dto.UserId, clearInstallationWhenNoOtherAccounts: dto.UserId == actorUserId);
        }

        return loggedOut ? NoContent() : Unauthorized();
    }

    [Authorize]
    [HttpPost("logout-all-devices")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAllDevices([FromBody] PasswordVerificationDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var loggedOut = await _authService.LogoutAllDevicesAsync(userId, dto.CurrentPassword);
            if (loggedOut)
            {
                ClearAccountAuthCookies(userId, clearInstallationWhenNoOtherAccounts: true);
            }

            return loggedOut ? NoContent() : Unauthorized();
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password Verification Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
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
    [HttpDelete("/api/auth/sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeSession(Guid sessionId, [FromBody] PasswordVerificationDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var hasCurrentSession = TryGetCurrentSessionId(out var currentSessionId);
        bool revoked;
        try
        {
            revoked = await _authService.RevokeSessionAsync(
                userId,
                sessionId,
                hasCurrentSession ? currentSessionId : null,
                dto.CurrentPassword);
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password Verification Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!revoked)
        {
            return NotFound();
        }

        if (hasCurrentSession && currentSessionId == sessionId)
        {
            ClearAccountAuthCookies(userId, clearInstallationWhenNoOtherAccounts: true);
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
                ClearAccountAuthCookies(userId, clearInstallationWhenNoOtherAccounts: true);
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

            ClearAccountAuthCookies(userId, clearInstallationWhenNoOtherAccounts: true);
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

    private async Task<IActionResult> ActivateCookieAccountAsync(RefreshTokenRequestDto dto, Guid accountId)
    {
        dto.UseCookies = true;
        var accountAccessToken = Request.Cookies[AuthCookieNames.AccessTokenFor(accountId)];
        var legacyAccessToken = Request.Cookies[AuthCookieNames.LegacyAccessToken];
        var legacyMatches = AuthCookieNames.ReadUserId(legacyAccessToken) == accountId;

        if (string.IsNullOrWhiteSpace(accountAccessToken) && legacyMatches)
        {
            accountAccessToken = legacyAccessToken;
        }

        if (!string.IsNullOrWhiteSpace(accountAccessToken))
        {
            if (AuthCookieNames.ReadUserId(accountAccessToken) != accountId)
            {
                return Unauthorized();
            }

            if (!string.IsNullOrWhiteSpace(Request.Cookies[AuthCookieNames.AccessTokenFor(accountId)]))
            {
                AppendActiveAccountCookie(accountId);
                return NoContent();
            }
        }

        var accountRefreshToken = Request.Cookies[AuthCookieNames.RefreshTokenFor(accountId)];
        if (string.IsNullOrWhiteSpace(accountRefreshToken) && legacyMatches)
        {
            accountRefreshToken = Request.Cookies[AuthCookieNames.LegacyRefreshToken];
        }

        if (string.IsNullOrWhiteSpace(accountRefreshToken))
        {
            return Unauthorized();
        }

        dto.RefreshToken = accountRefreshToken;
        dto.DeviceId = Request.Cookies[AuthCookieNames.DeviceId];
        dto.DeviceKey = Request.Cookies[AuthCookieNames.DeviceKey];

        try
        {
            var response = await _authService.RefreshAsync(dto);
            return response.User.Id == accountId
                ? AuthResponse(response, StatusCodes.Status200OK, useCookies: true)
                : Unauthorized();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
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
        dto.DeviceId ??= Request.Cookies[AuthCookieNames.DeviceId];
        dto.DeviceKey ??= Request.Cookies[AuthCookieNames.DeviceKey];
    }

    private void ApplyDeviceCookies(RegisterRequestDto dto)
    {
        dto.DeviceId ??= Request.Cookies[AuthCookieNames.DeviceId];
        dto.DeviceKey ??= Request.Cookies[AuthCookieNames.DeviceKey];
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

        Response.Cookies.Append(AuthCookieNames.DeviceId, response.DeviceId, sessionCookieOptions);
        if (!string.IsNullOrWhiteSpace(response.DeviceKey))
        {
            Response.Cookies.Append(AuthCookieNames.DeviceKey, response.DeviceKey, sessionCookieOptions);
        }
    }

    private void AppendAuthCookies(AuthResponseDto response)
    {
        var userId = response.User.Id;
        var accessCookieOptions = InstallationCookieOptions(response);
        accessCookieOptions.Expires = response.ExpiresAt;
        Response.Cookies.Append(AuthCookieNames.AccessTokenFor(userId), response.Token!, accessCookieOptions);

        var sessionCookieOptions = InstallationCookieOptions(response);
        if (!string.IsNullOrWhiteSpace(response.RefreshToken))
        {
            Response.Cookies.Append(AuthCookieNames.RefreshTokenFor(userId), response.RefreshToken, sessionCookieOptions);
        }

        // Keep the original cookie names for older browser clients. The active
        // account selector makes the account-scoped cookies authoritative.
        Response.Cookies.Append(AuthCookieNames.LegacyAccessToken, response.Token!, accessCookieOptions);
        if (!string.IsNullOrWhiteSpace(response.RefreshToken))
        {
            Response.Cookies.Append(AuthCookieNames.LegacyRefreshToken, response.RefreshToken, sessionCookieOptions);
        }
        Response.Cookies.Append(AuthCookieNames.ActiveAccountId, userId.ToString(), sessionCookieOptions);
    }

    private string? GetSelectedRefreshToken()
    {
        if (Guid.TryParse(Request.Cookies[AuthCookieNames.ActiveAccountId], out var selectedAccount))
        {
            var selectedToken = Request.Cookies[AuthCookieNames.RefreshTokenFor(selectedAccount)];
            if (!string.IsNullOrWhiteSpace(selectedToken))
            {
                return selectedToken;
            }

            if (AuthCookieNames.ReadUserId(Request.Cookies[AuthCookieNames.LegacyAccessToken]) == selectedAccount)
            {
                return Request.Cookies[AuthCookieNames.LegacyRefreshToken];
            }

            // A missing refresh token for the selected account must never fall
            // through to another account's legacy token.
            return null;
        }

        return Request.Cookies[AuthCookieNames.LegacyRefreshToken];
    }

    private void AppendActiveAccountCookie(Guid userId)
    {
        Response.Cookies.Append(
            AuthCookieNames.ActiveAccountId,
            userId.ToString(),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps || (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing")),
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
    }

    private void ClearAccountAuthCookies(Guid userId, bool clearInstallationWhenNoOtherAccounts)
    {
        var cookieOptions = ExpiredCookieOptions();
        Response.Cookies.Delete(AuthCookieNames.AccessTokenFor(userId), cookieOptions);
        Response.Cookies.Delete(AuthCookieNames.RefreshTokenFor(userId), cookieOptions);

        if (AuthCookieNames.ReadUserId(Request.Cookies[AuthCookieNames.LegacyAccessToken]) == userId)
        {
            Response.Cookies.Delete(AuthCookieNames.LegacyAccessToken, cookieOptions);
            Response.Cookies.Delete(AuthCookieNames.LegacyRefreshToken, cookieOptions);
        }

        if (Guid.TryParse(Request.Cookies[AuthCookieNames.ActiveAccountId], out var activeAccount) && activeAccount == userId)
        {
            Response.Cookies.Delete(AuthCookieNames.ActiveAccountId, cookieOptions);
        }

        if (clearInstallationWhenNoOtherAccounts && !HasOtherAccountCookies(userId))
        {
            ClearInstallationCookies(cookieOptions);
        }
    }

    private bool HasOtherAccountCookies(Guid excludedUserId)
    {
        var accessPrefix = AuthCookieNames.AccountAccessTokenPrefix;
        var refreshPrefix = AuthCookieNames.AccountRefreshTokenPrefix;
        return Request.Cookies.Keys.Any(name =>
            (name.StartsWith(accessPrefix, StringComparison.Ordinal) &&
             !string.Equals(name, AuthCookieNames.AccessTokenFor(excludedUserId), StringComparison.Ordinal)) ||
            (name.StartsWith(refreshPrefix, StringComparison.Ordinal) &&
             !string.Equals(name, AuthCookieNames.RefreshTokenFor(excludedUserId), StringComparison.Ordinal)));
    }

    private void ClearAllDeviceCookies()
    {
        var cookieOptions = ExpiredCookieOptions();
        ClearInstallationCookies(cookieOptions);
        Response.Cookies.Delete(AuthCookieNames.ActiveAccountId, cookieOptions);
        Response.Cookies.Delete(AuthCookieNames.LegacyAccessToken, cookieOptions);
        Response.Cookies.Delete(AuthCookieNames.LegacyRefreshToken, cookieOptions);

        foreach (var name in Request.Cookies.Keys.Where(name =>
            name.StartsWith(AuthCookieNames.AccountAccessTokenPrefix, StringComparison.Ordinal) ||
            name.StartsWith(AuthCookieNames.AccountRefreshTokenPrefix, StringComparison.Ordinal)))
        {
            Response.Cookies.Delete(name, cookieOptions);
        }
    }

    private void ClearInstallationCookies(CookieOptions cookieOptions)
    {
        Response.Cookies.Delete(AuthCookieNames.DeviceId, cookieOptions);
        Response.Cookies.Delete(AuthCookieNames.DeviceKey, cookieOptions);
    }

    private CookieOptions ExpiredCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps || (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing")),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = DateTimeOffset.UnixEpoch
    };

}
