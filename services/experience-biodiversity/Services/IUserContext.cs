using System.Security.Claims;
using System.Text.Json;

namespace Blueverse.ExperienceBiodiversity.Services;

public interface IUserContext
{
    Guid? CurrentUserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    bool HasPermission(string permission);
    bool HasAnyPermission(params string[] permissions);
}

public sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? CurrentUserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst("sub")?.Value;

                if (Guid.TryParse(subClaim, out var parsedId))
                {
                    return parsedId;
                }
            }

            // Also check X-User-Id header forwarded by gateway or used in integration tests
            var headerUserId = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].ToString();
            if (Guid.TryParse(headerUserId, out var parsedHeaderId))
            {
                return parsedHeaderId;
            }

            // Extract sub from Authorization Bearer token forwarded by gateway
            using var payload = GetJwtPayloadJson();
            if (payload != null && payload.RootElement.TryGetProperty("sub", out var subProp))
            {
                if (Guid.TryParse(subProp.GetString(), out var jwtSub))
                {
                    return jwtSub;
                }
            }

            return null;
        }
    }

    public bool IsAuthenticated => CurrentUserId.HasValue;

    public IReadOnlyList<string> Roles
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                return user.FindAll(ClaimTypes.Role)
                    .Concat(user.FindAll("role"))
                    .Select(c => c.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // Also check X-User-Roles header forwarded by gateway or test client
            var headerRoles = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Roles"].ToString();
            if (!string.IsNullOrWhiteSpace(headerRoles))
            {
                return headerRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            using var payload = GetJwtPayloadJson();
            if (payload != null && payload.RootElement.TryGetProperty("role", out var roleProp))
            {
                var roles = new List<string>();
                if (roleProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in roleProp.EnumerateArray())
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) roles.Add(val);
                    }
                }
                else if (roleProp.ValueKind == JsonValueKind.String)
                {
                    var val = roleProp.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) roles.Add(val);
                }
                return roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> Permissions
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                return user.FindAll("permission")
                    .Select(c => c.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // Also check X-User-Permissions header forwarded by gateway or test client
            var headerPerms = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Permissions"].ToString();
            if (!string.IsNullOrWhiteSpace(headerPerms))
            {
                return headerPerms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            using var payload = GetJwtPayloadJson();
            if (payload != null && payload.RootElement.TryGetProperty("permission", out var permProp))
            {
                var perms = new List<string>();
                if (permProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in permProp.EnumerateArray())
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) perms.Add(val);
                    }
                }
                else if (permProp.ValueKind == JsonValueKind.String)
                {
                    var val = permProp.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) perms.Add(val);
                }
                return perms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            return Array.Empty<string>();
        }
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission)) return false;
        var perms = Permissions;
        if (perms.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(p, "experiences.catalogue.manage", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(p, "auth.role.system.manage", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Also check if user has Admin role
        return Roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
    }

    public bool HasAnyPermission(params string[] permissions)
    {
        if (permissions == null || permissions.Length == 0) return false;
        return permissions.Any(HasPermission);
    }

    private JsonDocument? GetJwtPayloadJson()
    {
        string? token = null;
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader.Substring(7).Trim();
        }
        else
        {
            token = _httpContextAccessor.HttpContext?.Request.Cookies["blueverse_access_token"];
        }

        if (string.IsNullOrWhiteSpace(token)) return null;

        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            var base64 = parts[1].Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            var bytes = Convert.FromBase64String(base64);
            return JsonDocument.Parse(bytes);
        }
        catch
        {
            return null;
        }
    }
}
