using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;

namespace Blueverse.CoastalPlanner.Controllers;

[ApiController]
[Route("api/planner/itineraries")]
public class ItinerariesController : ControllerBase
{
    private readonly ICoastalPlannerService _plannerService;

    public ItinerariesController(ICoastalPlannerService plannerService)
    {
        _plannerService = plannerService;
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdStr, out userId);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ItineraryDto>> CreateItinerary(
        [FromBody] CreateItineraryRequestDto request, 
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var result = await _plannerService.CreateItineraryAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetItinerary), new { itineraryId = result.ItineraryId }, result);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<ItineraryDto>>> ListItineraries(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20, 
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var list = await _plannerService.ListItinerariesAsync(userId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
        return Ok(list);
    }

    [HttpGet("{itineraryId:guid}")]
    [Authorize]
    public async Task<ActionResult<ItineraryDto>> GetItinerary(
        [FromRoute] Guid itineraryId, 
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var itinerary = await _plannerService.GetItineraryAsync(itineraryId, userId, ct);
        if (itinerary == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Itinerary not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Itinerary {itineraryId} not found."
            });
        }

        return Ok(itinerary);
    }

    [HttpPut("{itineraryId:guid}")]
    [Authorize]
    public async Task<ActionResult<ItineraryDto>> UpdateItinerary(
        [FromRoute] Guid itineraryId, 
        [FromBody] UpdateItineraryRequestDto request, 
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
            var updated = await _plannerService.UpdateItineraryAsync(itineraryId, request, userId, ct);
            if (updated == null)
            {
                return NotFound(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7807",
                    Title = "Itinerary not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Itinerary {itineraryId} not found."
                });
            }
            return Ok(updated);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Concurrency Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = "The itinerary was modified by another process. Refresh and retry."
            });
        }
    }

    [HttpDelete("{itineraryId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteItinerary(
        [FromRoute] Guid itineraryId, 
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var success = await _plannerService.DeleteItineraryAsync(itineraryId, userId, ct);
        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Itinerary not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Itinerary {itineraryId} not found."
            });
        }

        return NoContent();
    }

    [HttpPost("{itineraryId:guid}/re-evaluations")]
    [Authorize]
    public async Task<ActionResult<ItineraryReEvaluationResultDto>> ReEvaluateItinerary(
        [FromRoute] Guid itineraryId, 
        [FromBody] ItineraryReEvaluationRequestDto request, 
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var result = await _plannerService.ReEvaluateItineraryAsync(itineraryId, userId, request, ct);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Itinerary not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Itinerary {itineraryId} not found."
            });
        }

        return Ok(result);
    }
}
