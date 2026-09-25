using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.Auth.Security;

public static class AuthCookieNames
{
    public const string DeviceId = "blueverse_device_id";
    public const string DeviceKey = "blueverse_device_key";
    public const string ActiveAccountId = "blueverse_active_account_id";
    public const string LegacyAccessToken = "blueverse_access_token";
    public const string LegacyRefreshToken = "blueverse_refresh_token";
    public const string AccountAccessTokenPrefix = "blueverse_access_token_";
    public const string AccountRefreshTokenPrefix = "blueverse_refresh_token_";

    public static string AccessTokenFor(Guid userId) =>
        $"{AccountAccessTokenPrefix}{userId:N}";

    public static string RefreshTokenFor(Guid userId) =>
        $"{AccountRefreshTokenPrefix}{userId:N}";

    public static Guid? ReadUserId(string? token)
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
