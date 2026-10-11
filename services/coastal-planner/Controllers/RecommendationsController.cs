using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.CoastalPlanner.Authorization;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;

namespace Blueverse.CoastalPlanner.Controllers;

[ApiController]
[Route("api/planner/recommendations")]
public class RecommendationsController(ICoastalPlannerService plannerService, IPeerServicesClient peerClient,
    IPlannerCatalogueClient catalogueClient) : ControllerBase
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
            var normalizedRequest = NormalizeTimeZone(request);
            var result = await plannerService.GenerateRecommendationsAsync(normalizedRequest, userId, ct);
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

    private static RecommendationRequestDto NormalizeTimeZone(RecommendationRequestDto request)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZone, out var zone))
            throw new ArgumentException("Choose a supported destination time zone.");

        static DateTime ToUtc(DateTime value, TimeZoneInfo zone)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            var localTime = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(localTime) || zone.IsAmbiguousTime(localTime))
                throw new ArgumentException("Choose a local time that is not skipped or ambiguous in the destination time zone.");
            return TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
        }

        return request with { StartsAt = ToUtc(request.StartsAt, zone), EndsAt = ToUtc(request.EndsAt, zone) };
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<RecommendationHighlightDto>>> GetHighlights(CancellationToken ct)
    {
        var catalogue = await catalogueClient.GetDestinationsAsync(ct);
        if (!string.Equals(catalogue.Status, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Coastal highlights unavailable",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = catalogue.Message ?? "Current coastal highlights are temporarily unavailable. Please try again later."
            });
        }

        if (catalogue.Destinations.Count == 0)
            return Ok(Array.Empty<RecommendationHighlightDto>());

        var result = await GetCatalogueHighlightsAsync(catalogue, ct);
        if (result.Highlights.Count == 0 && result.UnavailableNotes.Count > 0)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Coastal highlights unavailable",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = string.Join(" ", result.UnavailableNotes.Distinct(StringComparer.Ordinal))
            });
        }

        if (result.UnavailableNotes.Count > 0)
        {
            const string partialNotice = "Some coastal highlights could not be shown because supporting experience data is unavailable.";
            return Ok(result.Highlights.Select(highlight => highlight with
            {
                UncertaintyNotes = highlight.UncertaintyNotes.Append(partialNotice).Distinct(StringComparer.Ordinal).ToArray()
            }));
        }

        return Ok(result.Highlights);
    }

    private async Task<HighlightQueryResult> GetCatalogueHighlightsAsync(PlannerCatalogueResult catalogue, CancellationToken ct)
    {
        var highlights = new List<RecommendationHighlightDto>();
        var unavailableNotes = new List<string>();
        var now = DateTime.UtcNow;
        foreach (var destination in catalogue.Destinations)
        {
            ct.ThrowIfCancellationRequested();
            var (items, responded, note) = await peerClient.GetCatalogueOfferingsAsync(destination.DestinationId, null, ct);
            if (!responded)
            {
                unavailableNotes.Add(string.IsNullOrWhiteSpace(note)
                    ? "Experience data is temporarily unavailable. Please try again later."
                    : note);
                continue;
            }

            var activities = destination.Activities.ToDictionary(activity => activity.ActivityId, activity => activity.Name);
            foreach (var item in items.Where(item => item.PublicationState == "PUBLISHED" &&
                         item.AvailabilityStatus == "AVAILABLE" && item.OfferingId.HasValue &&
                         item.AvailableFrom is { Kind: DateTimeKind.Utc } from && item.AvailableUntil is { Kind: DateTimeKind.Utc } until &&
                         from < until && until > now && item.CheckedAt is { Kind: DateTimeKind.Utc } checkedAt &&
                         checkedAt <= now.AddMinutes(1) && checkedAt >= now.AddMinutes(-15) &&
                         !string.IsNullOrWhiteSpace(item.TimeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(item.TimeZone, out _) &&
                         activities.ContainsKey(item.ActivityId)).OrderBy(item => item.AvailableFrom))
            {
                var start = item.AvailableFrom!.Value > now ? item.AvailableFrom.Value : now;
                var end = item.AvailableUntil!.Value;
                if (end <= start) continue;

                var marine = await peerClient.GetMarineSuitabilityAsync(destination.DestinationId, item.ActivityId, start, end, ct);
                if (marine.Result?.Status == "UNSUITABLE") continue;
                var operations = await peerClient.GetOperationalStatusAsync(destination.DestinationId, ct);
                if (operations.Result?.OperationalStatus is "TEMPORARILY_SUSPENDED" or "CANCELLED" or "COMPLETED") continue;

                var uncertainty = new List<string>();
                if (!marine.Responded || marine.Result?.Status == "UNKNOWN") uncertainty.Add("Marine suitability is not verified.");
                if (!operations.Responded || operations.Result is null) uncertainty.Add("Current operating status is not verified.");
                if (highlights.Count == 3) return new(highlights, unavailableNotes);

                highlights.Add(new RecommendationHighlightDto(
                    Id: item.OfferingId!.Value,
                    Title: item.Title,
                    StartsAt: start,
                    EndsAt: end,
                    TimeZone: item.TimeZone!,
                    DestinationId: destination.DestinationId,
                    DestinationName: destination.Name,
                    ActivityId: item.ActivityId,
                    ActivityName: activities[item.ActivityId],
                    OfferingId: item.OfferingId,
                    FitScore: null,
                    SuitabilityStatus: marine.Result?.Status ?? "UNKNOWN",
                    AvailabilityStatus: item.AvailabilityStatus,
                    OperationalStatus: operations.Result?.OperationalStatus ?? "UNKNOWN",
                    RecommendationId: null,
                    UncertaintyNotes: uncertainty.ToArray(),
                    GeneratedAt: now,
                    Outcome: "MATCHES_FOUND",
                    ItemCount: 1,
                    EvidenceCheckedAt: item.CheckedAt,
                    StartsAtLocal: FormatLocalTime(start, item.TimeZone!),
                    EndsAtLocal: FormatLocalTime(end, item.TimeZone!)));
            }
        }

        return new(highlights, unavailableNotes);
    }

    private sealed record HighlightQueryResult(List<RecommendationHighlightDto> Highlights, List<string> UnavailableNotes);

    private static string FormatLocalTime(DateTime utc, string timeZone)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone)
            .ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdStr, out userId) && userId != Guid.Empty;
    }
}
