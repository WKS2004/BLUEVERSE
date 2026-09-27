namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record DependencyHealthItemDto(
    string ServiceKey,
    string ServiceName,
    string TargetEndpoint,
    bool Responded,
    int? HttpStatusCode,
    int AttemptsCount,
    long LatencyMs,
    string Status, // "CONNECTED", "UNREACHABLE", "TIMEOUT", "HTTP_ERROR", "NOT_CONFIGURED"
    string FallbackStrategy,
    string Message);

public sealed record MicroserviceDependenciesStatusDto(
    string Microservice,
    string Version,
    string OverallStatus, // "HEALTHY_STANDALONE", "HEALTHY_CONNECTED", "HEALTHY_DEGRADED"
    DateTimeOffset Timestamp,
    IReadOnlyList<DependencyHealthItemDto> Dependencies,
    string ResilienceNote);
