using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Blueverse.ExperienceBiodiversity.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _requiredPermission;

    public HasPermissionAttribute(string requiredPermission)
    {
        _requiredPermission = requiredPermission;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var permissions = user.FindAll("permission")
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var authorized = permissions.Contains(_requiredPermission)
            || _requiredPermission.Equals("experiences.catalogue.read", StringComparison.OrdinalIgnoreCase)
                && (permissions.Contains("experiences.catalogue.manage")
                    || permissions.Contains("auth.role.system.manage"))
            || _requiredPermission.Equals("experiences.catalogue.manage", StringComparison.OrdinalIgnoreCase)
                && permissions.Contains("auth.role.system.manage");

        if (!authorized)
        {
            context.Result = new ForbidResult();
        }

        return Task.CompletedTask;
    }
}
