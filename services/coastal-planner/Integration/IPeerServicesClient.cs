using System.Text.Json.Serialization;

namespace Blueverse.CoastalPlanner.Integration;

public record PeerCatalogueItem(
    Guid DestinationId,
    Guid ActivityId,
    Guid? OfferingId,
    string Title,
    string AvailabilityStatus,
    string PublicationState,
    DateTime? AvailableFrom = null,
    DateTime? AvailableUntil = null,
    List<string>? ExperienceLevels = null,
    DateTime? CheckedAt = null,
    string? TimeZone = null,
    double? Latitude = null,
    double? Longitude = null,
    string? ActivityName = null,
    string? Region = null,
    string? DestinationName = null
);

public record PeerSuitabilityResponse(
    Guid DestinationId,
    Guid ActivityId,
    string Status, // SUITABLE, CAUTION, UNSUITABLE, UNKNOWN
    DateTime ConditionTimestamp,
    Guid? SafetyProfileId,
    string? Advisory,
    bool IsFresh = false,
    Guid? AssessmentId = null,
    Guid? SnapshotId = null,
    DateTime? EvaluatedAt = null,
    DateTime? RetrievedAt = null,
    string? Source = null,
    string? Freshness = null,
    string[]? MissingFields = null,
    string[]? Violations = null,
    string[]? CautionFactors = null
);

public record PeerOperationStatusResponse(
    Guid DestinationId,
    string OperationalStatus, // OPEN, CAUTION, TEMPORARILY_SUSPENDED, CANCELLED, COMPLETED
    List<string>? ActiveAlerts,
    string? TargetType = null,
    Guid? TargetId = null,
    int? StateVersion = null,
    DateTime? UpdatedAt = null
);

public record PeerBiodiversityInferenceResponse(
    Guid DestinationId,
    Guid? ActivityId,
    string Status,
    List<PeerSpeciesInference>? Species,
    string? ModelVersion,
    [property: JsonPropertyName("inferenceTimestampUtc")] DateTime? Timestamp,
    string? Limitations,
    int ContractVersion = 1,
    string? UncertaintyText = null,
    string? Provider = null
);

public record BiodiversityInferenceRequest(
    int ContractVersion,
    Guid DestinationId,
    Guid? ActivityId,
    DateTime RequestedAtUtc
);

public record PeerSpeciesInference(
    Guid SpeciesId,
    string ScientificName,
    string CommonName,
    double HabitatSuitability,
    string ConfidenceLevel,
    string? UncertaintyText = null
);

public interface IPeerServicesClient
{
    Task<(List<PeerCatalogueItem> Items, bool Responded, string? Note)> GetCatalogueOfferingsAsync(
        Guid destinationId, 
        List<Guid>? preferredActivityIds, 
        CancellationToken ct = default);

    Task<(PeerSuitabilityResponse? Result, bool Responded, string? Note)> GetMarineSuitabilityAsync(
        Guid destinationId, 
        Guid activityId, 
        double? latitude,
        double? longitude,
        DateTime requestedTimeUtc,
        CancellationToken ct = default);

    Task<(PeerOperationStatusResponse? Result, bool Responded, string? Note)> GetOperationalStatusAsync(
        Guid destinationId, 
        CancellationToken ct = default);

    Task<(PeerBiodiversityInferenceResponse? Result, bool Responded, string? Note)> GetBiodiversityInferenceAsync(
        Guid destinationId, 
        Guid? activityId, 
        CancellationToken ct = default);
}
