using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Blueverse.CoastalPlanner.Authorization;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;

namespace Blueverse.CoastalPlanner.Controllers;

[ApiController]
[Route("api/planner/recommendations")]
public class RecommendationsController(ICoastalPlannerService plannerService) : ControllerBase
{
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
            var result = await plannerService.GenerateRecommendationsAsync(request, userId, ct);
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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(503, new ProblemDetails { Title = "Search timed out", Status = 503,
                Detail = "We couldn’t verify your experiences in time. Please try again." });
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

        var result = await plannerService.GetRecommendationAsync(recommendationId, userId, ct);
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

    [HttpGet("highlights")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<RecommendationHighlightDto>>> GetHighlights(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId) || userId == Guid.Empty)
        {
            return Ok(await GetCatalogueHighlightsAsync(ct));
        }

        var itineraryIds = await plannerService.ListItineraryIdsForHighlightsAsync(userId, ct);
        if (itineraryIds.Count == 0)
        {
            return Ok(await GetCatalogueHighlightsAsync(ct));
        }

        var highlights = new List<RecommendationHighlightDto>();
        foreach (var itineraryId in itineraryIds.Take(3))
        {
            var itinerary = await plannerService.GetItineraryForRecommendationHighlightAsync(itineraryId, userId, ct);
            if (itinerary is not null && itinerary.Items.Count > 0)
            {
                highlights.Add(MapItineraryToHighlight(itinerary));
            }
        }

        if (highlights.Count == 0)
        {
            return Ok(await GetCatalogueHighlightsAsync(ct));
        }

        return Ok(highlights);
    }

    private async Task<List<RecommendationHighlightDto>> GetCatalogueHighlightsAsync(CancellationToken ct)
    {
        var peerClient = plannerService.GetPeerClient();
        var (catalogueItems, responded, _) = await peerClient.GetCatalogueOfferingsAsync(Guid.Empty, null, ct);
        if (!responded || catalogueItems.Count == 0)
        {
            return new List<RecommendationHighlightDto>();
        }

        var highlights = new List<RecommendationHighlightDto>();
        foreach (var item in catalogueItems.Take(3))
        {
            highlights.Add(new RecommendationHighlightDto(
                Id: item.OfferingId ?? Guid.NewGuid(),
                Title: item.Title ?? "Coastal highlight",
                StartsAt: DateTime.UtcNow,
                EndsAt: DateTime.UtcNow.AddHours(2),
                TimeZone: item.TimeZone ?? "Asia/Colombo",
                DestinationId: item.DestinationId,
                DestinationName: item.Title ?? "Coastal highlight",
                ActivityId: item.ActivityId,
                ActivityName: item.Title ?? "Coastal highlight",
                OfferingId: item.OfferingId,
                FitScore: 0.7,
                SuitabilityStatus: "UNKNOWN",
                AvailabilityStatus: item.AvailabilityStatus,
                OperationalStatus: "OPEN",
                RecommendationId: null,
                UncertaintyNotes: Array.Empty<string>(),
                GeneratedAt: DateTime.UtcNow,
                Outcome: "MATCHES_FOUND",
                ItemCount: 1));
        }

        return highlights;
    }

    private static RecommendationHighlightDto MapItineraryToHighlight(ItineraryDto itinerary)
    {
        var topItem = itinerary.Items.OrderByDescending(item => item.FitScore).FirstOrDefault();
        return new RecommendationHighlightDto(
            Id: itinerary.ItineraryId,
            Title: itinerary.Title,
            StartsAt: itinerary.StartsAt,
            EndsAt: itinerary.EndsAt,
            TimeZone: itinerary.TimeZone,
            DestinationId: topItem?.DestinationId ?? Guid.Empty,
            DestinationName: topItem?.Title ?? itinerary.Title,
            ActivityId: topItem?.ActivityId ?? Guid.Empty,
            ActivityName: topItem?.Title ?? itinerary.Title,
            OfferingId: topItem?.OfferingId,
            FitScore: topItem?.FitScore ?? 0,
            SuitabilityStatus: topItem?.LastSuitabilityStatus ?? "UNKNOWN",
            AvailabilityStatus: topItem?.LastAvailabilityStatus ?? "UNKNOWN",
            OperationalStatus: topItem?.LastOperationalStatus ?? "UNKNOWN",
            RecommendationId: null,
            UncertaintyNotes: Array.Empty<string>(),
            GeneratedAt: itinerary.CreatedAt,
            Outcome: "MATCHES_FOUND",
            ItemCount: itinerary.Items.Count);
    }

    private static bool TryGetUserId(out Guid userId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdStr, out userId) && userId != Guid.Empty;
    }
}
