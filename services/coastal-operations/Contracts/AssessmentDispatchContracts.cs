namespace Blueverse.CoastalOperations.Contracts;

public sealed record PublishedAssessmentDispatch(
    Guid DispatchId, Guid WorkflowId, Guid AssessmentId, int PublishedVersion,
    string TargetType, Guid TargetId, Guid? SourceWorkflowId,
    DateTimeOffset PeriodStartsAt, DateTimeOffset PeriodEndsAt, string Objective,
    Guid ActorId, string CorrelationId, DateTimeOffset PublishedAt,
    IReadOnlyList<ComponentDependencyResult> ComponentDependencies,
    IReadOnlyList<AssessmentEvidenceResponse> Evidence, string Title = "", string? TimeZoneId = null);
