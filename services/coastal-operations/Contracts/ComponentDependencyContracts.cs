namespace Blueverse.CoastalOperations.Contracts;

public sealed record ComponentDependencyEvidence(
    string? AvailabilityStatus,
    string? SuitabilityClassification,
    string? WorkflowStatus,
    string? SourceVersion,
    string? ProfileVersion,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? ValidUntil,
    IReadOnlyList<string> ReasonCodes);

public sealed record ComponentDependencyResult(
    string Service,
    string Endpoint,
    string Status,
    int Attempts,
    int Retries,
    bool Retryable,
    string? ErrorCode,
    string Message,
    DateTimeOffset? CheckedAt,
    ComponentDependencyEvidence? Data);

public sealed record ComponentDependencyHealth(
    string Service,
    string Status,
    int Attempts,
    int Retries,
    bool Retryable,
    string? ErrorCode,
    string Message,
    DateTimeOffset? CheckedAt);

internal sealed class ExperienceAvailabilityResponse
{
    public string TargetType { get; init; } = string.Empty;
    public Guid TargetId { get; init; }
    public string AvailabilityStatus { get; init; } = string.Empty;
    public string? TargetVersion { get; init; }
    public DateTimeOffset EvaluatedAt { get; init; }
    public DateTimeOffset? ValidUntil { get; init; }
}
