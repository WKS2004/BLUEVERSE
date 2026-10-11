using System.Security.Claims;
namespace Blueverse.MarineSafety.Authorization;
public interface IPermissionResolver
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken);
}
// Auth resolves current permissions; the public API signs the operation context.
public sealed class GatewayPermissionResolver(IHttpContextAccessor accessor) : IPermissionResolver
{
    public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var principal = accessor.HttpContext?.User;
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        return Task.FromResult(principal?.Identity?.IsAuthenticated == true &&
            Guid.TryParse(subject, out var actor) && actor == userId && principal.HasClaim("permission", permissionCode));
    }
}
