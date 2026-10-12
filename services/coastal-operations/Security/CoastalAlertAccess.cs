using System.Security.Claims;

namespace Blueverse.CoastalOperations.Security;

public static class CoastalAlertAccess
{
    public static readonly string[] ManagementGrants =
    [CoastalPermissions.AlertManage, CoastalPermissions.AlertDecide,
     CoastalPermissions.AlertCreate, CoastalPermissions.AlertUpdate,
     CoastalPermissions.AlertDelete, CoastalPermissions.AlertPublish, CoastalPermissions.AlertResolve];

    public static bool CanManage(ClaimsPrincipal user) =>
        ManagementGrants.Any(grant => user.HasClaim("permission", grant));

    public static bool CanDecide(ClaimsPrincipal user, string? decision) =>
        decision?.Trim().ToUpperInvariant() switch
        {
            "PUBLISH" => user.HasClaim("permission", CoastalPermissions.AlertPublish) || user.HasClaim("permission", CoastalPermissions.AlertDecide),
            "RESOLVE" => user.HasClaim("permission", CoastalPermissions.AlertResolve) || user.HasClaim("permission", CoastalPermissions.AlertDecide),
            _ => false
        };
}
