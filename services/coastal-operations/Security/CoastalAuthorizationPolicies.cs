using Microsoft.AspNetCore.Authorization;

namespace Blueverse.CoastalOperations.Security;

public static class CoastalAuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentCreate);
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentDecide);
        AddPermissionPolicy(options, PermissionCodes.OperationsAssessmentQueueRead);
        AddPermissionPolicy(options, PermissionCodes.OperationsTargetStatusRead);
        AddPermissionPolicy(options, PermissionCodes.OperationsTargetHistoryRead);
        AddPermissionPolicy(options, PermissionCodes.OperationsEvidenceUpload);
        AddPermissionPolicy(options, PermissionCodes.OperationsEvidenceRead);
        AddPermissionPolicy(options, PermissionCodes.OperationsAlertManage);
        AddPermissionPolicy(options, PermissionCodes.OperationsAlertDecide);

        options.AddPolicy(PermissionCodes.OperationsAssessmentRead, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasClaim("permission", PermissionCodes.OperationsAssessmentRead) ||
                context.User.HasClaim("permission", PermissionCodes.OperationsAssessmentQueueRead)));
        options.AddPolicy(PermissionCodes.OperationsAlertRead, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasClaim("permission", PermissionCodes.OperationsAlertRead) ||
                context.User.HasClaim("permission", PermissionCodes.OperationsAlertManage)));
    }

    private static void AddPermissionPolicy(AuthorizationOptions options, string permission) =>
        options.AddPolicy(permission, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("permission", permission));
}
