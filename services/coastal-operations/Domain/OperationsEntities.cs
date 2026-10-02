namespace Blueverse.CoastalOperations.Domain;

public sealed class Assessment
{
    public string Title { get; set; } = string.Empty;
    public string? TimeZoneId { get; set; }
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? SourceWorkflowId { get; set; }
    public DateTimeOffset PeriodStartsAt { get; set; }
    public DateTimeOffset PeriodEndsAt { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string WorkflowStatus { get; set; } = "DRAFT";
    public string AiDependencyStatus { get; set; } = "NOT_CONNECTED";
    public string AiDispatchOutcome { get; set; } = "NOT_REQUESTED";
    public bool AiDispatchRetryable { get; set; }
    public string ComponentDependenciesJson { get; set; } = "[]";
    public Guid InitiatedBy { get; set; }
    public int Version { get; set; } = 1;
    public Guid? CancelledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<AssessmentProposal> Proposals { get; set; } = new List<AssessmentProposal>();
    public ICollection<ReviewerDecision> Decisions { get; set; } = new List<ReviewerDecision>();
    public ICollection<AssessmentEvidence> Evidence { get; set; } = new List<AssessmentEvidence>();
}

public sealed class AssessmentEvidence
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public int AssessmentVersion { get; set; }
    public Guid UploadedBy { get; set; }
    public string MediaType { get; set; } = "image/png";
    public long ByteLength { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public string InspectionStatus { get; set; } = "AVAILABLE";
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public DateTimeOffset? ContentDeletedAt { get; set; }

    public Assessment Assessment { get; set; } = null!;
}

public sealed class AssessmentProposal
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public int ProposalVersion { get; set; }
    public string ValidationStatus { get; set; } = "PENDING";
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string? ProposedState { get; set; }
    public int? ExpectedTargetStateVersion { get; set; }
    public bool RequiresSeparateReviewer { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Assessment Assessment { get; set; } = null!;
    public ICollection<ReviewerDecision> Decisions { get; set; } = new List<ReviewerDecision>();
}

public sealed class ReviewerDecision
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid ProposalId { get; set; }
    public int ProposalVersion { get; set; }
    public string Decision { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string? Explanation { get; set; }
    public int ExpectedTargetStateVersion { get; set; }
    public string WorkflowStatusAfterDecision { get; set; } = string.Empty;
    public string? AppliedOperationalState { get; set; }
    public int? AppliedOperationalStateVersion { get; set; }
    public DateTimeOffset DecidedAt { get; set; }

    public Assessment Assessment { get; set; } = null!;
    public AssessmentProposal Proposal { get; set; } = null!;
}

public sealed class TargetOperationalState
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string State { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OperationalHistoryEntry
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
    public Guid AssessmentId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid DecisionId { get; set; }
    public Guid ActorId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OperationalAlert
{
    public string? TimeZoneId { get; set; }
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? AssessmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Visibility { get; set; } = "OPERATIONS";
    public string Lifecycle { get; set; } = "PROPOSED";
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
    public int Version { get; set; } = 1;
    public Guid? WithdrawnBy { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Assessment? Assessment { get; set; }
}

public sealed class AlertDecision
{
    public Guid Id { get; set; }
    public Guid AlertId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public int ExpectedVersion { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestDigest { get; set; } = string.Empty;
    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OperationsAuditEntry
{
    public string? ActorName { get; set; }
    public string ActorRolesJson { get; set; } = "[]";
    public string? RecordTitle { get; set; }
    public string? Summary { get; set; }
    public string ChangesJson { get; set; } = "[]";
    public Guid Id { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
