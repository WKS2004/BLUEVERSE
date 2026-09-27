using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;

namespace Blueverse.ExperienceBiodiversity.Services;

public interface IEvaluationService
{
    Task<PublicationEvaluationResponse> EvaluateDestinationPublicationAsync(Guid destinationId, string requestedStatus, CancellationToken cancellationToken = default);
    Task<PublicationEvaluationResponse> EvaluateActivityPublicationAsync(Guid activityId, string requestedStatus, CancellationToken cancellationToken = default);
    Task<PublicationEvaluationResponse> EvaluateOfferingPublicationAsync(Guid offeringId, string requestedStatus, CancellationToken cancellationToken = default);
    Task<AvailabilityEvaluationResponse> EvaluateAvailabilityAsync(AvailabilityEvaluationRequest request, CancellationToken cancellationToken = default);
}

public sealed class EvaluationService : IEvaluationService
{
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly IOperationalStatusConsumerService _operationalStatusConsumer;
    private readonly ILogger<EvaluationService> _logger;

    public EvaluationService(
        ExperienceBiodiversityDbContext dbContext,
        IOperationalStatusConsumerService operationalStatusConsumer,
        ILogger<EvaluationService> logger)
    {
        _dbContext = dbContext;
        _operationalStatusConsumer = operationalStatusConsumer;
        _logger = logger;
    }

    public async Task<PublicationEvaluationResponse> EvaluateDestinationPublicationAsync(
        Guid destinationId,
        string requestedStatus,
        CancellationToken cancellationToken = default)
    {
        var dest = await _dbContext.Destinations.FindAsync(new object[] { destinationId }, cancellationToken);
        if (dest == null)
        {
            return new PublicationEvaluationResponse(destinationId, "Destination", "NOT_FOUND", requestedStatus, false, new[] { "Destination not found." });
        }

        return EvaluateGeneralPublication(dest.Id, "Destination", dest.Status, requestedStatus, new List<string>
        {
            string.IsNullOrWhiteSpace(dest.Name) ? "Destination name cannot be blank." : null!,
            dest.Latitude < -90 || dest.Latitude > 90 ? "Destination latitude must be between -90 and 90." : null!,
            dest.Longitude < -180 || dest.Longitude > 180 ? "Destination longitude must be between -180 and 180." : null!
        }.Where(x => x != null).ToList());
    }

    public async Task<PublicationEvaluationResponse> EvaluateActivityPublicationAsync(
        Guid activityId,
        string requestedStatus,
        CancellationToken cancellationToken = default)
    {
        var act = await _dbContext.Activities.FindAsync(new object[] { activityId }, cancellationToken);
        if (act == null)
        {
            return new PublicationEvaluationResponse(activityId, "Activity", "NOT_FOUND", requestedStatus, false, new[] { "Activity not found." });
        }

        return EvaluateGeneralPublication(act.Id, "Activity", act.Status, requestedStatus, new List<string>
        {
            string.IsNullOrWhiteSpace(act.Name) ? "Activity name cannot be blank." : null!,
            string.IsNullOrWhiteSpace(act.Code) ? "Activity code cannot be blank." : null!
        }.Where(x => x != null).ToList());
    }

