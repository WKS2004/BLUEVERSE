using Microsoft.AspNetCore.Authorization;

namespace Blueverse.CoastalOperations.Security;

public static class CoastalAuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(PermissionCodes.OperationsFormOptionsRead, policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.Claims.Any(x => x.Type == "permission" &&
                typeof(CoastalPermissions).GetFields().Any(field => (string?)field.GetRawConstantValue() == x.Value))));
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentCreate);
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentUpdate);
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentDelete);
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentSubmit);
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentDecide);
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentQueueRead);
        AddPermissionPolicy(options, PermissionCodes.OperationsTargetStatusRead);
        AddPermissionPolicy(options, PermissionCodes.OperationsTargetHistoryRead);
        AddPermissionPolicy(options, PermissionCodes.OperationsEvidenceUpload);
        AddPermissionPolicy(options, PermissionCodes.OperationsEvidenceRead);
        AddPermissionPolicy(options, PermissionCodes.OperationsAlertManage);
        AddPermissionPolicy(options, PermissionCodes.OperationsAuditRead);
        AddAlternativePolicy(options, PermissionCodes.OperationsAlertCreate, CoastalPermissions.AlertManage);
        AddAlternativePolicy(options, PermissionCodes.OperationsAlertUpdate, CoastalPermissions.AlertManage);
        AddAlternativePolicy(options, PermissionCodes.OperationsAlertDelete, CoastalPermissions.AlertManage);
        AddPermissionPolicy(options, PermissionCodes.OperationsAlertPublish);
        AddPermissionPolicy(options, PermissionCodes.OperationsAlertResolve);
        options.AddPolicy(PermissionCodes.OperationsAlertDecide, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.HasClaim("permission", CoastalPermissions.AlertDecide) ||
                context.User.HasClaim("permission", CoastalPermissions.AlertPublish) ||
                context.User.HasClaim("permission", CoastalPermissions.AlertResolve)));

        options.AddPolicy(PermissionCodes.OperationsAssessmentRead, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasClaim("permission", PermissionCodes.OperationsAssessmentRead) ||
                context.User.HasClaim("permission", PermissionCodes.OperationsAssessmentQueueRead)));
        options.AddPolicy(PermissionCodes.OperationsAlertRead, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasClaim("permission", PermissionCodes.OperationsAlertRead) ||
                CoastalAlertAccess.CanManage(context.User)));
    }

    private static void AddAlternativePolicy(AuthorizationOptions options, string permission, string legacy) =>
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.HasClaim("permission", permission) ||
                context.User.HasClaim("permission", legacy)));

    private static void AddPermissionPolicy(AuthorizationOptions options, string permission) =>
        options.AddPolicy(permission, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("permission", permission));
}
