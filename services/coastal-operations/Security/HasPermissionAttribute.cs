using Microsoft.AspNetCore.Authorization;

namespace Blueverse.CoastalOperations.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permissionCode)
    {
        PermissionCode = permissionCode;
        Policy = permissionCode;
    }

    public string PermissionCode { get; }
}
