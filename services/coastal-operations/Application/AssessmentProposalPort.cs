namespace Blueverse.CoastalOperations.Application;

public enum AssessmentAiAvailability
{
    NotConnected,
    Unavailable,
    Available
}

public sealed record ProposalDispatchOutcome(string Outcome, bool Retryable);

/// <summary>
/// Private, typed seam for the future Agentic AI runtime. Before G07 the
/// implementation must stay disconnected and must never create proposals.
/// </summary>
public interface IAssessmentProposalPort
{
    Task<AssessmentAiAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);
    Task<ProposalDispatchOutcome> DispatchAsync(Guid workflowId, Guid assessmentId, CancellationToken cancellationToken);
}

public sealed class DisconnectedAssessmentProposalPort : IAssessmentProposalPort
{
    public Task<AssessmentAiAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
        Task.FromResult(AssessmentAiAvailability.NotConnected);

    public Task<ProposalDispatchOutcome> DispatchAsync(Guid workflowId, Guid assessmentId, CancellationToken cancellationToken) =>
        Task.FromResult(new ProposalDispatchOutcome("NOT_STARTED", Retryable: false));
}
