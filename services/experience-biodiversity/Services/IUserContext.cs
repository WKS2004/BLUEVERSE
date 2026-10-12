using System.Security.Claims;

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
    private const string ExperienceCatalogueRead = "experiences.catalogue.read";
    private const string ExperienceCatalogueManage = "experiences.catalogue.manage";
    private const string SystemRoleManage = "auth.role.system.manage";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? CurrentUserId
    {
        get
        {
            var principal = User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;

            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
    }

    public IReadOnlyList<string> Roles
    {
        get
        {
            var principal = User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Array.Empty<string>();
            }

            return principal.FindAll(ClaimTypes.Role)
                .Concat(principal.FindAll("role"))
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public IReadOnlyList<string> Permissions
    {
        get
        {
            var principal = User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Array.Empty<string>();
            }

            return principal.FindAll("permission")
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) || !IsAuthenticated)
        {
            return false;
        }

        if (Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // Catalogue management includes read access. System-role management is
        // the explicit Auth permission that grants system-wide catalogue access.
        return permission.Equals(ExperienceCatalogueRead, StringComparison.OrdinalIgnoreCase)
                && Permissions.Any(value => value.Equals(ExperienceCatalogueManage, StringComparison.OrdinalIgnoreCase)
                    || value.Equals(SystemRoleManage, StringComparison.OrdinalIgnoreCase))
            || permission.Equals(ExperienceCatalogueManage, StringComparison.OrdinalIgnoreCase)
                && Permissions.Any(value => value.Equals(SystemRoleManage, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasAnyPermission(params string[] permissions)
    {
        return permissions is { Length: > 0 } && permissions.Any(HasPermission);
    }
}
