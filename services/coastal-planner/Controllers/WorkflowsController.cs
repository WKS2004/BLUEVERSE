using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [Authorize]
    public async Task<ActionResult<WorkflowStatusDto>> GetWorkflow(
        [FromRoute] Guid workflowId, 
        CancellationToken ct)
    {
        var result = await _plannerService.GetWorkflowStatusAsync(workflowId, ct);
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
