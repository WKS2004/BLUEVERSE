using System.Text.Json;
using System.Text;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Application;

public sealed class AssessmentDispatchDelivery(CoastalOperationsDbContext db, IAssessmentProposalPort port)
{
    public static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(5);
    public const int MaxPayloadBytes = 128 * 1024;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 3;

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
        timeout.CancelAfter(DispatchTimeout);
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
                    result = await port.DispatchAsync(payload, timeout.Token).WaitAsync(DispatchTimeout, cancellationToken)
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
        var accepted = result.Outcome == "ACCEPTED";
        var retry = !accepted && result.Outcome == "UNAVAILABLE" && result.Retryable && envelope.Attempts < MaxAttempts;
        var now = DateTimeOffset.UtcNow;
        envelope.Status = accepted ? "ACCEPTED" : retry ? "UNAVAILABLE" : "SAFE_FAILURE";
        envelope.LeaseId = null;
        envelope.LeaseUntil = null;
        envelope.NextAttemptAt = now.AddSeconds(30);
        envelope.UpdatedAt = now;
        envelope.Version++;
        var assessment = await db.Assessments.SingleAsync(x => x.Id == envelope.AssessmentId, cancellationToken);
        assessment.AiDependencyStatus = accepted ? "AVAILABLE" : "UNAVAILABLE";
        assessment.AiDispatchOutcome = accepted ? "SUCCEEDED" : result.Outcome == "UNAVAILABLE" ? "UNAVAILABLE" : "INVALID_RESULT";
        assessment.AiDispatchRetryable = retry;
        // SUCCEEDED describes message delivery only. No proposal/state action
        // is manufactured, and no submitted workflow advances to approval.
        if (envelope.Status == "SAFE_FAILURE" && assessment.WorkflowStatus == "SUBMITTED") assessment.WorkflowStatus = "SAFE_FAILURE";
        assessment.Version++;
        assessment.UpdatedAt = now;
        db.OperationsAudit.Add(Audit(envelope, accepted ? "AI_DISPATCH_ACCEPTED" : retry ? "AI_DISPATCH_UNAVAILABLE" : "AI_DISPATCH_FAILED", now));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
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
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
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
