using System.Security.Claims;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Blueverse.CoastalOperations.Tests;

public sealed class CoastalAuthorizationPolicyTests
{
    [Fact(DisplayName = "COASTAL-AUTH-001 every controller permission has an allow and deny policy")]
    public async Task COASTAL_AUTH_001_ControllerPermissionPoliciesAreRegisteredAndEnforced()
    {
        var permissions = new[]
        {
            PermissionCodes.OperationsAssessmentCreate,
            PermissionCodes.OperationsAssessmentUpdate,
            PermissionCodes.OperationsAssessmentDelete,
            PermissionCodes.OperationsAssessmentSubmit,
            PermissionCodes.OperationsAssessmentRead,
            PermissionCodes.OperationsAssessmentQueueRead,
            PermissionCodes.OperationsAssessmentDecide,
            PermissionCodes.OperationsTargetStatusRead,
            PermissionCodes.OperationsTargetHistoryRead,
            PermissionCodes.OperationsEvidenceUpload,
            PermissionCodes.OperationsEvidenceRead,
            PermissionCodes.OperationsAlertRead,
            PermissionCodes.OperationsAlertManage,
            PermissionCodes.OperationsAlertDecide
        };

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddAuthorization(CoastalAuthorizationPolicies.Configure)
            .BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        foreach (var permission in permissions)
        {
            var allowedUser = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("permission", permission)], "test"));
            var deniedUser = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("permission", "operations.unrelated")], "test"));

            Assert.True((await authorization.AuthorizeAsync(allowedUser, null, permission)).Succeeded, permission);
            Assert.False((await authorization.AuthorizeAsync(deniedUser, null, permission)).Succeeded, permission);
        }
    }

    [Fact(DisplayName = "COASTAL-AUTH-002 queue and alert manager permissions include their read capabilities")]
    public async Task COASTAL_AUTH_002_ManagerPermissionsSatisfyReadPolicies()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddAuthorization(CoastalAuthorizationPolicies.Configure)
            .BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var queueReader = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permission", PermissionCodes.OperationsAssessmentQueueRead)], "test"));
        var alertManager = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permission", PermissionCodes.OperationsAlertManage)], "test"));

        Assert.True((await authorization.AuthorizeAsync(queueReader, null, PermissionCodes.OperationsAssessmentRead)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(alertManager, null, PermissionCodes.OperationsAlertRead)).Succeeded);
    }
}
