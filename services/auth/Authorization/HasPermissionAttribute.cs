using Microsoft.AspNetCore.Authorization;

namespace Blueverse.Auth.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = $"PERMISSION:{permission}";
    }
}
