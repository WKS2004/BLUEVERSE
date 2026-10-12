namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record FocalSpeciesPredictionDto(
    string SpeciesName,
    string ScientificName,
    string ConservationStatus,
    double OccurrenceProbability,
    string? HabitatSuitability,
    string? PrimaryThreats);

public sealed record BiodiversityContextResponseDto(
    Guid DestinationId,
    string DestinationName,
    double Latitude,
    double Longitude,
    string Status, // "available", "unavailable", "not_connected"
    IReadOnlyList<FocalSpeciesPredictionDto> Predictions,
    string? ModelVersion,
    string? ModelSource,
    string? UncertaintyNotes,
    DateTimeOffset? EvaluatedAt,
    string Disclaimer,
    bool Responded = false,
    string? TargetEndpoint = null,
    int AttemptsCount = 0,
    long LatencyMs = 0,
    string? RemoteStatus = "UNREACHABLE",
    string? Message = null);
