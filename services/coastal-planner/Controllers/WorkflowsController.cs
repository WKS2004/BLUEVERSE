using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Blueverse.CoastalPlanner.Authorization;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;

namespace Blueverse.CoastalPlanner.Controllers;

[ApiController]
[Route("api/planner/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly ICoastalPlannerService _plannerService;

    public WorkflowsController(ICoastalPlannerService plannerService)
    {
        _plannerService = plannerService;
    }

    [HttpGet("{workflowId:guid}")]
    [HasPermission("planner.workflows.read")]
    public async Task<ActionResult<WorkflowStatusDto>> GetWorkflow(
        [FromRoute] Guid workflowId, 
        CancellationToken ct)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var ownerUserId) || ownerUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _plannerService.GetWorkflowStatusAsync(workflowId, ownerUserId, ct);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Workflow not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Workflow {workflowId} was not found."
            });
        }

        return Ok(result);
    }
}
