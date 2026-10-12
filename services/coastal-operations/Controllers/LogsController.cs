using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blueverse.CoastalOperations.Controllers;

[ApiController]
[Authorize]
[Route("api/operations/logs")]
public sealed class LogsController(AssessmentApplicationService assessments, AlertApplicationService alerts) : ControllerBase
{
    [HttpGet("/api/operations/logs/assessments")]
    [HasPermission(PermissionCodes.OperationsAuditRead)]
    [ProducesResponseType(typeof(AssessmentQueueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentQueueResponse>> Assessments([FromQuery] AssessmentListQuery query, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var queue = User.HasClaim("permission", CoastalPermissions.AssessmentQueueRead);
        if (!queue && !User.HasClaim("permission", CoastalPermissions.AssessmentRead)) return Forbid();
        return Ok(await assessments.GetQueueAsync(query, AuthenticatedActor.GetId(User), queue, cancellationToken, auditView: true));
    }

    [HttpGet("/api/operations/logs/alerts")]
    [HasPermission(PermissionCodes.OperationsAuditRead)]
    [ProducesResponseType(typeof(AlertQueueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertQueueResponse>> Alerts([FromQuery] AlertListQuery query, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var manage = CoastalAlertAccess.CanManage(User);
        if (!manage && !User.HasClaim("permission", CoastalPermissions.AlertRead)) return Forbid();
        return Ok(await alerts.GetQueueAsync(query, AuthenticatedActor.GetId(User), manage, cancellationToken, auditView: true));
    }
}
