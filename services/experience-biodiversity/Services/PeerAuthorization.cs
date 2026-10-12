using System.Net.Http.Headers;

namespace Blueverse.ExperienceBiodiversity.Services;

internal static class PeerAuthorization
{
    public static string? GetBearerToken(IHttpContextAccessor? accessor)
    {
        var context = accessor?.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true) return null;

        if (AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization) &&
            string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(authorization.Parameter))
            return authorization.Parameter;

        return context.Request.Cookies.TryGetValue("blueverse_access_token", out var cookie) &&
               !string.IsNullOrWhiteSpace(cookie)
            ? cookie
            : null;
    }
}
