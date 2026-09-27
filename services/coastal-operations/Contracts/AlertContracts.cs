using System.ComponentModel.DataAnnotations;

namespace Blueverse.CoastalOperations.Contracts;

public sealed class CreateAlertRequest
{
    [Required, MaxLength(32)] public string TargetType { get; init; } = string.Empty;
    public Guid TargetId { get; init; }
    public Guid? AssessmentId { get; init; }
    [Required, MaxLength(160)] public string Title { get; init; } = string.Empty;
    [Required, MaxLength(4000)] public string Description { get; init; } = string.Empty;
    [Required, MaxLength(16)] public string Severity { get; init; } = string.Empty;
    [MaxLength(16)] public string Visibility { get; init; } = "OPERATIONS";
    [Required, MaxLength(64)] public string ValidFrom { get; init; } = string.Empty;
    [Required, MaxLength(64)] public string ValidUntil { get; init; } = string.Empty;
}

public sealed class UpdateAlertRequest
{
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; init; }
    [Required, MaxLength(160)] public string Title { get; init; } = string.Empty;
    [Required, MaxLength(4000)] public string Description { get; init; } = string.Empty;
    [Required, MaxLength(16)] public string Severity { get; init; } = string.Empty;
    [MaxLength(16)] public string? Visibility { get; init; }
    [Required, MaxLength(64)] public string ValidFrom { get; init; } = string.Empty;
    [Required, MaxLength(64)] public string ValidUntil { get; init; } = string.Empty;
}

public sealed class AlertDecisionRequest
{
    [Required, MaxLength(16)] public string Decision { get; init; } = string.Empty;
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; init; }
}

public sealed class AlertListQuery
{
    [MaxLength(256)] public string? Cursor { get; init; }
    [Range(1, 100)] public int PageSize { get; init; } = 25;
    [MaxLength(16)] public string? Lifecycle { get; init; }
    public Guid? TargetId { get; init; }
}

public sealed record AlertResponse(
    Guid AlertId,
    string TargetType,
    Guid TargetId,
    Guid? AssessmentId,
    string Title,
    string Description,
    string Severity,
    string Visibility,
    string Lifecycle,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AlertQueueResponse(IReadOnlyList<AlertResponse> Items, string? NextCursor);

public sealed record AlertDecisionResponse(
    Guid DecisionId,
    Guid AlertId,
    string Decision,
    string Lifecycle,
    int Version,
    DateTimeOffset DecidedAt);
