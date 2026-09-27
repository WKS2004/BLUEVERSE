namespace Blueverse.CoastalPlanner.Integration;

public record PeerCatalogueItem(
    Guid DestinationId,
    Guid ActivityId,
    Guid? OfferingId,
    string Title,
    string AvailabilityStatus,
    string PublicationState
);

public record PeerSuitabilityResponse(
    Guid DestinationId,
    Guid ActivityId,
    string Status, // SUITABLE, CAUTION, UNSUITABLE, UNKNOWN
    DateTime ConditionTimestamp,
    Guid? SafetyProfileId,
    string? Advisory
);

public record PeerOperationStatusResponse(
    Guid DestinationId,
    string OperationalStatus, // OPEN, CAUTION, TEMPORARILY_SUSPENDED, CANCELLED, COMPLETED
    List<string>? ActiveAlerts
);

public record PeerBiodiversityInferenceResponse(
    Guid DestinationId,
    Guid? ActivityId,
    string Status,
    List<PeerSpeciesInference>? Species,
    string? ModelVersion,
    DateTime? Timestamp,
    string? Limitations
);

public record PeerSpeciesInference(
    Guid SpeciesId,
    string ScientificName,
    string CommonName,
    double HabitatSuitability,
    string ConfidenceLevel
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
        DateTime windowStart, 
        DateTime windowEnd, 
        CancellationToken ct = default);

    Task<(PeerOperationStatusResponse? Result, bool Responded, string? Note)> GetOperationalStatusAsync(
        Guid destinationId, 
        CancellationToken ct = default);

    Task<(PeerBiodiversityInferenceResponse? Result, bool Responded, string? Note)> GetBiodiversityInferenceAsync(
        Guid destinationId, 
        Guid? activityId, 
        CancellationToken ct = default);
}
