using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Blueverse.CoastalPlanner.Authorization;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;

namespace Blueverse.CoastalPlanner.Controllers;

[ApiController]
[Route("api/planner/recommendations")]
public class RecommendationsController : ControllerBase
{
    private readonly ICoastalPlannerService _plannerService;

    public RecommendationsController(ICoastalPlannerService plannerService)
    {
        _plannerService = plannerService;
    }

    [HttpPost]
    [HasPermission("planner.recommendations.create")]
    public async Task<ActionResult<RecommendationResultDto>> CreateRecommendations(
        [FromBody] RecommendationRequestDto request, 
        CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdStr, out var userId) || userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _plannerService.GenerateRecommendationsAsync(request, userId, ct);
            return CreatedAtAction(nameof(GetRecommendation), new { recommendationId = result.RecommendationId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Invalid planning parameters",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{recommendationId:guid}")]
    [HasPermission("planner.recommendations.read")]
    public async Task<ActionResult<RecommendationResultDto>> GetRecommendation(
        [FromRoute] Guid recommendationId, 
        CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdStr, out var userId) || userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _plannerService.GetRecommendationAsync(recommendationId, userId, ct);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Recommendation session not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Recommendation session {recommendationId} was not found."
            });
        }

        return Ok(result);
    }
}
