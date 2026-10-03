using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.MarineSafety.Authorization;

/// <summary>
/// Reads the Auth-managed account cookie selected by the browser, while
/// retaining bearer-token and legacy single-account cookie support.
/// </summary>
internal static class MarineSafetyCookieTokenReader
{
    private const string ActiveAccountCookie = "blueverse_active_account_id";
    private const string LegacyAccessTokenCookie = "blueverse_access_token";
    private const string AccountAccessTokenPrefix = "blueverse_access_token_";

    public static Task OnMessageReceived(MessageReceivedContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Token))
        {
            return Task.CompletedTask;
        }

        // JwtBearerHandler invokes this event before it reads the standard
        // Authorization header, so explicitly preserve bearer precedence.
        var authorizationHeader = context.Request.Headers.Authorization.ToString();
        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Token = authorizationHeader["Bearer ".Length..].Trim();
            return Task.CompletedTask;
        }

        var legacyToken = context.Request.Cookies[LegacyAccessTokenCookie];
        if (!context.Request.Cookies.TryGetValue(ActiveAccountCookie, out var selectedAccountCookie))
        {
            // Preserve legacy single-account clients only when no explicit
            // account selection is present.
            context.Token = legacyToken;
            return Task.CompletedTask;
        }

        // A malformed explicit selection must fail closed. Falling back to a
        // legacy token here could authenticate a different account than the
        // browser selected.
        if (!Guid.TryParse(selectedAccountCookie, out var activeAccountId))
        {
            return Task.CompletedTask;
        }

        var accountToken = context.Request.Cookies[$"{AccountAccessTokenPrefix}{activeAccountId:N}"];
        if (!string.IsNullOrWhiteSpace(accountToken))
        {
            context.Token = accountToken;
        }
        else if (ReadSubject(legacyToken) == activeAccountId)
        {
            // A stale legacy cookie for another signed-in account must not
            // override the browser's explicit account selection.
            context.Token = legacyToken;
        }

        return Task.CompletedTask;
    }

    private static Guid? ReadSubject(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var subject = jwt.Subject ?? jwt.Claims
                .FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
