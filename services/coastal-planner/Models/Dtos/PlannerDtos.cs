namespace Blueverse.CoastalPlanner.Models.Dtos;

public record RecommendationRequestDto(
    Guid TargetDestinationId,
    DateTime StartsAt,
    DateTime EndsAt,
    int DurationHours,
    List<Guid>? PreferredActivityIds,
    string? ExperienceLevel,
    bool IncludeBiodiversityContext = true
);

public record RecommendationCandidateDto(
    Guid DestinationId,
    Guid ActivityId,
    Guid? OfferingId,
    string Title,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    string AvailabilityStatus,
    SuitabilitySummaryDto Suitability,
    string OperationalStatus,
    BiodiversityContextDto? BiodiversityContext,
    double FitScore,
    List<string> Reasons
);

public record SuitabilitySummaryDto(
    string Status,
    DateTime? MarineConditionTime,
    Guid? SafetyProfileId
);

public record BiodiversityContextDto(
    string SpeciesName,
    double Probability,
    string Uncertainty,
    DateTime PredictionTimestamp
);

public record RecommendationResultDto(
    Guid RecommendationId,
    Guid WorkflowId,
    string Status,
    DateTime GeneratedAt,
    List<RecommendationCandidateDto> Candidates,
    int ExcludedCandidatesCount,
    List<string> UncertaintyNotes
);

public record WorkflowStatusDto(
    Guid WorkflowId,
    string WorkflowType,
    string Status,
    Guid? InitiatorUserId,
    string? Objective,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ResultSummary,
    string? FailureReason
);

public record ItineraryDto(
    Guid ItineraryId,
    Guid OwnerUserId,
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    int ConcurrencyVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ItineraryItemDto> Items
);

public record ItineraryItemDto(
    Guid ItemId,
    Guid ItineraryId,
    Guid DestinationId,
    Guid ActivityId,
    Guid? OfferingId,
    string Title,
    int OrderIndex,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    string LastSuitabilityStatus,
    string LastAvailabilityStatus,
    string LastOperationalStatus,
    string? AdvisoryNote
);

public record CreateItineraryRequestDto(
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    List<CreateItineraryItemRequestDto> Items
);

public record CreateItineraryItemRequestDto(
    Guid DestinationId,
    Guid ActivityId,
    Guid? OfferingId,
    string Title,
    int OrderIndex,
    DateTime ScheduledStart,
    DateTime ScheduledEnd
);

public record UpdateItineraryRequestDto(
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    int ConcurrencyVersion,
    List<CreateItineraryItemRequestDto> Items
);

public record ItineraryReEvaluationRequestDto(
    string ReEvaluationMode = "FULL_ASSESSMENT"
);

public record ItineraryReEvaluationResultDto(
    Guid ItineraryId,
    DateTime EvaluatedAt,
    bool HasChanges,
    string Summary,
    List<ItineraryReEvaluationItemDto> Items
);

public record ItineraryReEvaluationItemDto(
    Guid ItemId,
    Guid? OfferingId,
    string CurrentAvailability,
    string CurrentSuitability,
    string CurrentOperationalStatus,
    string? AdvisoryMessage,
    string SuggestedAction
);

public record BiodiversityPredictionDto(
    Guid DestinationId,
    Guid? ActivityId,
    string Status,
    List<PredictedSpeciesDto> PredictedSpecies,
    ModelMetadataDto? ModelMetadata,
    string Limitations
);

public record PredictedSpeciesDto(
    Guid SpeciesId,
    string ScientificName,
    string CommonName,
    double HabitatSuitability,
    string ConfidenceLevel
);

public record ModelMetadataDto(
    string? ModelVersion,
    DateTime? InferenceTimestamp
);
