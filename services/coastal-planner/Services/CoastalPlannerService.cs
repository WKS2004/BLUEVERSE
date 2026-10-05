using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Data.Entities;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Models.Dtos;

namespace Blueverse.CoastalPlanner.Services;

public partial class CoastalPlannerService : ICoastalPlannerService
{
    private readonly CoastalPlannerDbContext _db;
    private readonly IPeerServicesClient _peerClient;
    private readonly ILogger<CoastalPlannerService> _logger;
    private readonly IPlanningCoordinationClient? _coordinationClient;

    public CoastalPlannerService(
        CoastalPlannerDbContext db, 
        IPeerServicesClient peerClient,
        ILogger<CoastalPlannerService> logger,
        IPlanningCoordinationClient? coordinationClient = null)
    {
        _db = db;
        _peerClient = peerClient;
        _logger = logger;
        _coordinationClient = coordinationClient;
    }

    public async Task<RecommendationResultDto> GenerateRecommendationsAsync(
        RecommendationRequestDto request, 
        Guid userId, 
        CancellationToken ct = default)
    {
        ValidateRecommendationRequest(request, userId);
        var callerCancellation = ct;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(40));
        ct = budget.Token;

        var workflow = new PlanningWorkflow
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowType = "TOURIST_RECOMMENDATION",
            Status = "PROCESSING",
            InitiatorUserId = userId,
            Objective = $"Coastal planning for destination {request.TargetDestinationId}",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.PlanningWorkflows.Add(workflow);
        await _db.SaveChangesAsync(ct);

