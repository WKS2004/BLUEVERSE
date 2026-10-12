using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/experiences")]
public sealed class ExperienceCatalogueController(ExperienceBiodiversityDbContext db) : ControllerBase
{
    [HttpGet("catalogue")]
    [ProducesResponseType(typeof(IReadOnlyList<ExperienceCatalogueItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<ExperienceCatalogueItemDto>>> GetCatalogue(
        [FromQuery] Guid? destinationId,
        [FromQuery] Guid? activityId,
        [FromQuery] Guid? offeringId,
        [FromQuery] string? activityIds,
        CancellationToken cancellationToken)
    {
        if (destinationId == Guid.Empty || activityId == Guid.Empty || offeringId == Guid.Empty)
            return BadRequest(new ProblemDetails { Title = "Invalid catalogue filter", Status = 400,
                Detail = "Catalogue identifiers must be non-empty GUIDs." });

        var selectedActivities = new HashSet<Guid>();
        if (!string.IsNullOrWhiteSpace(activityIds))
        {
            var tokens = activityIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length is 0 or > 100 || tokens.Any(token => !Guid.TryParse(token, out var id) || id == Guid.Empty))
                return BadRequest(new ProblemDetails { Title = "Invalid activity filter", Status = 400,
                    Detail = "activityIds must contain at most 100 non-empty GUIDs." });
            foreach (var token in tokens) selectedActivities.Add(Guid.Parse(token));
        }

        var query = db.Offerings.AsNoTracking()
            .Include(item => item.Destination)
            .Include(item => item.Activity)
            .Include(item => item.Schedules)
            .Where(item => item.Status == PublicationStatus.Published &&
                item.Destination.Status == PublicationStatus.Published &&
                item.Activity.Status == PublicationStatus.Published);

        if (destinationId.HasValue) query = query.Where(item => item.DestinationId == destinationId.Value);
        if (activityId.HasValue) query = query.Where(item => item.ActivityId == activityId.Value);
        if (offeringId.HasValue) query = query.Where(item => item.Id == offeringId.Value);
        if (selectedActivities.Count > 0) query = query.Where(item => selectedActivities.Contains(item.ActivityId));

        var offerings = await query.OrderBy(item => item.Destination.Name).ThenBy(item => item.Title)
            .Take(1001).ToListAsync(cancellationToken);
        if (offerings.Count > 1000)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Catalogue result is too large",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "Filter the catalogue by destination, activity or offering and retry."
            });

        var checkedAt = DateTime.UtcNow;
        var items = new List<ExperienceCatalogueItemDto>();
        foreach (var offering in offerings)
        {
            var schedules = offering.Schedules
                .OrderBy(schedule => schedule.StartsAt)
                .ToArray();
            if (schedules.Length == 0)
            {
                items.Add(ToItem(offering, null, checkedAt));
                continue;
            }

            items.AddRange(schedules.Select(schedule => ToItem(offering, schedule, checkedAt)));
        }

        if (items.Count > 5000)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Catalogue result is too large",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "Filter the catalogue by destination, activity or offering and retry."
            });

        return Ok(items);
    }

    [HttpGet("{targetType}/{targetId:guid}/availability")]
    [ProducesResponseType(typeof(ExperienceTargetAvailabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExperienceTargetAvailabilityDto>> GetTargetAvailability(
        string targetType,
        Guid targetId,
        [FromQuery] DateTimeOffset? periodStartsAt,
        [FromQuery] DateTimeOffset? periodEndsAt,
        CancellationToken cancellationToken)
    {
        var normalizedType = targetType.Trim().ToUpperInvariant();
        if (targetId == Guid.Empty || normalizedType is not ("DESTINATION" or "ACTIVITY" or "OFFERING"))
            return BadRequest(new ProblemDetails { Title = "Invalid target", Status = 400,
                Detail = "Use a non-empty destination, activity or offering identifier." });
        if (periodStartsAt is null || periodEndsAt is null || periodEndsAt <= periodStartsAt)
            return BadRequest(new ProblemDetails { Title = "Invalid availability period", Status = 400,
                Detail = "Provide an end time later than the start time, with an explicit UTC offset." });

        DateTimeOffset updatedAt;
        IQueryable<Offering> relatedOfferings;
        switch (normalizedType)
        {
            case "DESTINATION":
            {
                var destination = await db.Destinations.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == targetId, cancellationToken);
                if (destination is null) return TargetNotFound(normalizedType, targetId);
                updatedAt = destination.UpdatedAt;
                relatedOfferings = db.Offerings.AsNoTracking()
                    .Where(item => item.DestinationId == targetId);
                break;
            }
            case "ACTIVITY":
            {
                var activity = await db.Activities.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == targetId, cancellationToken);
                if (activity is null) return TargetNotFound(normalizedType, targetId);
                updatedAt = activity.UpdatedAt;
                relatedOfferings = db.Offerings.AsNoTracking().Where(item => item.ActivityId == targetId);
                break;
            }
            default:
            {
                var offering = await db.Offerings.AsNoTracking()
                    .Include(item => item.Destination)
                    .Include(item => item.Activity)
                    .Include(item => item.Schedules)
                    .SingleOrDefaultAsync(item => item.Id == targetId, cancellationToken);
                if (offering is null) return TargetNotFound(normalizedType, targetId);
                updatedAt = offering.UpdatedAt;
                var available = IsPublished(offering) && offering.Schedules.Any(schedule =>
                    schedule.IsActive && schedule.StartsAt <= periodStartsAt.Value && schedule.EndsAt >= periodEndsAt.Value);
                var until = available
                    ? offering.Schedules.Where(schedule => schedule.IsActive && schedule.StartsAt <= periodStartsAt.Value && schedule.EndsAt >= periodEndsAt.Value)
                        .Select(schedule => (DateTimeOffset?)schedule.EndsAt).Min()
                    : null;
                return Ok(new ExperienceTargetAvailabilityDto(normalizedType, targetId,
                    available ? "AVAILABLE" : "UNAVAILABLE", updatedAt.ToString("O"), DateTimeOffset.UtcNow, until));
            }
        }

        var destinationIsPublished = normalizedType != "DESTINATION" ||
            await db.Destinations.AsNoTracking().AnyAsync(item => item.Id == targetId && item.Status == PublicationStatus.Published, cancellationToken);
        var activityIsPublished = normalizedType != "ACTIVITY" ||
            await db.Activities.AsNoTracking().AnyAsync(item => item.Id == targetId && item.Status == PublicationStatus.Published, cancellationToken);
        var hasAvailableOffering = destinationIsPublished && activityIsPublished && await relatedOfferings
            .AnyAsync(item => item.Status == PublicationStatus.Published &&
                item.Destination.Status == PublicationStatus.Published &&
                item.Activity.Status == PublicationStatus.Published &&
                item.Schedules.Any(schedule => schedule.IsActive &&
                    schedule.StartsAt <= periodStartsAt.Value && schedule.EndsAt >= periodEndsAt.Value), cancellationToken);

        return Ok(new ExperienceTargetAvailabilityDto(normalizedType, targetId,
            hasAvailableOffering ? "AVAILABLE" : "UNAVAILABLE", updatedAt.ToString("O"), DateTimeOffset.UtcNow, null));
    }

    private static ExperienceCatalogueItemDto ToItem(Offering offering, Schedule? schedule, DateTime checkedAt) => new(
        DestinationId: offering.DestinationId,
        DestinationName: offering.Destination.Name,
        Region: offering.Destination.Region,
        Latitude: offering.Destination.Latitude,
        Longitude: offering.Destination.Longitude,
        ActivityId: offering.ActivityId,
        ActivityName: offering.Activity.Name,
        OfferingId: offering.Id,
        Title: offering.Title,
        AvailabilityStatus: schedule is { IsActive: true } && schedule.EndsAt > DateTimeOffset.UtcNow
            ? "AVAILABLE" : "UNAVAILABLE",
        PublicationState: PublicationStatus.Published,
        AvailableFrom: schedule?.StartsAt.UtcDateTime,
        AvailableUntil: schedule?.EndsAt.UtcDateTime,
        ExperienceLevels: offering.ExperienceLevels,
        CheckedAt: checkedAt,
        TimeZone: schedule?.TimeZoneId);

    private static bool IsPublished(Offering offering) =>
        offering.Status == PublicationStatus.Published &&
        offering.Destination.Status == PublicationStatus.Published &&
        offering.Activity.Status == PublicationStatus.Published;

    private ActionResult<ExperienceTargetAvailabilityDto> TargetNotFound(string targetType, Guid targetId) =>
        NotFound(new ProblemDetails { Title = "Experience target not found", Status = 404,
            Detail = $"No canonical {targetType.ToLowerInvariant()} record exists for {targetId}." });
}
