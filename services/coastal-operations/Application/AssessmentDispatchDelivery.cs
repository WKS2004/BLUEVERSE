using System.Text.Json;
using System.Text;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Application;

public sealed class AssessmentDispatchDelivery(
    CoastalOperationsDbContext db,
    IAssessmentProposalPort port,
    IOptions<AgenticAiOptions>? agenticOptions = null)
{
    public static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(5);
    public const int MaxPayloadBytes = 128 * 1024;
    private const int MaxAttempts = 3;
    private TimeSpan EffectiveDispatchTimeout => agenticOptions is null
        ? DispatchTimeout
        : TimeSpan.FromSeconds(Math.Clamp(agenticOptions.Value.DispatchTimeoutSeconds, 5, 180));
    private TimeSpan LeaseDuration => EffectiveDispatchTimeout.Add(TimeSpan.FromSeconds(30));

    public async Task<bool> DeliverNextAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var envelope = await db.AssessmentDispatches.Where(x =>
                (x.Status == "NOT_CONNECTED" || x.Status == "PENDING" || x.Status == "UNAVAILABLE" ||
                 (x.Status == "LEASED" && x.LeaseUntil <= now)) && x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (envelope is null) return false;
        if (envelope.Attempts >= MaxAttempts)
        {
            await CompleteAsync(envelope, new ProposalDispatchOutcome("UNAVAILABLE", false), cancellationToken);
            return true;
        }

        AssessmentAiAvailability availability;
        using var availabilityTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        availabilityTimeout.CancelAfter(DispatchTimeout);
        try
        {
            availability = await port.GetAvailabilityAsync(availabilityTimeout.Token)
                .WaitAsync(DispatchTimeout, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
            exception is TimeoutException or OperationCanceledException or HttpRequestException)
        {
            availability = AssessmentAiAvailability.Unavailable;
        }
        // Production is deliberately disconnected before G07. Do not invoke a
        // delivery endpoint or claim that an agent started.
        if (availability == AssessmentAiAvailability.NotConnected) return false;

        var leaseId = Guid.CreateVersion7();
        envelope.Status = "LEASED";
        envelope.LeaseId = leaseId;
        envelope.LeaseUntil = now.Add(LeaseDuration);
        envelope.Attempts++;
        envelope.Version++;
        envelope.UpdatedAt = now;
        db.OperationsAudit.Add(Audit(envelope, "AI_DISPATCH_ATTEMPT", now));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }

        ProposalDispatchOutcome result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(EffectiveDispatchTimeout);
        try
        {
            if (availability != AssessmentAiAvailability.Available)
                result = new("UNAVAILABLE", true);
            else
            {
                var payload = Encoding.UTF8.GetByteCount(envelope.PayloadJson) <= MaxPayloadBytes
                    ? JsonSerializer.Deserialize<PublishedAssessmentDispatch>(envelope.PayloadJson, OperationsValidation.JsonOptions)
                    : null;
                if (payload is null || payload.DispatchId != envelope.Id || payload.AssessmentId != envelope.AssessmentId ||
                    payload.WorkflowId != envelope.WorkflowId || payload.PublishedVersion <= 0 ||
                    payload.ActorId != envelope.ActorId || payload.CorrelationId != envelope.CorrelationId)
                    result = new("INVALID_RESULT", false);
                else
                    result = await port.DispatchAsync(payload, timeout.Token).WaitAsync(EffectiveDispatchTimeout, cancellationToken)
                        ?? new ProposalDispatchOutcome("INVALID_RESULT", false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException or HttpRequestException)
        { result = new("UNAVAILABLE", true); }
        catch (JsonException) { result = new("INVALID_RESULT", false); }

        // A late sender cannot complete a lease that another worker reclaimed.
        db.ChangeTracker.Clear();
        envelope = await db.AssessmentDispatches.SingleAsync(x => x.Id == envelope.Id, cancellationToken);
        if (envelope.LeaseId != leaseId) return false;
        await CompleteAsync(envelope, result, cancellationToken);
        return true;
    }

    private async Task CompleteAsync(AssessmentDispatch envelope, ProposalDispatchOutcome result, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var assessment = await db.Assessments.SingleAsync(x => x.Id == envelope.AssessmentId, cancellationToken);
        AssessmentProposal? proposal = null;
        if (result.Outcome == "ACCEPTED" && result.Summary is not null &&
            !TryPrepareSummaryAndProposal(assessment, result.Summary, now, out proposal))
            result = new ProposalDispatchOutcome("INVALID_RESULT", false);

        var accepted = result.Outcome == "ACCEPTED";
        var retry = !accepted && result.Outcome == "UNAVAILABLE" && result.Retryable && envelope.Attempts < MaxAttempts;
        envelope.Status = accepted ? "ACCEPTED" : retry ? "UNAVAILABLE" : "SAFE_FAILURE";
        envelope.LeaseId = null;
        envelope.LeaseUntil = null;
        envelope.NextAttemptAt = now.AddSeconds(30);
        envelope.UpdatedAt = now;
        envelope.Version++;
        assessment.AiDependencyStatus = accepted ? "AVAILABLE" : "UNAVAILABLE";
        assessment.AiDispatchOutcome = accepted ? "SUCCEEDED" : result.Outcome == "UNAVAILABLE" ? "UNAVAILABLE" : "INVALID_RESULT";
        assessment.AiDispatchRetryable = retry;
        if (accepted && result.Summary is not null)
        {
            assessment.AgentExecutionSummaryJson = JsonSerializer.Serialize(result.Summary, OperationsValidation.JsonOptions);
            if (proposal is not null)
            {
                db.AssessmentProposals.Add(proposal);
                assessment.WorkflowStatus = "PENDING_APPROVAL";
                db.OperationsAudit.Add(Audit(envelope, "AI_PROPOSAL_CREATED", now));
            }
        }
        if (envelope.Status == "SAFE_FAILURE" && assessment.WorkflowStatus == "SUBMITTED") assessment.WorkflowStatus = "SAFE_FAILURE";
        assessment.Version++;
        assessment.UpdatedAt = now;
        db.OperationsAudit.Add(Audit(envelope, accepted ? "AI_DISPATCH_ACCEPTED" : retry ? "AI_DISPATCH_UNAVAILABLE" : "AI_DISPATCH_FAILED", now));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
    }

    private bool TryPrepareSummaryAndProposal(
        Assessment assessment,
        AssessmentAgentExecutionSummaryResponse summary,
        DateTimeOffset now,
        out AssessmentProposal? proposal)
    {
        proposal = null;
        if (assessment.WorkflowStatus != "SUBMITTED" || summary.Status != "COMPLETED" ||
            string.IsNullOrWhiteSpace(summary.ObjectiveSummary) || summary.ObjectiveSummary.Length > 500 ||
            summary.ObjectiveSummary.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            summary.ObjectiveSummary.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
            summary.Steps is null || summary.Steps.Count != 4 || summary.SafetyProposal is null)
            return false;
        if (JsonSerializer.SerializeToUtf8Bytes(summary, OperationsValidation.JsonOptions).Length > 32_768)
            return false;

        var expectedAgents = new HashSet<string>(StringComparer.Ordinal)
        {
            "planning_coordination", "experience_biodiversity", "marine_conditions", "safety_operations"
        };
        if (summary.Steps.Any(step => step is null || !expectedAgents.Remove(step.AgentId) || step.Status != "SUCCEEDED" ||
                step.Summary is { Length: > 1000 } || step.SourceReferences is null || step.SourceReferences.Count > 32 ||
                step.UncertaintyNotes is null || step.UncertaintyNotes.Count > 32 ||
                step.SourceReferences.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 256) ||
                step.UncertaintyNotes.Any(value => value is null || value.Length > 500)) || expectedAgents.Count != 0)
            return false;

        var safety = summary.SafetyProposal;
        if (safety.AssessmentId != assessment.Id || safety.TargetId != assessment.TargetId ||
            safety.TargetStateVersion <= 0 || safety.Summary is null || safety.Summary.Length is 0 or > 1000 ||
            safety.Summary.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            safety.Summary.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
            safety.EvidenceReferences is null || safety.EvidenceReferences.Count is < 1 or > 12 ||
            safety.EvidenceReferences.Distinct(StringComparer.Ordinal).Count() != safety.EvidenceReferences.Count ||
            safety.EvidenceReferences.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128) ||
            safety.Uncertainties is null || safety.Uncertainties.Count > 10 ||
            safety.Uncertainties.Any(value => value is null || value.Length > 300) ||
            safety.Recommendation is not ("CONTINUE" or "CONTINUE_WITH_CAUTION" or "PROPOSE_ACTION" or "INSUFFICIENT_DATA") ||
            safety.ReviewSuggestion is not ("NONE" or "REVIEW_RECOMMENDED"))
            return false;

        var state = db.TargetOperationalStates.SingleOrDefault(x => x.TargetType == assessment.TargetType && x.TargetId == assessment.TargetId);
        if (state is null || state.Version != safety.TargetStateVersion) return false;
        if (safety.ProposedAction is null)
            return safety.Recommendation != "PROPOSE_ACTION";
        if (safety.Recommendation != "PROPOSE_ACTION" || safety.ProposedAction.Length > 32 ||
            !OperationsValidation.IsAllowedTransition(state.TargetType, state.State, safety.ProposedAction))
            return false;

        var nextVersion = db.AssessmentProposals.Where(x => x.AssessmentId == assessment.Id)
            .Select(x => (int?)x.ProposalVersion).Max() ?? 0;
        var expiresAt = now.AddMinutes(30);
        proposal = new AssessmentProposal
        {
            Id = Guid.CreateVersion7(),
            AssessmentId = assessment.Id,
            ProposalVersion = checked(nextVersion + 1),
            ValidationStatus = "VALIDATED",
            TargetType = assessment.TargetType,
            TargetId = assessment.TargetId,
            ProposedState = safety.ProposedAction,
            ExpectedTargetStateVersion = state.Version,
            RequiresSeparateReviewer = OperationsValidation.IsHighImpactState(assessment.TargetType, safety.ProposedAction),
            ExpiresAt = expiresAt,
            CreatedAt = now
        };
        return true;
    }

    private static OperationsAuditEntry Audit(AssessmentDispatch envelope, string action, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), ResourceType = "assessment", ResourceId = envelope.AssessmentId,
        Action = action, ActorId = envelope.ActorId, CorrelationId = envelope.CorrelationId, CreatedAt = now
    };
}

public sealed class AssessmentDispatchHostedService(
    IServiceScopeFactory scopes, ILogger<AssessmentDispatchHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                for (var count = 0; count < 20 && !stoppingToken.IsCancellationRequested; count++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    if (!await scope.ServiceProvider.GetRequiredService<AssessmentDispatchDelivery>().DeliverNextAsync(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "Assessment delivery is temporarily unavailable."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
