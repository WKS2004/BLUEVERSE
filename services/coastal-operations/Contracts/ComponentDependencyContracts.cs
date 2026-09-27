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

internal sealed class MarineSuitabilityResponse
{
    public string TargetType { get; init; } = string.Empty;
    public Guid TargetId { get; init; }
    public string Classification { get; init; } = string.Empty;
    public string? ProfileVersion { get; init; }
    public DateTimeOffset AssessedAt { get; init; }
    public DateTimeOffset? ValidUntil { get; init; }
    public IReadOnlyList<string>? ReasonCodes { get; init; }
}

internal sealed class PlannerWorkflowResponse
{
    public Guid WorkflowId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}

internal sealed record MarineSuitabilityRequest(
    string TargetType,
    Guid TargetId,
    DateTimeOffset PeriodStartsAt,
    DateTimeOffset PeriodEndsAt);
