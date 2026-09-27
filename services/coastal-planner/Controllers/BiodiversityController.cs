using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [Authorize]
    public async Task<ActionResult<BiodiversityPredictionDto>> GetPredictions(
        [FromQuery] Guid destinationId, 
        [FromQuery] Guid? activityId, 
        CancellationToken ct)
    {
        if (destinationId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Invalid Destination ID",
                Status = StatusCodes.Status400BadRequest,
                Detail = "destinationId must be a valid non-empty GUID."
            });
        }

        var result = await _plannerService.GetBiodiversityPredictionsAsync(destinationId, activityId, ct);
        return Ok(result);
    }
}
