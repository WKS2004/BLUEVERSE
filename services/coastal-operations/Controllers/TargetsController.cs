using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blueverse.CoastalOperations.Controllers;

[ApiController]
[Authorize]
[Route("api/operations/targets")]
public sealed class TargetsController(TargetStatusApplicationService targets) : ControllerBase
{
    [HttpGet("{targetType}/{targetId:guid}/status")]
    [HasPermission(PermissionCodes.OperationsTargetStatusRead)]
    [ProducesResponseType(typeof(OperationalStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationalStatusResponse>> GetStatus(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken) =>
        Ok(await targets.GetStatusAsync(targetType, targetId, cancellationToken));

    [HttpGet("{targetType}/{targetId:guid}/history")]
    [HasPermission(PermissionCodes.OperationsTargetHistoryRead)]
    [ProducesResponseType(typeof(OperationalHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationalHistoryResponse>> GetHistory(
        string targetType,
        Guid targetId,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default) =>
        Ok(await targets.GetHistoryAsync(targetType, targetId, pageSize, cursor, cancellationToken));
}
