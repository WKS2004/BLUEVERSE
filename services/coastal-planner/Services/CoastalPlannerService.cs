using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Data.Entities;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Models.Dtos;

namespace Blueverse.CoastalPlanner.Services;

public class CoastalPlannerService : ICoastalPlannerService
{
    private readonly CoastalPlannerDbContext _db;
    private readonly IPeerServicesClient _peerClient;
    private readonly ILogger<CoastalPlannerService> _logger;

    public CoastalPlannerService(
        CoastalPlannerDbContext db, 
        IPeerServicesClient peerClient,
        ILogger<CoastalPlannerService> logger)
    {
        _db = db;
        _peerClient = peerClient;
        _logger = logger;
    }

    public async Task<RecommendationResultDto> GenerateRecommendationsAsync(
        RecommendationRequestDto request, 
        Guid? userId, 
        CancellationToken ct = default)
    {
        ValidateRecommendationRequest(request, userId);

        var workflow = new PlanningWorkflow
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowType = "TOURIST_RECOMMENDATION",
            Status = "PROCESSING",
            InitiatorUserId = userId!.Value,
            Objective = $"Coastal planning for destination {request.TargetDestinationId}",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.PlanningWorkflows.Add(workflow);
        await _db.SaveChangesAsync(ct);

        var recommendationId = Guid.NewGuid();
        var candidates = new List<RecommendationCandidateDto>();
        var uncertaintyNotes = new List<string>();

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

            var start = request.StartsAt;
            var end = start.AddHours(request.DurationHours);
            var (suitability, suitResponded, suitNote) = await _peerClient.GetMarineSuitabilityAsync(
                item.DestinationId, item.ActivityId, start, end, ct);
            AddNote(uncertaintyNotes, suitNote);

            if (!suitResponded || suitability is null ||
                suitability.DestinationId != item.DestinationId || suitability.ActivityId != item.ActivityId ||
                suitability.Status is not ("SUITABLE" or "CAUTION") ||
                suitability.SafetyProfileId.GetValueOrDefault() == Guid.Empty ||
                suitability.ConditionTimestamp == default ||
                suitability.ConditionTimestamp.Kind != DateTimeKind.Utc ||
                suitability.ConditionTimestamp > DateTime.UtcNow.AddMinutes(1))
            {
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
                var (bioResult, bioResponded, bioNote) = await _peerClient.GetBiodiversityInferenceAsync(
                    item.DestinationId, item.ActivityId, ct);
                AddNote(uncertaintyNotes, bioNote);
                if (bioResponded && bioResult?.Species?.FirstOrDefault() is { } topSpecies && bioResult.Timestamp is { } timestamp)
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
                Reasons: reasons));
        }

        if (eligibleOfferings.Count > 0 && !opResponded)
        {
            AddNote(uncertaintyNotes, "No recommendation candidates were returned because the operations endpoint did not respond; operating restrictions could not be verified.");
        }

        if (eligibleOfferings.Count > 0 && catalogueResponded && opResponded && candidates.Count == 0)
        {
            AddNote(uncertaintyNotes, "No recommendation candidates passed the available operations and marine safety checks.");
        }

        var candidatesJson = JsonSerializer.Serialize(candidates);

        var session = new RecommendationSession
        {
            RecommendationId = recommendationId,
            WorkflowId = workflow.WorkflowId,
            UserId = userId!.Value,
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
            UncertaintyNotes: uncertaintyNotes
        );
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
            UncertaintyNotes: DeserializeNotes(session.UncertaintyNotesJson)
        );
    }

    public async Task<WorkflowStatusDto?> GetWorkflowStatusAsync(Guid workflowId, Guid ownerUserId, CancellationToken ct = default)
    {
        var workflow = await _db.PlanningWorkflows.FirstOrDefaultAsync(
            w => w.WorkflowId == workflowId && w.InitiatorUserId == ownerUserId, ct);
        if (workflow == null) return null;

        return new WorkflowStatusDto(
            WorkflowId: workflow.WorkflowId,
            WorkflowType: workflow.WorkflowType,
            Status: workflow.Status,
            InitiatorUserId: workflow.InitiatorUserId,
            Objective: workflow.Objective,
            CreatedAt: workflow.CreatedAtUtc,
            CompletedAt: workflow.CompletedAtUtc,
            ResultSummary: workflow.ResultSummary,
            FailureReason: workflow.FailureReason
        );
    }

    public async Task<ItineraryDto> CreateItineraryAsync(CreateItineraryRequestDto request, Guid ownerUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateItinerary(ownerUserId, request.Title, request.Description, request.StartsAt, request.EndsAt, request.Items);

        var itinerary = new Itinerary
        {
            ItineraryId = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Title = request.Title,
            Description = request.Description,
            StartsAtUtc = request.StartsAt,
            EndsAtUtc = request.EndsAt,
            ConcurrencyVersion = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Items = request.Items.Select(item => new ItineraryItem
            {
                ItemId = Guid.NewGuid(),
                DestinationId = item.DestinationId,
                ActivityId = item.ActivityId,
                OfferingId = item.OfferingId,
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
            .Include(i => i.Items)
            .Where(i => i.OwnerUserId == ownerUserId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        itinerary.Title = request.Title;
        itinerary.Description = request.Description;
        itinerary.StartsAtUtc = request.StartsAt;
        itinerary.EndsAtUtc = request.EndsAt;
        itinerary.ConcurrencyVersion++;
        itinerary.UpdatedAtUtc = DateTime.UtcNow;

        _db.ItineraryItems.RemoveRange(itinerary.Items);
        itinerary.Items = request.Items.Select(item => new ItineraryItem
        {
            ItemId = Guid.NewGuid(),
            ItineraryId = itinerary.ItineraryId,
            DestinationId = item.DestinationId,
            ActivityId = item.ActivityId,
            OfferingId = item.OfferingId,
            Title = item.Title,
            OrderIndex = item.OrderIndex,
            ScheduledStartUtc = item.ScheduledStart,
            ScheduledEndUtc = item.ScheduledEnd,
            LastSuitabilityStatus = "UNKNOWN",
            LastAvailabilityStatus = "UNKNOWN",
            LastOperationalStatus = "UNKNOWN"
        }).ToList();

        await _db.SaveChangesAsync(ct);
        return MapItineraryToDto(itinerary);
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
        var itinerary = await _db.Itineraries
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.ItineraryId == itineraryId && i.OwnerUserId == ownerUserId, ct);

        if (itinerary == null) return null;

        var itemsResult = new List<ItineraryReEvaluationItemDto>();
        var hasChanges = false;
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
                (item.OfferingId.HasValue ? candidate.OfferingId == item.OfferingId : candidate.ActivityId == item.ActivityId));
            var currentSuit = suitResponded && suitability is not null &&
                suitability.DestinationId == item.DestinationId && suitability.ActivityId == item.ActivityId &&
                suitability.Status is ("SUITABLE" or "CAUTION" or "UNSUITABLE" or "UNKNOWN") &&
                suitability.ConditionTimestamp != default && suitability.ConditionTimestamp.Kind == DateTimeKind.Utc &&
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

            if (currentSuit == "UNSUITABLE" || currentOp is "TEMPORARILY_SUSPENDED" or "CANCELLED" ||
                currentAvail is not ("AVAILABLE" or "UNKNOWN"))
            {
                action = "CANCEL_OR_RESCHEDULE";
                advisory ??= "The activity is unsuitable, unavailable, unpublished, or restricted by current operations.";
                hasChanges = true;
            }
            else if (currentSuit == "UNKNOWN" || currentOp == "UNKNOWN" || currentAvail == "UNKNOWN")
            {
                action = "REVIEW_CONDITIONS";
                advisory ??= "Current safety, availability, or operational information is unavailable. Review conditions before proceeding.";
                hasChanges = true;
            }
            else if (currentSuit == "CAUTION" || currentOp == "CAUTION")
            {
                action = "REVIEW_CONDITIONS";
                advisory ??= "Elevated risk caution present for the planned activity window.";
                hasChanges = true;
            }

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
                SuggestedAction: action
            ));
        }

        if (itinerary.Items.Count > 0)
        {
            itinerary.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var summary = hasChanges 
            ? "Re-evaluation identified advisories or condition changes requiring review." 
            : "All planned items remain suitable based on the available evidence.";

        if (summaryNotes.Any())
        {
            summary += $" (Warnings: {string.Join("; ", summaryNotes)})";
        }

        return new ItineraryReEvaluationResultDto(
            ItineraryId: itinerary.ItineraryId,
            EvaluatedAt: DateTime.UtcNow,
            HasChanges: hasChanges,
            Summary: summary,
            Items: itemsResult
        );
    }

    public async Task<BiodiversityPredictionDto> GetBiodiversityPredictionsAsync(
        Guid destinationId, 
        Guid? activityId, 
        CancellationToken ct = default)
    {
        var cache = await _db.BiodiversityPredictions
            .FirstOrDefaultAsync(p => p.DestinationId == destinationId && p.ActivityId == activityId && p.ExpiresAtUtc > DateTime.UtcNow, ct);

        if (cache != null && !string.IsNullOrWhiteSpace(cache.SpeciesDataJson))
        {
            var species = JsonSerializer.Deserialize<List<PredictedSpeciesDto>>(cache.SpeciesDataJson) ?? new List<PredictedSpeciesDto>();
            return new BiodiversityPredictionDto(
                DestinationId: cache.DestinationId,
                ActivityId: cache.ActivityId,
                Status: cache.Status,
                PredictedSpecies: species,
                ModelMetadata: new ModelMetadataDto(cache.ModelVersion, cache.InferenceTimestampUtc),
                Limitations: cache.Limitations ?? "Contextual prediction only."
            );
        }

        // Call IT3091 ML inference via peer client
        var (peerBio, bioResponded, bioNote) = await _peerClient.GetBiodiversityInferenceAsync(destinationId, activityId, ct);

        var now = DateTime.UtcNow;
        if (!bioResponded || peerBio is null || peerBio.Timestamp is null ||
            string.IsNullOrWhiteSpace(peerBio.ModelVersion) || peerBio.ModelVersion.Length > 64 || peerBio.Status != "AVAILABLE" ||
            peerBio.Species is null || peerBio.Species.Any(species =>
                species is null || species.SpeciesId == Guid.Empty || string.IsNullOrWhiteSpace(species.ScientificName) ||
                species.ScientificName.Length > 200 || string.IsNullOrWhiteSpace(species.CommonName) || species.CommonName.Length > 200 ||
                !double.IsFinite(species.HabitatSuitability) ||
                species.HabitatSuitability is < 0 or > 1 || string.IsNullOrWhiteSpace(species.ConfidenceLevel)) ||
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
                AdvisoryNote: item.AdvisoryNote
            )).ToList()
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
    }

    private static void AddNote(List<string> notes, string? note)
    {
        if (!string.IsNullOrWhiteSpace(note) && !notes.Contains(note, StringComparer.Ordinal))
        {
            notes.Add(note);
        }
    }

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
