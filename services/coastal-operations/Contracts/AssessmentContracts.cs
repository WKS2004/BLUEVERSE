using System.ComponentModel.DataAnnotations;

namespace Blueverse.CoastalOperations.Contracts;

public sealed class CreateAssessmentRequest
{
    [Required, MaxLength(32)] public string TargetType { get; init; } = string.Empty;
    public Guid? TargetId { get; init; }
    public Guid? SourceWorkflowId { get; init; }
    [Required, MaxLength(64)] public string PeriodStartsAt { get; init; } = string.Empty;
    [Required, MaxLength(64)] public string PeriodEndsAt { get; init; } = string.Empty;
    [MaxLength(160)] public string? Title { get; init; }
    [MaxLength(100)] public string? TimeZoneId { get; init; }
    [Required, MaxLength(2000)] public string Objective { get; init; } = string.Empty;
}

public sealed class UpdateAssessmentDraftRequest
{
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; init; }
    [Required, MaxLength(32)] public string TargetType { get; init; } = string.Empty;
    public Guid? TargetId { get; init; }
    public Guid? SourceWorkflowId { get; init; }
    [Required, MaxLength(64)] public string PeriodStartsAt { get; init; } = string.Empty;
    [Required, MaxLength(64)] public string PeriodEndsAt { get; init; } = string.Empty;
    [MaxLength(160)] public string? Title { get; init; }
    [MaxLength(100)] public string? TimeZoneId { get; init; }
    [Required, MaxLength(2000)] public string Objective { get; init; } = string.Empty;
}

public sealed class CancelAssessmentDraftRequest
{
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; init; }
}

public sealed class SubmitAssessmentDraftRequest
{
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; init; }
}

public sealed record AssessmentResponse(
    Guid AssessmentId,
    Guid WorkflowId,
    string TargetType,
    Guid TargetId,
    Guid? SourceWorkflowId,
    DateTimeOffset PeriodStartsAt,
    DateTimeOffset PeriodEndsAt,
    string Objective,
    string WorkflowStatus,
    string AiDependencyStatus,
    string AiDispatchOutcome,
    bool AiDispatchRetryable,
    IReadOnlyList<ComponentDependencyResult> ComponentDependencies,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? CancelledBy = null,
    DateTimeOffset? CancelledAt = null, string Title = "", string? TimeZoneId = null, string? PeriodStartsLocal = null, string? PeriodEndsLocal = null);

public sealed record AssessmentQueueResponse(IReadOnlyList<AssessmentResponse> Items, string? NextCursor);

public sealed class AssessmentListQuery
{
    [MaxLength(160)] public string? Search { get; init; }
    [MaxLength(32)] public string? TargetType { get; init; }
    public Guid? TargetId { get; init; }
    public Guid? RecordId { get; init; }
    public bool OnlyMine { get; init; }
    public bool PublishedOnly { get; init; }
    [MaxLength(256)] public string? Cursor { get; init; }
    [Range(1, 100)] public int PageSize { get; init; } = 25;
    [MaxLength(32)] public string? WorkflowStatus { get; init; }
    public bool IncludeCancelled { get; init; }
}

public sealed class ReviewerDecisionRequest
{
    [Required] public Guid ProposalId { get; init; }
    [Range(1, int.MaxValue)] public int ProposalVersion { get; init; }
    [Range(0, int.MaxValue)] public int ExpectedTargetStateVersion { get; init; }
    [Required, MaxLength(24)] public string Decision { get; init; } = string.Empty;
    [MaxLength(1000)] public string? Explanation { get; init; }
}

public sealed record ReviewerDecisionResponse(
    Guid DecisionId,
    Guid AssessmentId,
    Guid ProposalId,
    int ProposalVersion,
    string Decision,
    string WorkflowStatus,
    string? OperationalState,
    int? OperationalStateVersion,
    Guid ActorId,
    string? Explanation,
    int ExpectedTargetStateVersion,
    DateTimeOffset DecidedAt);

public sealed record AssessmentDetailResponse(
    AssessmentResponse Assessment,
    IReadOnlyList<ReviewerDecisionResponse> Decisions,
    IReadOnlyList<AssessmentEvidenceResponse> Evidence);

public sealed record AssessmentEvidenceResponse(
    Guid EvidenceId,
    int AssessmentVersion,
    string MediaType,
    long ByteLength,
    string ContentSha256,
    string InspectionStatus,
    DateTimeOffset UploadedAt,
    DateTimeOffset ExpiresAt);

public sealed record AssessmentEvidenceUploadResponse(
    Guid EvidenceId,
    Guid AssessmentId,
    int AssessmentVersion,
    string MediaType,
    long ByteLength,
    string ContentSha256,
    string InspectionStatus,
    DateTimeOffset UploadedAt,
    DateTimeOffset ExpiresAt);

public sealed class AssessmentEvidenceUploadRequest
{
    [Required] public Microsoft.AspNetCore.Http.IFormFile? Image { get; init; }
}

public sealed class RemoveAssessmentEvidenceRequest
{
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; init; }
}
public sealed record AssessmentEvidenceRemovalResponse(Guid EvidenceId, Guid AssessmentId, int AssessmentVersion, string InspectionStatus, DateTimeOffset RemovedAt);