    public async Task<PublicationEvaluationResponse> EvaluateOfferingPublicationAsync(
        Guid offeringId,
        string requestedStatus,
        CancellationToken cancellationToken = default)
    {
        var offering = await _dbContext.Offerings
            .Include(o => o.Destination)
            .Include(o => o.Activity)
            .FirstOrDefaultAsync(o => o.Id == offeringId, cancellationToken);

        if (offering == null)
        {
            return new PublicationEvaluationResponse(offeringId, "Offering", "NOT_FOUND", requestedStatus, false, new[] { "Offering not found." });
        }

        var blockers = new List<string>();
        if (string.IsNullOrWhiteSpace(offering.Title))
        {
            blockers.Add("Offering title cannot be blank.");
        }

        if (string.Equals(requestedStatus, PublicationStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            if (offering.Destination.Status != PublicationStatus.Published)
            {
                blockers.Add($"Parent Destination '{offering.Destination.Name}' must be PUBLISHED before the offering can be published.");
            }
            if (offering.Activity.Status != PublicationStatus.Published)
            {
                blockers.Add($"Parent Activity '{offering.Activity.Name}' must be PUBLISHED before the offering can be published.");
            }
        }

        return EvaluateGeneralPublication(offering.Id, "Offering", offering.Status, requestedStatus, blockers);
    }

    private static PublicationEvaluationResponse EvaluateGeneralPublication(
        Guid id,
        string type,
        string currentStatus,
        string requestedStatus,
        List<string> domainBlockers)
    {
        var reasons = new List<string>(domainBlockers);
        var normCurrent = currentStatus.ToUpperInvariant();
        var normRequested = requestedStatus.ToUpperInvariant();

        if (!PublicationStatus.All.Contains(normRequested))
        {
            reasons.Add($"Target status '{requestedStatus}' is invalid. Allowed: DRAFT, PUBLISHED, ARCHIVED.");
            return new PublicationEvaluationResponse(id, type, currentStatus, requestedStatus, false, reasons);
        }

        if (normCurrent == PublicationStatus.Archived)
        {
            reasons.Add("Archived items are terminal and cannot be transitioned to another state.");
            return new PublicationEvaluationResponse(id, type, currentStatus, requestedStatus, false, reasons);
        }

        if (normCurrent == normRequested)
        {
            reasons.Add($"Item is already in state '{normRequested}'.");
            return new PublicationEvaluationResponse(id, type, currentStatus, requestedStatus, true, reasons);
        }

        var canTransition = reasons.Count == 0;
        return new PublicationEvaluationResponse(id, type, currentStatus, requestedStatus, canTransition, reasons);
    }

    public async Task<AvailabilityEvaluationResponse> EvaluateAvailabilityAsync(
        AvailabilityEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var reasons = new List<string>();

        if (request.EndsAt <= request.StartsAt)
        {
            reasons.Add("INVALID_INTERVAL: endsAt must be strictly greater than startsAt.");
            return BuildResponse(request.OfferingId, request.StartsAt, request.EndsAt, AvailabilityStatus.Unavailable, reasons, null!, null);
        }

        var offering = await _dbContext.Offerings
            .AsNoTracking()
            .Include(o => o.Destination)
            .Include(o => o.Activity)
            .Include(o => o.Schedules.Where(s => s.IsActive))
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId, cancellationToken);

        if (offering == null)
        {
            reasons.Add("OFFERING_NOT_FOUND: Offering with the specified ID does not exist.");
            return BuildResponse(request.OfferingId, request.StartsAt, request.EndsAt, AvailabilityStatus.Unavailable, reasons, null!, null);
        }

        var summary = new OfferingSummaryDto(
            offering.Id,
            offering.Title,
            offering.Destination.Id,
            offering.Destination.Name,
            offering.Destination.Status,
            offering.Activity.Id,
            offering.Activity.Name,
            offering.Activity.Status,
            offering.Status);

        // 1. Catalogue Publication Eligibility Check
        if (offering.Status != PublicationStatus.Published)
        {
            reasons.Add("OFFERING_NOT_PUBLISHED: Offering status is not PUBLISHED.");
        }
        if (offering.Destination.Status != PublicationStatus.Published)
        {
            reasons.Add("DESTINATION_NOT_PUBLISHED: Parent destination is not PUBLISHED.");
        }
        if (offering.Activity.Status != PublicationStatus.Published)
        {
            reasons.Add("ACTIVITY_NOT_PUBLISHED: Parent activity is not PUBLISHED.");
        }

        // 2. Schedule Coverage Check [startsAt, endsAt)
        var hasMatchingSchedule = offering.Schedules.Any(s =>
            s.IsActive &&
            s.StartsAt <= request.StartsAt &&
            s.EndsAt >= request.EndsAt);

        if (!hasMatchingSchedule)
        {
            reasons.Add("NO_SCHEDULE_COVERAGE: No active schedule encompasses the requested interval.");
        }

        // 3. Operational Restriction Check (Member 4 Integration)
        var opsContext = await _operationalStatusConsumer.GetOperationalStatusAsync(
            offering.DestinationId,
            offering.Id,
            cancellationToken);

        if (opsContext.HasRestriction)
        {
            reasons.Add($"OPERATIONAL_RESTRICTION_ACTIVE: {opsContext.Reason ?? "Active restriction imposed by coastal operations authority."}");
        }

        if (string.Equals(opsContext.SourceStatus, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("OPERATIONAL_SERVICE_UNAVAILABLE: Operational review status could not be verified; optimistic availability is strictly prohibited.");
            return BuildResponse(request.OfferingId, request.StartsAt, request.EndsAt, AvailabilityStatus.Unknown, reasons, summary, opsContext);
        }

        var finalStatus = reasons.Count == 0 ? AvailabilityStatus.Available : AvailabilityStatus.Unavailable;
        return BuildResponse(request.OfferingId, request.StartsAt, request.EndsAt, finalStatus, reasons, summary, opsContext);
    }

    private static AvailabilityEvaluationResponse BuildResponse(
        Guid offeringId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string status,
        IReadOnlyList<string> reasons,
        OfferingSummaryDto summary,
        OperationalRestrictionContextDto? opsContext)
    {
        return new AvailabilityEvaluationResponse(
            OfferingId: offeringId,
            StartsAt: startsAt,
            EndsAt: endsAt,
            Status: status,
            ReasonCodes: reasons,
            Offering: summary,
            OperationalRestriction: opsContext,
            EvaluatedAt: DateTimeOffset.UtcNow);
    }
}
