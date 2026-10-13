namespace Blueverse.CoastalOperations.Contracts;

public sealed record AssessmentAgentStepResponse(
    string AgentId,
    string Status,
    string? Summary,
    IReadOnlyList<string> SourceReferences,
    IReadOnlyList<string> UncertaintyNotes,
    string? ErrorCode = null);

public sealed record AssessmentSafetyProposalResponse(
    Guid AssessmentId,
    Guid TargetId,
    int TargetStateVersion,
    string Recommendation,
    string? ProposedAction,
    string Summary,
    IReadOnlyList<string> EvidenceReferences,
    IReadOnlyList<string> Uncertainties,
    string ReviewSuggestion,
    DateTimeOffset ProducedAtUtc);

public sealed record AssessmentAgentExecutionSummaryResponse(
    string Status,
    string ObjectiveSummary,
    IReadOnlyList<AssessmentAgentStepResponse> Steps,
    AssessmentSafetyProposalResponse? SafetyProposal);

public sealed record AssessmentProposalResponse(
    Guid ProposalId,
    int ProposalVersion,
    string ValidationStatus,
    string TargetType,
    Guid TargetId,
    string? ProposedState,
    int? ExpectedTargetStateVersion,
    bool RequiresSeparateReviewer,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
