using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Blueverse.MarineSafety.Authorization;

/// <summary>
/// Permission attribute for the marine-safety service, matching the Auth
/// service's policy convention so one role-to-permission model serves both.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = $"PERMISSION:{permission}";
    }
}

/// <summary>Named permission codes enforced by this service; the codes are
/// seeded for the Admin role by the Auth service's seeder.</summary>
public static class PermissionCodes
{
    public const string MarineProfileRead = "marine.profile.read";
    public const string MarineProfileManage = "marine.profile.manage";
}

/// <summary>Requirement carrying the concrete permission code.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Builds PERMISSION:&lt;code&gt; policies on demand, like the Auth service.</summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("PERMISSION:", StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName["PERMISSION:".Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}

/// <summary>
/// Resolves the caller's permission through the configured
/// <see cref="IPermissionResolver"/>. JWT permission claims are a display
/// snapshot; this database resolution is authoritative, exactly as the Auth
/// service resolves permissions for its own endpoints.
/// </summary>
public sealed class PermissionHandler(
    IPermissionResolver resolver,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(subject, out var userId))
        {
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted
            ?? (context.Resource as HttpContext)?.RequestAborted
            ?? CancellationToken.None;

        if (await resolver.HasPermissionAsync(userId, requirement.Permission, cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
