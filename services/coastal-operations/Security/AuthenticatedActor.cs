using System.Security.Claims;

namespace Blueverse.CoastalOperations.Security;

public static class AuthenticatedActor
{
    public static Guid GetId(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub") ??
                      principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out var actorId) ? actorId : Guid.Empty;
    }
}
