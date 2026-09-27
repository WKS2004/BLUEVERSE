namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record ActiveAdvisoryItemDto(
    string AdvisoryId,
    string Title,
    string Severity, // "INFO", "WARNING", "CRITICAL"
    string Description,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt);

public sealed record OperationalAdvisoriesResponseDto(
    Guid DestinationId,
    string DestinationName,
    bool Responded,
    string? TargetEndpoint,
    int AttemptsCount,
    long LatencyMs,
    string RemoteStatus, // "RESPONDED", "UNREACHABLE", "TIMEOUT", "HTTP_ERROR", "NOT_CONFIGURED"
    IReadOnlyList<ActiveAdvisoryItemDto> Advisories,
    bool FallbackUsed,
    string Message);
