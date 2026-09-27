using Microsoft.AspNetCore.Mvc;
using Blueverse.CoastalPlanner.Authorization;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;

namespace Blueverse.CoastalPlanner.Controllers;

[ApiController]
[Route("api/planner/biodiversity")]
public class BiodiversityController : ControllerBase
{
    private readonly ICoastalPlannerService _plannerService;

    public BiodiversityController(ICoastalPlannerService plannerService)
    {
        _plannerService = plannerService;
    }

    [HttpGet("predictions")]
    [HasPermission("planner.biodiversity.read")]
    public async Task<ActionResult<BiodiversityPredictionDto>> GetPredictions(
        [FromQuery] Guid destinationId, 
        [FromQuery] Guid? activityId, 
        CancellationToken ct)
    {
        if (destinationId == Guid.Empty || activityId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Invalid biodiversity query",
                Status = StatusCodes.Status400BadRequest,
                Detail = "destinationId must be a valid non-empty GUID and activityId, when supplied, must also be non-empty."
            });
        }

        var result = await _plannerService.GetBiodiversityPredictionsAsync(destinationId, activityId, ct);
        return Ok(result);
    }
}
