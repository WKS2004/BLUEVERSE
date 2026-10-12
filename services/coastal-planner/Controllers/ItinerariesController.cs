using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.CoastalPlanner.Authorization;
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
        return Guid.TryParse(userIdStr, out userId) && userId != Guid.Empty;
    }

    [HttpPost("{itineraryId:guid}/confirm")]
    [HasPermission("planner.itineraries.manage")]
    public async Task<ActionResult<ItineraryDto>> ConfirmItinerary(
        [FromRoute] Guid itineraryId,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var result = await _plannerService.ConfirmItineraryAsync(itineraryId, userId, ct);
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

    [HttpPost]
    [HasPermission("planner.itineraries.manage")]
    public async Task<ActionResult<ItineraryDto>> CreateItinerary(
        [FromBody] CreateItineraryRequestDto request, 
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
            var result = await _plannerService.CreateItineraryAsync(request, userId, ct);
            return CreatedAtAction(nameof(GetItinerary), new { itineraryId = result.ItineraryId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid itinerary", Status = StatusCodes.Status400BadRequest, Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Trip cannot be confirmed", Status = StatusCodes.Status400BadRequest, Detail = ex.Message });
        }
    }

    [HttpGet]
    [HasPermission("planner.itineraries.manage")]
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
    [HasPermission("planner.itineraries.manage")]
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
    [HasPermission("planner.itineraries.manage")]
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
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid itinerary", Status = StatusCodes.Status400BadRequest, Detail = ex.Message });
        }
    }

    [HttpDelete("{itineraryId:guid}")]
    [HasPermission("planner.itineraries.manage")]
    public async Task<IActionResult> DeleteItinerary(
        [FromRoute] Guid itineraryId, 
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
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
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Trip cannot be cancelled", Status = StatusCodes.Status409Conflict, Detail = ex.Message });
        }
    }

    [HttpPost("{itineraryId:guid}/re-evaluations")]
    [HasPermission("planner.itineraries.manage")]
    public async Task<ActionResult<ItineraryReEvaluationResultDto>> ReEvaluateItinerary(
        [FromRoute] Guid itineraryId, 
        [FromBody] ItineraryReEvaluationRequestDto request, 
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        ItineraryReEvaluationResultDto? result;
        try { result = await _plannerService.ReEvaluateItineraryAsync(itineraryId, userId, request, ct); }
        catch (ArgumentException ex) { return BadRequest(new ProblemDetails { Title = "Invalid condition review", Status = 400, Detail = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "Trip changed", Status = 409, Detail = "Reload the trip before reviewing conditions again." }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(503, new ProblemDetails { Title = "Review timed out", Status = 503, Detail = "Conditions could not be checked in time. Your trip has been kept. Please try again." }); }
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

    [HttpGet("{itineraryId:guid}/re-evaluations")]
    [HasPermission("planner.itineraries.manage")]
    public async Task<ActionResult<List<ItineraryReEvaluationResultDto>>> GetEvaluationHistory(Guid itineraryId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var results = await _plannerService.GetEvaluationHistoryAsync(itineraryId, userId, ct);
        return results is null ? NotFound(new ProblemDetails { Title = "Trip not found", Status = 404 }) : Ok(results);
    }
}
