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
        if (request.EndsAt <= request.StartsAt)
        {
            throw new ArgumentException("EndsAt must be after StartsAt");
        }

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

        var recommendationId = Guid.NewGuid();
        var candidates = new List<RecommendationCandidateDto>();
        var uncertaintyNotes = new List<string>();

        // 1. Fetch catalogue offerings from Member 1
        var (catalogueItems, catalogueResponded, catalogueNote) = await _peerClient.GetCatalogueOfferingsAsync(
            request.TargetDestinationId, 
            request.PreferredActivityIds, 
            ct);

        if (!catalogueResponded && catalogueNote != null)
        {
            uncertaintyNotes.Add(catalogueNote);
        }

        // 2. Fetch coastal operational alerts from Member 4
        var (opStatus, opResponded, opNote) = await _peerClient.GetOperationalStatusAsync(request.TargetDestinationId, ct);
        if (!opResponded && opNote != null)
        {
            uncertaintyNotes.Add(opNote);
        }

        // 3. For each candidate item, fetch marine suitability from Member 2
        foreach (var item in catalogueItems)
        {
            var start = request.StartsAt.AddHours(1);
            var end = request.StartsAt.AddHours(3);

            var (suitability, suitResponded, suitNote) = await _peerClient.GetMarineSuitabilityAsync(
                item.DestinationId, 
                item.ActivityId, 
                start, 
                end, 
                ct);

            if (!suitResponded && suitNote != null && !uncertaintyNotes.Contains(suitNote))
            {
                uncertaintyNotes.Add(suitNote);
            }

            BiodiversityContextDto? bioContext = null;
            if (request.IncludeBiodiversityContext)
            {
                var (bioResult, bioResponded, bioNote) = await _peerClient.GetBiodiversityInferenceAsync(item.DestinationId, item.ActivityId, ct);
                if (bioResponded && bioResult?.Species?.Any() == true)
                {
                    var topSpecies = bioResult.Species.First();
                    bioContext = new BiodiversityContextDto(
                        SpeciesName: $"{topSpecies.ScientificName} ({topSpecies.CommonName})",
                        Probability: topSpecies.HabitatSuitability,
                        Uncertainty: topSpecies.ConfidenceLevel,
                        PredictionTimestamp: bioResult.Timestamp ?? DateTime.UtcNow
                    );
                }
                else if (!bioResponded && bioNote != null && !uncertaintyNotes.Contains(bioNote))
                {
                    uncertaintyNotes.Add(bioNote);
                }
            }

            // Exclude unsuitable items deterministically
            if (suitability?.Status == "UNSUITABLE" || opStatus?.OperationalStatus == "TEMPORARILY_SUSPENDED")
            {
                continue;
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
                    Status: suitability?.Status ?? "UNKNOWN",
                    MarineConditionTime: suitability?.ConditionTimestamp ?? start,
                    SafetyProfileId: suitability?.SafetyProfileId ?? Guid.NewGuid()
                ),
                OperationalStatus: opStatus?.OperationalStatus ?? "OPEN",
                BiodiversityContext: bioContext,
                FitScore: suitability?.Status == "SUITABLE" ? 0.95 : 0.70,
                Reasons: new List<string> { "Candidate matches planning window and requested destination." }
            ));
        }

        var candidatesJson = JsonSerializer.Serialize(candidates);

        var session = new RecommendationSession
        {
            RecommendationId = recommendationId,
            WorkflowId = workflow.WorkflowId,
            UserId = userId,
            TargetDestinationId = request.TargetDestinationId,
            StartsAtUtc = request.StartsAt,
            EndsAtUtc = request.EndsAt,
            DurationHours = request.DurationHours,
            ExperienceLevel = request.ExperienceLevel ?? "INTERMEDIATE",
            IncludeBiodiversityContext = request.IncludeBiodiversityContext,
            CandidatesJson = candidatesJson,
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

    public async Task<RecommendationResultDto?> GetRecommendationAsync(Guid recommendationId, CancellationToken ct = default)
    {
        var session = await _db.Recommendations.FirstOrDefaultAsync(r => r.RecommendationId == recommendationId, ct);
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
            UncertaintyNotes: new List<string>()
        );
    }

    public async Task<WorkflowStatusDto?> GetWorkflowStatusAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.PlanningWorkflows.FirstOrDefaultAsync(w => w.WorkflowId == workflowId, ct);
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
                LastSuitabilityStatus = "SUITABLE",
                LastAvailabilityStatus = "AVAILABLE",
                LastOperationalStatus = "OPEN"
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
            LastSuitabilityStatus = "SUITABLE",
            LastAvailabilityStatus = "AVAILABLE",
            LastOperationalStatus = "OPEN"
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
        bool hasChanges = false;
        var summaryNotes = new List<string>();

        // Re-evaluate operational status for destination
        var (opStatus, opResponded, opNote) = await _peerClient.GetOperationalStatusAsync(itinerary.Items.FirstOrDefault()?.DestinationId ?? Guid.Empty, ct);
        if (!opResponded && opNote != null)
        {
            summaryNotes.Add(opNote);
        }

        foreach (var item in itinerary.Items)
        {
            var (suitability, suitResponded, suitNote) = await _peerClient.GetMarineSuitabilityAsync(
                item.DestinationId, 
                item.ActivityId, 
                item.ScheduledStartUtc, 
                item.ScheduledEndUtc, 
                ct);

            if (!suitResponded && suitNote != null && !summaryNotes.Contains(suitNote))
            {
                summaryNotes.Add(suitNote);
            }

            var currentSuit = suitability?.Status ?? "UNKNOWN";
            var currentOp = opStatus?.OperationalStatus ?? "OPEN";
            var currentAvail = item.LastAvailabilityStatus;

            string? advisory = suitability?.Advisory;
            string action = "KEEP";

            if (currentSuit == "UNSUITABLE" || currentOp == "TEMPORARILY_SUSPENDED")
            {
                action = "CANCEL_OR_RESCHEDULE";
                advisory ??= "Activity is currently marked UNSUITABLE due to marine conditions or operational advisory.";
                hasChanges = true;
            }
            else if (currentSuit == "CAUTION")
            {
                action = "REVIEW_CONDITIONS";
                advisory ??= "Elevated risk caution present for the planned activity window.";
                hasChanges = true;
            }

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

        var summary = hasChanges 
            ? "Re-evaluation identified advisories or condition changes requiring review." 
            : "All planned items remain suitable.";

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

        List<PredictedSpeciesDto> predictedSpecies;
        string modelVersion = "it3091-v1.2";
        string limitations = "Contextual prediction only. Not a guarantee of wildlife sighting or site safety.";
        string status = "AVAILABLE";

        if (bioResponded && peerBio?.Species?.Any() == true)
        {
            predictedSpecies = peerBio.Species.Select(s => new PredictedSpeciesDto(
                SpeciesId: s.SpeciesId,
                ScientificName: s.ScientificName,
                CommonName: s.CommonName,
                HabitatSuitability: s.HabitatSuitability,
                ConfidenceLevel: s.ConfidenceLevel
            )).ToList();
            modelVersion = peerBio.ModelVersion ?? modelVersion;
            limitations = peerBio.Limitations ?? limitations;
            status = peerBio.Status ?? status;
        }
        else
        {
            _logger.LogWarning("IT3091 Biodiversity inference unavailable ({Note}); returning default contextual prediction fallback.", bioNote);
            predictedSpecies = new List<PredictedSpeciesDto>
            {
                new PredictedSpeciesDto(
                    SpeciesId: Guid.NewGuid(),
                    ScientificName: "Chelonia mydas",
                    CommonName: "Green Sea Turtle",
                    HabitatSuitability: 0.82,
                    ConfidenceLevel: "MEDIUM"
                )
            };
            limitations += $" (Note: {bioNote ?? "ML service unreachable, using fallback"})";
        }

        var newCache = new BiodiversityPredictionCache
        {
            PredictionId = Guid.NewGuid(),
            DestinationId = destinationId,
            ActivityId = activityId,
            Status = status,
            SpeciesDataJson = JsonSerializer.Serialize(predictedSpecies),
            ModelVersion = modelVersion,
            InferenceTimestampUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(6),
            Limitations = limitations
        };

        _db.BiodiversityPredictions.Add(newCache);
        await _db.SaveChangesAsync(ct);

        return new BiodiversityPredictionDto(
            DestinationId: destinationId,
            ActivityId: activityId,
            Status: status,
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
}
