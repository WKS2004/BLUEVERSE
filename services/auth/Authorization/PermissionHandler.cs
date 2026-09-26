using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Blueverse.Auth.Data;

namespace Blueverse.Auth.Authorization;

public sealed class PermissionHandler(AuthDbContext dbContext) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(subject, out var userId))
        {
            return;
        }

        var permittedCodes = PermissionCodes.LegacyGrantAliases(requirement.Permission)
            .Append(requirement.Permission)
            .ToArray();

        // JWT permission claims are a display snapshot. Authorization is resolved
        // from the active account's current role assignments on every request so
        // stale or forged claim sets cannot grant access after a role change.
        var hasPermission = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .SelectMany(user => user.UserRoles)
            .SelectMany(userRole => userRole.Role.RolePermissions)
            .AnyAsync(rolePermission => permittedCodes.Contains(rolePermission.Permission.Code));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
