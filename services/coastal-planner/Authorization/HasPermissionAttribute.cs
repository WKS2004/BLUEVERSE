using Microsoft.AspNetCore.Authorization;

namespace Blueverse.CoastalPlanner.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) => Policy = permission;
}
