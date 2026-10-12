namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record MarineConditionsContextDto(
    Guid DestinationId,
    string DestinationName,
    double Latitude,
    double Longitude,
    bool Responded,
    string? TargetEndpoint,
    int AttemptsCount,
    long LatencyMs,
    string RemoteStatus, // "RESPONDED", "UNREACHABLE", "TIMEOUT", "HTTP_ERROR", "NOT_CONFIGURED"
    string SafetyLevel, // "SAFE", "CAUTION", "DANGER", "UNKNOWN"
    string WaterCondition, // "CALM", "MODERATE", "ROUGH", "UNKNOWN"
    double? WaveHeightMeters,
    double? WindSpeedKnots,
    string? TideStatus,
    string? AdvisoryMessage,
    bool FallbackUsed,
    DateTimeOffset? EvaluatedAt,
    string Disclaimer);
