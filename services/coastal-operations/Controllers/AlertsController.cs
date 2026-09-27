using System.Diagnostics;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blueverse.CoastalOperations.Controllers;

[ApiController]
[Authorize]
[Route("api/operations/alerts")]
public sealed class AlertsController(AlertApplicationService alerts) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.OperationsAlertRead)]
    [ProducesResponseType(typeof(AlertQueueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertQueueResponse>> List(
        [FromQuery] AlertListQuery query,
        CancellationToken cancellationToken)
    {
        var canManage = User.HasClaim("permission", CoastalPermissions.AlertManage);
        return Ok(await alerts.GetQueueAsync(query, AuthenticatedActor.GetId(User), canManage, cancellationToken));
    }

    [HttpPost]
    [HasPermission(PermissionCodes.OperationsAlertManage)]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AlertResponse>> Create(
        [FromBody] CreateAlertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await alerts.CreateAsync(request, AuthenticatedActor.GetId(User), CorrelationId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("{alertId:guid}")]
    [HasPermission(PermissionCodes.OperationsAlertManage)]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertResponse>> Update(
        Guid alertId,
        [FromBody] UpdateAlertRequest request,
        CancellationToken cancellationToken) =>
        Ok(await alerts.UpdateDraftAsync(alertId, request, AuthenticatedActor.GetId(User), CorrelationId(), cancellationToken));

    [HttpPost("{alertId:guid}/decisions")]
    [HasPermission(PermissionCodes.OperationsAlertDecide)]
    [ProducesResponseType(typeof(AlertDecisionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Decide(
        Guid alertId,
        [FromBody] AlertDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await alerts.DecideAsync(
            alertId,
            request,
            AuthenticatedActor.GetId(User),
            CorrelationId(),
            Request.Headers["Idempotency-Key"].ToString(),
            cancellationToken);
        return new ContentResult
        {
            StatusCode = result.StatusCode,
            ContentType = "application/json; charset=utf-8",
            Content = result.SerializedBody
        };
    }

    private string CorrelationId() => User.FindFirst("correlation_id")?.Value ??
        Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
}