        try
        {

        var recommendationId = Guid.NewGuid();
        var candidates = new List<RecommendationCandidateDto>();
        var uncertaintyNotes = new List<string>();
        var verificationUnavailable = false;

        // These are independent peers: both calls are bounded and convert peer
        // failures into an unavailable result, so the planning workflow can still
        // complete and report which evidence was missing.
        var catalogueTask = _peerClient.GetCatalogueOfferingsAsync(
            request.TargetDestinationId, request.PreferredActivityIds, ct);
        var operationsTask = _peerClient.GetOperationalStatusAsync(request.TargetDestinationId, ct);
        await Task.WhenAll(catalogueTask, operationsTask);

        var (catalogueItems, catalogueResponded, catalogueNote) = await catalogueTask;
        var (opStatus, opResponded, opNote) = await operationsTask;
        AddNote(uncertaintyNotes, catalogueNote);
        AddNote(uncertaintyNotes, opNote);
        verificationUnavailable = !catalogueResponded || !opResponded;

        var eligibleOfferings = catalogueResponded
            ? catalogueItems.Where(item => item.DestinationId == request.TargetDestinationId &&
                item.ActivityId != Guid.Empty && item.OfferingId is { } offeringId && offeringId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(item.Title) &&
                string.Equals(item.PublicationState, "PUBLISHED", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.AvailabilityStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase) &&
                (request.PreferredActivityIds is not { Count: > 0 } || request.PreferredActivityIds.Contains(item.ActivityId)))
                .ToList()
            : [];

        if (catalogueResponded && eligibleOfferings.Count == 0)
        {
            AddNote(uncertaintyNotes, catalogueItems.Count == 0
                ? "The catalogue responded but returned no offerings for the requested destination."
                : "No catalogue offerings met the published, available, destination, and preference requirements.");
        }

        foreach (var item in eligibleOfferings)
        {
            if (!opResponded || opStatus is null || opStatus.DestinationId != request.TargetDestinationId)
            {
                continue;
            }

            if (opStatus.OperationalStatus is not ("OPEN" or "CAUTION"))
            {
                AddNote(uncertaintyNotes, $"Offering {item.ActivityId} was omitted because operations status is {opStatus.OperationalStatus}.");
                continue;
            }

            if (item.AvailableFrom is not { Kind: DateTimeKind.Utc } availableFrom ||
                item.AvailableUntil is not { Kind: DateTimeKind.Utc } availableUntil || availableUntil <= availableFrom ||
                item.CheckedAt is not { Kind: DateTimeKind.Utc } checkedAt || checkedAt < DateTime.UtcNow.AddMinutes(-15) ||
                checkedAt > DateTime.UtcNow.AddMinutes(1) || !ValidTimeZone(item.TimeZone))
            {
                AddNote(uncertaintyNotes, "Some experiences were left out because their schedule or availability could not be verified.");
                verificationUnavailable = true;
                continue;
            }
            if (item.ExperienceLevels is not { Count: > 0 } ||
                !item.ExperienceLevels.Contains(request.ExperienceLevel ?? "INTERMEDIATE", StringComparer.OrdinalIgnoreCase))
            {
                AddNote(uncertaintyNotes, "Some experiences do not support your selected experience level.");
                continue;
            }
            var start = request.StartsAt > availableFrom ? request.StartsAt : availableFrom;
            var end = start.AddHours(request.DurationHours);
            if (end > request.EndsAt || end > availableUntil) continue;
            var (suitability, suitResponded, suitNote) = await _peerClient.GetMarineSuitabilityAsync(
                item.DestinationId, item.ActivityId, start, end, ct);
            AddNote(uncertaintyNotes, suitNote);

            if (!suitResponded || suitability is null ||
                suitability.DestinationId != item.DestinationId || suitability.ActivityId != item.ActivityId ||
                suitability.Status is not ("SUITABLE" or "CAUTION") ||
                suitability.SafetyProfileId.GetValueOrDefault() == Guid.Empty ||
                suitability.ConditionTimestamp == default ||
                suitability.ConditionTimestamp.Kind != DateTimeKind.Utc ||
                !suitability.IsFresh || suitability.ConditionTimestamp > end)
            {
                verificationUnavailable |= !suitResponded || suitability?.Status is not ("UNSUITABLE");
                if (suitResponded && suitability?.Status == "UNSUITABLE")
                {
                    AddNote(uncertaintyNotes, $"Offering {item.ActivityId} was omitted because marine suitability is UNSUITABLE.");
                }
                else if (suitResponded && suitability is not null)
                {
                    AddNote(uncertaintyNotes, $"Offering {item.ActivityId} was omitted because required marine suitability evidence is incomplete or unknown.");
                }

                continue;
            }

            BiodiversityContextDto? bioContext = null;
            if (request.IncludeBiodiversityContext)
            {
                var bioResult = await GetBiodiversityPredictionsAsync(item.DestinationId, item.ActivityId, ct);
                if (bioResult.Status != "AVAILABLE") AddNote(uncertaintyNotes, bioResult.Limitations);
                if (bioResult.Status == "AVAILABLE" && bioResult.PredictedSpecies.FirstOrDefault() is { } topSpecies &&
                    bioResult.ModelMetadata?.InferenceTimestamp is { } timestamp)
                {
                    bioContext = new BiodiversityContextDto(
                        SpeciesName: $"{topSpecies.ScientificName} ({topSpecies.CommonName})",
                        Probability: topSpecies.HabitatSuitability,
                        Uncertainty: topSpecies.ConfidenceLevel,
                        PredictionTimestamp: timestamp);
                }
            }

            var reasons = new List<string>
            {
                "Published and available in the requested destination.",
                suitability.Status == "SUITABLE"
                    ? "Marine conditions are suitable for the requested time window."
                    : "Marine conditions include a caution for the requested time window.",
                opStatus.OperationalStatus == "OPEN"
                    ? "Coastal operations are open."
                    : "Coastal operations report a caution."
            };
            if (request.PreferredActivityIds?.Contains(item.ActivityId) == true)
            {
                reasons.Add("Matches a preferred activity.");
            }

            candidates.Add(new RecommendationCandidateDto(
                DestinationId: item.DestinationId,
                ActivityId: item.ActivityId,
                OfferingId: item.OfferingId,
                Title: item.Title,
                ScheduledStart: start,
                ScheduledEnd: end,
                AvailabilityStatus: item.AvailabilityStatus,
                Suitability: new SuitabilitySummaryDto(
                    Status: suitability.Status,
                    MarineConditionTime: suitability.ConditionTimestamp,
                    SafetyProfileId: suitability.SafetyProfileId),
                OperationalStatus: opStatus.OperationalStatus,
                BiodiversityContext: bioContext,
                FitScore: suitability.Status == "SUITABLE" && opStatus.OperationalStatus == "OPEN" ? 0.95 : 0.70,
                Reasons: reasons,
                TimeZone: item.TimeZone!));
        }

        if (eligibleOfferings.Count > 0 && !opResponded)
        {
            AddNote(uncertaintyNotes, "No recommendation candidates were returned because the operations endpoint did not respond; operating restrictions could not be verified.");
        }

        if (eligibleOfferings.Count > 0 && catalogueResponded && opResponded && candidates.Count == 0)
        {
            AddNote(uncertaintyNotes, "No recommendation candidates passed the available operations and marine safety checks.");
        }

        candidates = candidates.OrderByDescending(c => c.FitScore).ThenBy(c => c.ScheduledStart)
            .ThenBy(c => c.Title, StringComparer.Ordinal).ThenBy(c => c.OfferingId).ToList();
        var candidatesJson = JsonSerializer.Serialize(candidates);

        var session = new RecommendationSession
        {
            RecommendationId = recommendationId,
            Outcome = candidates.Count > 0 ? "MATCHES_FOUND" : verificationUnavailable ? "DEPENDENCIES_UNAVAILABLE" : "NO_MATCHES",
            WorkflowId = workflow.WorkflowId,
            UserId = userId,
            TargetDestinationId = request.TargetDestinationId,
            StartsAtUtc = request.StartsAt,
            EndsAtUtc = request.EndsAt,
            DurationHours = request.DurationHours,
            ExperienceLevel = request.ExperienceLevel ?? "INTERMEDIATE",
            IncludeBiodiversityContext = request.IncludeBiodiversityContext,
            CandidatesJson = candidatesJson,
            UncertaintyNotesJson = JsonSerializer.Serialize(uncertaintyNotes),
            ExcludedCandidatesCount = catalogueItems.Count - candidates.Count,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Recommendations.Add(session);

        workflow.Status = "COMPLETED";
        workflow.CompletedAtUtc = DateTime.UtcNow;
        workflow.ResultSummary = $"Generated {candidates.Count} recommendations with {uncertaintyNotes.Count} peer dependency warnings.";
        
        await _db.SaveChangesAsync(ct);

        return new RecommendationResultDto(
            RecommendationId: recommendationId,
            WorkflowId: workflow.WorkflowId,
            Status: "COMPLETED",
            GeneratedAt: session.CreatedAtUtc,
            Candidates: candidates,
            ExcludedCandidatesCount: session.ExcludedCandidatesCount,
            UncertaintyNotes: uncertaintyNotes,
            Outcome: session.Outcome
        );
        }
        catch (Exception ex)
        {
            // A caller disconnect must not leave a durable business workflow processing forever.
            _db.ChangeTracker.Clear();
            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                var failed = await _db.PlanningWorkflows.FindAsync([workflow.WorkflowId], recovery.Token);
                if (failed is not null)
                {
                    failed.Status = "FAILED";
                    failed.CompletedAtUtc = DateTime.UtcNow;
                    failed.FailureReason = ex is OperationCanceledException ? callerCancellation.IsCancellationRequested
                        ? "The request was cancelled. Please start a new search." : "The search timed out. Please try again."
                        : "The search could not be completed. Please try again.";
                    await _db.SaveChangesAsync(recovery.Token);
                }
            }
            catch (Exception recoveryError) { _logger.LogError(recoveryError, "Could not record failed planning workflow {WorkflowId}", workflow.WorkflowId); }
            throw;
        }
    }

    public async Task<RecommendationResultDto?> GetRecommendationAsync(Guid recommendationId, Guid ownerUserId, CancellationToken ct = default)
    {
        var session = await _db.Recommendations.FirstOrDefaultAsync(
            r => r.RecommendationId == recommendationId && r.UserId == ownerUserId, ct);
        if (session == null) return null;

        var candidates = string.IsNullOrWhiteSpace(session.CandidatesJson)
            ? new List<RecommendationCandidateDto>()
            : JsonSerializer.Deserialize<List<RecommendationCandidateDto>>(session.CandidatesJson) ?? new List<RecommendationCandidateDto>();

        return new RecommendationResultDto(
            RecommendationId: session.RecommendationId,
            WorkflowId: session.WorkflowId,
            Status: "COMPLETED",
            GeneratedAt: session.CreatedAtUtc,
            Candidates: candidates,
            ExcludedCandidatesCount: session.ExcludedCandidatesCount,
            UncertaintyNotes: DeserializeNotes(session.UncertaintyNotesJson),
            Outcome: session.Outcome
        );
    }

    public async Task<WorkflowStatusDto?> GetWorkflowStatusAsync(Guid workflowId, Guid ownerUserId, CancellationToken ct = default)
    {
        var workflow = await _db.PlanningWorkflows.FirstOrDefaultAsync(
            w => w.WorkflowId == workflowId && w.InitiatorUserId == ownerUserId, ct);
        if (workflow == null) return null;

        var ai = _coordinationClient is null ? new PlanningCoordinationAvailability("NOT_CONNECTED", false) :
            await _coordinationClient.CheckAvailabilityAsync(ct);
        return new WorkflowStatusDto(
            WorkflowId: workflow.WorkflowId,
            WorkflowType: workflow.WorkflowType,
            Status: workflow.Status,
            InitiatorUserId: workflow.InitiatorUserId,
            Objective: workflow.Objective,
            CreatedAt: workflow.CreatedAtUtc,
            CompletedAt: workflow.CompletedAtUtc,
            ResultSummary: workflow.ResultSummary,
            FailureReason: workflow.FailureReason,
            AiDependencyStatus: ai.Status
        );
    }

    public async Task<ItineraryDto> CreateItineraryAsync(CreateItineraryRequestDto request, Guid ownerUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateItinerary(ownerUserId, request.Title, request.Description, request.StartsAt, request.EndsAt, request.Items);
        await ValidateRecommendationSelectionAsync(request.RecommendationId, request.Items, ownerUserId, ct);
        if (!ValidTimeZone(request.TimeZone) || request.Items.Any(i => !ValidTimeZone(i.TimeZone)))
            throw new ArgumentException("Choose a supported destination time zone.");

        var itinerary = new Itinerary
        {
            ItineraryId = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            StartsAtUtc = request.StartsAt,
            EndsAtUtc = request.EndsAt,
            TimeZone = request.TimeZone,
            ConcurrencyVersion = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Items = request.Items.Select(item => new ItineraryItem
            {
                ItemId = Guid.NewGuid(),
                DestinationId = item.DestinationId,
                ActivityId = item.ActivityId,
                OfferingId = item.OfferingId,
                TimeZone = item.TimeZone,
                Title = item.Title,
                OrderIndex = item.OrderIndex,
                ScheduledStartUtc = item.ScheduledStart,
                ScheduledEndUtc = item.ScheduledEnd,
                LastSuitabilityStatus = "UNKNOWN",
                LastAvailabilityStatus = "UNKNOWN",
                LastOperationalStatus = "UNKNOWN"
            }).ToList()
        };

        _db.Itineraries.Add(itinerary);
        await _db.SaveChangesAsync(ct);

        return MapItineraryToDto(itinerary);
    }

    public async Task<List<ItineraryDto>> ListItinerariesAsync(Guid ownerUserId, int page, int pageSize, CancellationToken ct = default)
    {
        var itineraries = await _db.Itineraries
            .AsNoTracking()
            .Include(i => i.Items)
            .Where(i => i.OwnerUserId == ownerUserId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip((Math.Clamp(page, 1, 10000) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return itineraries.Select(MapItineraryToDto).ToList();
    }

    public async Task<ItineraryDto?> GetItineraryAsync(Guid itineraryId, Guid ownerUserId, CancellationToken ct = default)
    {
        var itinerary = await _db.Itineraries
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.ItineraryId == itineraryId && i.OwnerUserId == ownerUserId, ct);

        return itinerary == null ? null : MapItineraryToDto(itinerary);
    }

    public async Task<ItineraryDto?> UpdateItineraryAsync(Guid itineraryId, UpdateItineraryRequestDto request, Guid ownerUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateItinerary(ownerUserId, request.Title, request.Description, request.StartsAt, request.EndsAt, request.Items);
        var itinerary = await _db.Itineraries
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.ItineraryId == itineraryId && i.OwnerUserId == ownerUserId, ct);

        if (itinerary == null) return null;

        if (itinerary.ConcurrencyVersion != request.ConcurrencyVersion)
        {
            throw new DbUpdateConcurrencyException("Itinerary was modified concurrently.");
        }

        if (request.Items.Any(i => i.ItemId.HasValue && !itinerary.Items.Any(old => old.ItemId == i.ItemId &&
            old.DestinationId == i.DestinationId && old.ActivityId == i.ActivityId && old.OfferingId == i.OfferingId)))
            throw new ArgumentException("A stop reference must belong to this trip and experience.");
        var additions = request.Items.Where(i => !i.ItemId.HasValue && !itinerary.Items.Any(old =>
            old.DestinationId == i.DestinationId && old.ActivityId == i.ActivityId && old.OfferingId == i.OfferingId)).ToList();
        await ValidateRecommendationSelectionAsync(request.RecommendationId, additions, ownerUserId, ct);
        if (!ValidTimeZone(request.TimeZone) || request.Items.Any(i => !ValidTimeZone(i.TimeZone)))
            throw new ArgumentException("Choose a supported destination time zone.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            itinerary = await _db.Itineraries.Include(i => i.Items).SingleAsync(i => i.ItineraryId == itineraryId && i.OwnerUserId == ownerUserId, ct);
            if (itinerary.ConcurrencyVersion != request.ConcurrencyVersion) throw new DbUpdateConcurrencyException("Trip changed.");
            await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
            itinerary.Title = request.Title.Trim();
            itinerary.Description = request.Description?.Trim();
            itinerary.StartsAtUtc = request.StartsAt;
            itinerary.EndsAtUtc = request.EndsAt;
            itinerary.TimeZone = request.TimeZone;
            itinerary.ConcurrencyVersion++;
            itinerary.UpdatedAtUtc = DateTime.UtcNow;
            // Move existing orders out of the final range before reordering: PostgreSQL's
            // unique itinerary/order index is checked after each UPDATE, not at commit.
            var occupiedOrders = itinerary.Items.Select(i => i.OrderIndex)
                .Concat(request.Items.Select(i => i.OrderIndex)).ToHashSet();
            var temporaryOrder = 0;
            foreach (var old in itinerary.Items)
            {
                while (occupiedOrders.Contains(temporaryOrder)) temporaryOrder++;
                old.OrderIndex = temporaryOrder;
                occupiedOrders.Add(temporaryOrder++);
            }
            await _db.SaveChangesAsync(ct);
            var existing = itinerary.Items.ToList();
            var replacement = new List<ItineraryItem>();
            foreach (var item in request.Items)
            {
                var stop = item.ItemId.HasValue ? existing.Single(i => i.ItemId == item.ItemId) :
                    existing.FirstOrDefault(i => !replacement.Contains(i) && i.DestinationId == item.DestinationId &&
                        i.ActivityId == item.ActivityId && i.OfferingId == item.OfferingId);
                if (stop is null)
                {
                    stop = new ItineraryItem { ItemId = Guid.NewGuid(), ItineraryId = itinerary.ItineraryId,
                        DestinationId = item.DestinationId, ActivityId = item.ActivityId, OfferingId = item.OfferingId };
                    _db.ItineraryItems.Add(stop);
                }
                if (stop.ScheduledStartUtc != item.ScheduledStart || stop.ScheduledEndUtc != item.ScheduledEnd)
                {
                    stop.LastSuitabilityStatus = stop.LastAvailabilityStatus = stop.LastOperationalStatus = "UNKNOWN";
                    stop.AdvisoryNote = null;
                }
                stop.Title = item.Title.Trim();
                stop.TimeZone = item.TimeZone;
                stop.OrderIndex = item.OrderIndex;
                stop.ScheduledStartUtc = item.ScheduledStart;
                stop.ScheduledEndUtc = item.ScheduledEnd;
                replacement.Add(stop);
            }
            _db.ItineraryItems.RemoveRange(existing.Except(replacement));
            itinerary.Items = replacement;
            await _db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return MapItineraryToDto(itinerary);
        });
    }

    public async Task<bool> DeleteItineraryAsync(Guid itineraryId, Guid ownerUserId, CancellationToken ct = default)
    {
        var itinerary = await _db.Itineraries
            .FirstOrDefaultAsync(i => i.ItineraryId == itineraryId && i.OwnerUserId == ownerUserId, ct);

        if (itinerary == null) return false;

        _db.Itineraries.Remove(itinerary);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ItineraryReEvaluationResultDto?> ReEvaluateItineraryAsync(
        Guid itineraryId, 
        Guid ownerUserId, 
        ItineraryReEvaluationRequestDto request, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ReEvaluationMode != "FULL_ASSESSMENT") throw new ArgumentException("Only full condition review is supported.");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(40));
        ct = budget.Token;
        var itinerary = await _db.Itineraries
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.ItineraryId == itineraryId && i.OwnerUserId == ownerUserId, ct);

        if (itinerary == null) return null;

        var itemsResult = new List<ItineraryReEvaluationItemDto>();
        var hasChanges = false;
        var requiresReview = false;
        var summaryNotes = new List<string>();
        var destinationEvidence = new Dictionary<Guid, (
            PeerOperationStatusResponse? Operations,
            bool OperationsResponded,
            List<PeerCatalogueItem> Catalogue,
            bool CatalogueResponded)>();

        foreach (var destinationId in itinerary.Items.Select(item => item.DestinationId).Distinct())
        {
            var preferredActivityIds = itinerary.Items
                .Where(item => item.DestinationId == destinationId)
                .Select(item => item.ActivityId)
                .Distinct()
                .ToList();
            var operationsTask = _peerClient.GetOperationalStatusAsync(destinationId, ct);
            var catalogueTask = _peerClient.GetCatalogueOfferingsAsync(destinationId, preferredActivityIds, ct);
            await Task.WhenAll(operationsTask, catalogueTask);

            var (operations, operationsResponded, operationsNote) = await operationsTask;
            var (catalogue, catalogueResponded, catalogueNote) = await catalogueTask;
            AddNote(summaryNotes, operationsNote);
            AddNote(summaryNotes, catalogueNote);
            destinationEvidence[destinationId] = (
                operations, operationsResponded, catalogue, catalogueResponded);
        }

        foreach (var item in itinerary.Items)
        {
            var (suitability, suitResponded, suitNote) = await _peerClient.GetMarineSuitabilityAsync(
                item.DestinationId, 
                item.ActivityId, 
                item.ScheduledStartUtc, 
                item.ScheduledEndUtc, 
                ct);
            AddNote(summaryNotes, suitNote);

            var (operations, operationsResponded, catalogue, catalogueResponded) = destinationEvidence[item.DestinationId];
            var catalogueItem = catalogue.FirstOrDefault(candidate => candidate.DestinationId == item.DestinationId &&
                candidate.ActivityId == item.ActivityId &&
                (item.OfferingId.HasValue ? candidate.OfferingId == item.OfferingId : candidate.ActivityId == item.ActivityId));
            var currentSuit = suitResponded && suitability is not null &&
                suitability.DestinationId == item.DestinationId && suitability.ActivityId == item.ActivityId &&
                suitability.Status is ("SUITABLE" or "CAUTION" or "UNSUITABLE" or "UNKNOWN") &&
                suitability.ConditionTimestamp != default && suitability.ConditionTimestamp.Kind == DateTimeKind.Utc &&
                suitability.ConditionTimestamp <= item.ScheduledEndUtc &&
                (suitability.Status is "UNSUITABLE" or "UNKNOWN" || suitability.IsFresh) &&
                (suitability.Status is "UNSUITABLE" or "UNKNOWN" || suitability.SafetyProfileId.GetValueOrDefault() != Guid.Empty)
                ? suitability.Status
                : "UNKNOWN";
            var currentOp = operationsResponded && operations is not null && operations.DestinationId == item.DestinationId &&
                operations.OperationalStatus is ("OPEN" or "CAUTION" or "TEMPORARILY_SUSPENDED" or "CANCELLED" or "COMPLETED")
                ? operations.OperationalStatus
                : "UNKNOWN";
            var currentAvail = catalogueResponded && catalogueItem is not null
                ? string.Equals(catalogueItem.PublicationState, "PUBLISHED", StringComparison.OrdinalIgnoreCase)
                    ? catalogueItem.AvailabilityStatus is ("AVAILABLE" or "UNAVAILABLE" or "UNKNOWN")
                        ? catalogueItem.AvailabilityStatus
                        : "UNKNOWN"
                    : "NOT_PUBLISHED"
                : "UNKNOWN";
            if (currentAvail == "AVAILABLE")
            {
                if (catalogueItem?.CheckedAt is not { Kind: DateTimeKind.Utc } checkedAt ||
                    checkedAt < DateTime.UtcNow.AddMinutes(-15) || checkedAt > DateTime.UtcNow.AddMinutes(1) ||
                    catalogueItem.AvailableFrom is not { Kind: DateTimeKind.Utc } availableFrom ||
                    catalogueItem.AvailableUntil is not { Kind: DateTimeKind.Utc } availableUntil)
                    currentAvail = "UNKNOWN";
                else if (item.ScheduledStartUtc < availableFrom || item.ScheduledEndUtc > availableUntil)
                    currentAvail = "UNAVAILABLE";
            }

            if (!catalogueResponded)
            {
                AddNote(summaryNotes, $"Catalogue availability for destination {item.DestinationId} could not be verified.");
            }
            else if (catalogueItem is null)
            {
                AddNote(summaryNotes, $"The saved offering for activity {item.ActivityId} was not returned by the catalogue.");
            }

            string? advisory = currentSuit == "UNKNOWN" ? null : suitability?.Advisory;
            string action = "KEEP";

            if (currentSuit == "UNSUITABLE" || currentOp is "TEMPORARILY_SUSPENDED" or "CANCELLED" or "COMPLETED" ||
                currentAvail is not ("AVAILABLE" or "UNKNOWN"))
            {
                action = "CANCEL_OR_RESCHEDULE";
                advisory ??= "The activity is unsuitable, unavailable, unpublished, or restricted by current operations.";
                requiresReview = true;
            }
            else if (currentSuit == "UNKNOWN" || currentOp == "UNKNOWN" || currentAvail == "UNKNOWN")
            {
                action = "REVIEW_CONDITIONS";
                advisory ??= "Current safety, availability, or operational information is unavailable. Review conditions before proceeding.";
                requiresReview = true;
            }
            else if (currentSuit == "CAUTION" || currentOp == "CAUTION")
            {
                action = "REVIEW_CONDITIONS";
                advisory ??= "Elevated risk caution present for the planned activity window.";
                requiresReview = true;
            }

            var previousAvailability = item.LastAvailabilityStatus;
            var previousSuitability = item.LastSuitabilityStatus;
            var previousOperations = item.LastOperationalStatus;
            hasChanges |= previousAvailability != currentAvail || previousSuitability != currentSuit || previousOperations != currentOp;
            item.LastSuitabilityStatus = currentSuit;
            item.LastOperationalStatus = currentOp;
            item.LastAvailabilityStatus = currentAvail;
            item.AdvisoryNote = advisory;

            itemsResult.Add(new ItineraryReEvaluationItemDto(
                ItemId: item.ItemId,
                OfferingId: item.OfferingId,
                CurrentAvailability: currentAvail,
                CurrentSuitability: currentSuit,
                CurrentOperationalStatus: currentOp,
                AdvisoryMessage: advisory,
                SuggestedAction: action,
                PreviousAvailability: previousAvailability,
                PreviousSuitability: previousSuitability,
                PreviousOperationalStatus: previousOperations,
                MarineConditionTime: currentSuit == "UNKNOWN" ? null : suitability?.ConditionTimestamp,
                SafetyProfileId: currentSuit == "UNKNOWN" ? null : suitability?.SafetyProfileId
            ));
        }

        var summary = itinerary.Items.Count == 0 ? "There are no experiences in this trip to review." : requiresReview
            ? "Re-evaluation identified advisories or condition changes requiring review." 
            : "All planned items remain suitable based on the available evidence.";

        if (summaryNotes.Any())
        {
            summary += $" (Warnings: {string.Join("; ", summaryNotes)})";
        }

        itinerary.UpdatedAtUtc = DateTime.UtcNow;
        itinerary.ConcurrencyVersion++;
        var result = new ItineraryReEvaluationResultDto(
            ItineraryId: itinerary.ItineraryId,
            EvaluatedAt: DateTime.UtcNow,
            HasChanges: hasChanges,
            Summary: summary,
            Items: itemsResult,
            EvaluationId: Guid.NewGuid(),
            RequiresReview: requiresReview,
            ConcurrencyVersion: itinerary.ConcurrencyVersion
        );
        _db.ItineraryEvaluations.Add(new ItineraryEvaluation { EvaluationId = result.EvaluationId,
            ItineraryId = itinerary.ItineraryId, EvaluatedAtUtc = result.EvaluatedAt, ResultJson = JsonSerializer.Serialize(result) });
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<BiodiversityPredictionDto> GetBiodiversityPredictionsAsync(
        Guid destinationId, 
        Guid? activityId, 
        CancellationToken ct = default)
    {
        var cache = await _db.BiodiversityPredictions
            .OrderByDescending(p => p.InferenceTimestampUtc)
            .FirstOrDefaultAsync(p => p.DestinationId == destinationId && p.ActivityId == activityId && p.ExpiresAtUtc > DateTime.UtcNow, ct);

        if (cache != null && !string.IsNullOrWhiteSpace(cache.SpeciesDataJson))
        {
            try
            {
            var species = JsonSerializer.Deserialize<List<PredictedSpeciesDto>>(cache.SpeciesDataJson);
            if (cache.Status == "AVAILABLE" && cache.InferenceTimestampUtc >= DateTime.UtcNow.AddHours(-6) &&
                cache.InferenceTimestampUtc <= DateTime.UtcNow.AddMinutes(1) && !string.IsNullOrWhiteSpace(cache.ModelVersion) &&
                species is not null && species.All(s => s is not null && s.SpeciesId != Guid.Empty &&
                    !string.IsNullOrWhiteSpace(s.ScientificName) && !string.IsNullOrWhiteSpace(s.CommonName) &&
                    double.IsFinite(s.HabitatSuitability) && s.HabitatSuitability is >= 0 and <= 1 &&
                    !string.IsNullOrWhiteSpace(s.ConfidenceLevel))) return new BiodiversityPredictionDto(
                DestinationId: cache.DestinationId,
                ActivityId: cache.ActivityId,
                Status: cache.Status,
                PredictedSpecies: species,
                ModelMetadata: new ModelMetadataDto(cache.ModelVersion, cache.InferenceTimestampUtc),
                Limitations: cache.Limitations ?? "Contextual prediction only."
            );
            }
            catch (JsonException) { _logger.LogWarning("A cached biodiversity prediction was unreadable."); }
            _db.BiodiversityPredictions.Remove(cache);
            await _db.SaveChangesAsync(ct);
        }

        // Call IT3091 ML inference via peer client
        var (peerBio, bioResponded, bioNote) = await _peerClient.GetBiodiversityInferenceAsync(destinationId, activityId, ct);

        var now = DateTime.UtcNow;
        if (!bioResponded || peerBio is null || peerBio.Timestamp is null || peerBio.DestinationId != destinationId || peerBio.ActivityId != activityId ||
            string.IsNullOrWhiteSpace(peerBio.ModelVersion) || peerBio.ModelVersion.Length > 64 || peerBio.Status != "AVAILABLE" ||
            peerBio.Species is null || peerBio.Species.Any(species =>
                species is null || species.SpeciesId == Guid.Empty || string.IsNullOrWhiteSpace(species.ScientificName) ||
                species.ScientificName.Length > 200 || string.IsNullOrWhiteSpace(species.CommonName) || species.CommonName.Length > 200 ||
                !double.IsFinite(species.HabitatSuitability) ||
                species.HabitatSuitability is < 0 or > 1 || string.IsNullOrWhiteSpace(species.ConfidenceLevel) || species.ConfidenceLevel.Length > 64) ||
            peerBio.Timestamp.Value.Kind != DateTimeKind.Utc || peerBio.Timestamp.Value < now.AddHours(-6) ||
            peerBio.Timestamp.Value > now.AddMinutes(1))
        {
            _logger.LogWarning("Biodiversity inference is unavailable: {Note}", bioNote);
            return new BiodiversityPredictionDto(
                DestinationId: destinationId,
                ActivityId: activityId,
                Status: "UNAVAILABLE",
                PredictedSpecies: [],
                ModelMetadata: null,
                Limitations: bioNote ?? "Biodiversity inference endpoint did not respond; no prediction is available.");
        }

        var predictedSpecies = peerBio.Species?.Select(species => new PredictedSpeciesDto(
            SpeciesId: species.SpeciesId,
            ScientificName: species.ScientificName,
            CommonName: species.CommonName,
            HabitatSuitability: species.HabitatSuitability,
            ConfidenceLevel: species.ConfidenceLevel)).ToList() ?? [];
        var modelVersion = peerBio.ModelVersion;
        var limitations = peerBio.Limitations ?? "Contextual prediction only. Not a guarantee of wildlife sighting or site safety.";

        var newCache = new BiodiversityPredictionCache
        {
            PredictionId = Guid.NewGuid(),
            DestinationId = destinationId,
            ActivityId = activityId,
            Status = peerBio.Status,
            SpeciesDataJson = JsonSerializer.Serialize(predictedSpecies),
            ModelVersion = modelVersion,
            InferenceTimestampUtc = peerBio.Timestamp.Value,
            ExpiresAtUtc = peerBio.Timestamp.Value.AddHours(6),
            Limitations = limitations
        };

        _db.BiodiversityPredictions.Add(newCache);
        await _db.SaveChangesAsync(ct);

        return new BiodiversityPredictionDto(
            DestinationId: destinationId,
            ActivityId: activityId,
            Status: peerBio.Status,
            PredictedSpecies: predictedSpecies,
            ModelMetadata: new ModelMetadataDto(modelVersion, newCache.InferenceTimestampUtc),
            Limitations: limitations
        );
    }

    private static ItineraryDto MapItineraryToDto(Itinerary i)
    {
        return new ItineraryDto(
            ItineraryId: i.ItineraryId,
            OwnerUserId: i.OwnerUserId,
            Title: i.Title,
            Description: i.Description,
            StartsAt: i.StartsAtUtc,
            EndsAt: i.EndsAtUtc,
            ConcurrencyVersion: i.ConcurrencyVersion,
            CreatedAt: i.CreatedAtUtc,
            UpdatedAt: i.UpdatedAtUtc,
            Items: i.Items.OrderBy(item => item.OrderIndex).Select(item => new ItineraryItemDto(
                ItemId: item.ItemId,
                ItineraryId: item.ItineraryId,
                DestinationId: item.DestinationId,
                ActivityId: item.ActivityId,
                OfferingId: item.OfferingId,
                Title: item.Title,
                OrderIndex: item.OrderIndex,
                ScheduledStart: item.ScheduledStartUtc,
                ScheduledEnd: item.ScheduledEndUtc,
                LastSuitabilityStatus: item.LastSuitabilityStatus,
                LastAvailabilityStatus: item.LastAvailabilityStatus,
                LastOperationalStatus: item.LastOperationalStatus,
                AdvisoryNote: item.AdvisoryNote,
                TimeZone: item.TimeZone
            )).ToList(),
            TimeZone: i.TimeZone
        );
    }

    private static void ValidateRecommendationRequest(RecommendationRequestDto request, Guid? userId)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("An authenticated actor is required.");
        }

        if (request.TargetDestinationId == Guid.Empty)
        {
            throw new ArgumentException("TargetDestinationId must be a non-empty GUID.");
        }

        if (request.StartsAt.Kind != DateTimeKind.Utc || request.EndsAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("StartsAt and EndsAt must be UTC timestamps.");
        }

        if (request.EndsAt <= request.StartsAt)
        {
            throw new ArgumentException("EndsAt must be after StartsAt.");
        }

        if (request.DurationHours <= 0 || request.DurationHours > (request.EndsAt - request.StartsAt).TotalHours)
        {
            throw new ArgumentException("DurationHours must be positive and fit within the requested time range.");
        }

        var now = DateTime.UtcNow;
        if (request.StartsAt < now || request.EndsAt > now.AddDays(30))
        {
            throw new ArgumentException("Planning dates must be in the future and within the next 30 days.");
        }

        if (request.PreferredActivityIds?.Any(id => id == Guid.Empty) == true)
        {
            throw new ArgumentException("PreferredActivityIds cannot contain an empty GUID.");
        }
        if (request.ExperienceLevel is not (null or "BEGINNER" or "INTERMEDIATE" or "ADVANCED") ||
            request.PreferredActivityIds is { Count: > 100 } ||
            request.PreferredActivityIds?.Distinct().Count() != request.PreferredActivityIds?.Count)
            throw new ArgumentException("Choose a supported experience level and at most 100 distinct activities.");
    }

    private static void ValidateItinerary(
        Guid ownerUserId,
        string title,
        string? description,
        DateTime startsAt,
        DateTime endsAt,
        List<CreateItineraryItemRequestDto>? items)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("An authenticated owner is required.");
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > 150 || (description?.Length ?? 0) > 500)
        {
            throw new ArgumentException("Title is required and must not exceed 150 characters; description must not exceed 500 characters.");
        }

        if (startsAt.Kind != DateTimeKind.Utc || endsAt.Kind != DateTimeKind.Utc || endsAt <= startsAt)
        {
            throw new ArgumentException("Itinerary start and end must be UTC timestamps with the end after the start.");
        }

        if (items is null)
        {
            throw new ArgumentException("Items are required.");
        }

        var now = DateTime.UtcNow;
        if (startsAt < now || endsAt > now.AddDays(30))
        {
            throw new ArgumentException("Itinerary dates must be in the future and within the next 30 days.");
        }

        if (items.Any(item => item is null || item.DestinationId == Guid.Empty || item.ActivityId == Guid.Empty ||
                item.OfferingId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 150 || item.OrderIndex < 0 ||
                item.ScheduledStart.Kind != DateTimeKind.Utc || item.ScheduledEnd.Kind != DateTimeKind.Utc ||
                item.ScheduledEnd <= item.ScheduledStart || item.ScheduledStart < startsAt || item.ScheduledEnd > endsAt))
        {
            throw new ArgumentException("Each itinerary item must have valid IDs, a title, a unique non-negative order, and a UTC schedule inside the itinerary range.");
        }

        if (items.Select(item => item.OrderIndex).Distinct().Count() != items.Count)
        {
            throw new ArgumentException("Itinerary item order values must be unique.");
        }
        if (items.Count > 50 || items.Where(i => i.ItemId.HasValue).Select(i => i.ItemId).Distinct().Count() != items.Count(i => i.ItemId.HasValue) ||
            items.Select(i => (i.DestinationId, i.ActivityId, i.OfferingId, i.ScheduledStart)).Distinct().Count() != items.Count)
            throw new ArgumentException("A trip supports at most 50 stops. Duplicate stops and item references are not allowed.");
        var scheduled = items.OrderBy(i => i.ScheduledStart).ToList();
        if (scheduled.Zip(scheduled.Skip(1)).Any(pair => pair.First.ScheduledEnd > pair.Second.ScheduledStart))
            throw new ArgumentException("Stops cannot overlap. Choose separate times for each experience.");
    }

    private static void AddNote(List<string> notes, string? note)
    {
        if (!string.IsNullOrWhiteSpace(note) && !notes.Contains(note, StringComparer.Ordinal))
        {
            notes.Add(note);
        }
    }
    private static bool ValidTimeZone(string? zone) => !string.IsNullOrWhiteSpace(zone) && zone.Length <= 100 &&
        TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _);

    private static List<string> DeserializeNotes(string? notesJson)
    {
        if (string.IsNullOrWhiteSpace(notesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(notesJson) ?? [];
        }
        catch (JsonException)
        {
            return ["Stored uncertainty notes could not be read."];
        }
    }
}
