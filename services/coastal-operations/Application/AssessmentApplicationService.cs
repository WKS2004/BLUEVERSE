using System.Text.Json;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Blueverse.CoastalOperations.Application;

public sealed class AssessmentApplicationService(
    CoastalOperationsDbContext db,
    IdempotencyStore idempotencyStore,
    IAssessmentProposalPort proposalPort,
    IComponentDependencyCollector componentDependencyCollector)
{
    private static readonly HashSet<string> WorkflowStatuses =
    ["SUBMITTED", "PROPOSAL_READY", "PENDING_APPROVAL", "REVISION_REQUESTED", "REJECTED", "APPROVED", "EXECUTED", "BLOCKED", "SAFE_FAILURE"];

    public async Task<StoredOutcome<AssessmentResponse>> CreateAsync(
        CreateAssessmentRequest request,
        Guid actorId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var key = OperationsValidation.ValidateIdempotencyKey(idempotencyKey);
        var digest = OperationsValidation.RequestDigest(request);
        var replay = await idempotencyStore.TryReplayAsync<AssessmentResponse>(
            actorId, "assessment.create", key, digest, cancellationToken);
        if (replay is not null) return replay;

        var targetType = OperationsValidation.NormalizeTargetType(request.TargetType);
        if (request.TargetId == Guid.Empty || (request.SourceWorkflowId == Guid.Empty))
        {
            throw Invalid("target_identity_invalid", "Target identity is invalid", "Provide a non-empty target ID and omit an empty source workflow ID.");
        }

        var objective = request.Objective.Trim();
        if (objective.Length == 0)
        {
            throw Invalid("objective_required", "An assessment objective is required", "Describe the operational question to assess.");
        }

        var period = OperationsValidation.ParsePeriod(request.PeriodStartsAt, request.PeriodEndsAt);
        var dependenciesTask = componentDependencyCollector.CollectAsync(
            targetType,
            request.TargetId,
            period.StartsAt,
            period.EndsAt,
            request.SourceWorkflowId,
            cancellationToken);
        var aiAvailabilityTask = ReadAiAvailabilityAsync(cancellationToken);
        await Task.WhenAll(dependenciesTask, aiAvailabilityTask);
        var componentDependencies = await dependenciesTask;
        var aiAvailability = await aiAvailabilityTask;
        var now = DateTimeOffset.UtcNow;
        await InitializeTargetOperationalStateIfConfirmedAsync(
            targetType, request.TargetId, componentDependencies, actorId, correlationId, now, cancellationToken);
        var assessment = new Assessment
        {
            Id = Guid.CreateVersion7(),
            WorkflowId = Guid.CreateVersion7(),
            TargetType = targetType,
            TargetId = request.TargetId,
            SourceWorkflowId = request.SourceWorkflowId,
            PeriodStartsAt = period.StartsAt,
            PeriodEndsAt = period.EndsAt,
            Objective = objective,
            WorkflowStatus = "SUBMITTED",
            AiDependencyStatus = aiAvailability switch
            {
                AssessmentAiAvailability.Available => "AVAILABLE",
                AssessmentAiAvailability.Unavailable => "UNAVAILABLE",
                _ => "NOT_CONNECTED"
            },
            AiDispatchOutcome = aiAvailability == AssessmentAiAvailability.Unavailable ? "NOT_STARTED" : "NOT_REQUESTED",
            AiDispatchRetryable = aiAvailability == AssessmentAiAvailability.Unavailable,
            ComponentDependenciesJson = JsonSerializer.Serialize(componentDependencies, OperationsValidation.JsonOptions),
            InitiatedBy = actorId,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Assessments.Add(assessment);
        db.OperationsAudit.Add(Audit("assessment", assessment.Id, "CREATED", actorId, correlationId, now));
        return await SaveIdempotentAsync(
            actorId,
            "assessment.create",
            key,
            digest,
            StatusCodes.Status201Created,
            ToResponse(assessment),
            cancellationToken);
    }

    public async Task<AssessmentQueueResponse> GetQueueAsync(
        AssessmentListQuery query,
        Guid actorId,
        bool canReadQueue,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        if (!OperationsValidation.TryReadCursor(query.Cursor, out var cursorId))
        {
            throw Invalid("cursor_invalid", "The assessment cursor is invalid", "Use the cursor returned by the previous page.");
        }

        var items = db.Assessments.AsNoTracking().AsQueryable();
        if (!canReadQueue) items = items.Where(x => x.InitiatedBy == actorId);
        if (!string.IsNullOrWhiteSpace(query.WorkflowStatus))
        {
            var status = query.WorkflowStatus.Trim().ToUpperInvariant();
            if (!WorkflowStatuses.Contains(status))
            {
                throw Invalid("workflow_status_invalid", "The workflow status is invalid", "Use a documented Coastal Operations workflow status.");
            }

            items = items.Where(x => x.WorkflowStatus == status);
        }

        if (cursorId != Guid.Empty) items = items.Where(x => x.Id.CompareTo(cursorId) < 0);
        var page = await items.OrderByDescending(x => x.Id).Take(query.PageSize + 1).ToListAsync(cancellationToken);
        var hasMore = page.Count > query.PageSize;
        if (hasMore) page.RemoveAt(page.Count - 1);
        return new AssessmentQueueResponse(
            page.Select(ToResponse).ToArray(),
            hasMore && page.Count > 0 ? OperationsValidation.EncodeCursor(page[^1].Id) : null);
    }

    public async Task<AssessmentDetailResponse> GetDetailAsync(
        Guid assessmentId,
        Guid actorId,
        bool canReadQueue,
        bool canReadEvidence,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var assessment = await db.Assessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null || (!canReadQueue && assessment.InitiatedBy != actorId))
        {
            throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist or is outside the caller's scope.");
        }

        var decisions = await db.ReviewerDecisions.AsNoTracking()
            .Where(x => x.AssessmentId == assessmentId)
            .OrderBy(x => x.DecidedAt)
            .Select(x => new ReviewerDecisionResponse(
                x.Id, x.AssessmentId, x.ProposalId, x.ProposalVersion, x.Decision,
                x.WorkflowStatusAfterDecision, x.AppliedOperationalState, x.AppliedOperationalStateVersion, x.ActorId, x.Explanation,
                x.ExpectedTargetStateVersion, x.DecidedAt))
            .ToListAsync(cancellationToken);
        var evidence = canReadEvidence
            ? await db.AssessmentEvidence.AsNoTracking()
                .Where(x => x.AssessmentId == assessmentId && x.InspectionStatus == "AVAILABLE")
                .OrderBy(x => x.UploadedAt)
                .Select(x => new AssessmentEvidenceResponse(
                    x.Id, x.AssessmentVersion, x.MediaType, x.ByteLength, x.ContentSha256,
                    x.InspectionStatus, x.UploadedAt, x.ExpiresAt))
                .ToListAsync(cancellationToken)
            : [];
        return new AssessmentDetailResponse(ToResponse(assessment), decisions, evidence);
    }

    public async Task<StoredOutcome<ReviewerDecisionResponse>> DecideAsync(
        Guid assessmentId,
        ReviewerDecisionRequest request,
        Guid actorId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var decision = OperationsValidation.NormalizeDecision(request.Decision, "APPROVE", "REJECT", "REQUEST_REVISION");
        var key = OperationsValidation.ValidateIdempotencyKey(idempotencyKey);
        var digest = OperationsValidation.RequestDigest(request);
        var replay = await idempotencyStore.TryReplayAsync<ReviewerDecisionResponse>(
            actorId, $"assessment.decide:{assessmentId:N}", key, digest, cancellationToken);
        if (replay is not null) return replay;

        var assessment = await db.Assessments.SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken)
            ?? throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist.");
        var proposal = await db.AssessmentProposals.SingleOrDefaultAsync(
            x => x.Id == request.ProposalId && x.AssessmentId == assessmentId,
            cancellationToken);
        if (proposal is null || proposal.ProposalVersion != request.ProposalVersion)
        {
            throw Conflict("proposal_not_available", "A current proposal is not available", "This assessment has no proposal matching the supplied ID and version.");
        }

        if (assessment.WorkflowStatus != "PENDING_APPROVAL" || proposal.ExpiresAt <= DateTimeOffset.UtcNow || proposal.ValidationStatus != "VALIDATED")
        {
            throw Conflict("proposal_not_actionable", "The proposal is no longer actionable", "The proposal must be current, validated and awaiting review.");
        }

        if (await db.ReviewerDecisions.AnyAsync(x => x.ProposalId == proposal.Id && x.ProposalVersion == proposal.ProposalVersion, cancellationToken))
        {
            throw Conflict("proposal_already_decided", "The proposal already has a decision", "A proposal version can receive only one effective reviewer decision.");
        }

        if (decision == "APPROVE" && string.IsNullOrWhiteSpace(proposal.ProposedState))
        {
            throw Conflict("proposal_has_no_action", "This proposal has no operational action", "Read-only recommendations cannot be executed as operational-state decisions.");
        }

        var isHighImpact = proposal.RequiresSeparateReviewer ||
                           (proposal.ProposedState is not null && OperationsValidation.IsHighImpactState(proposal.TargetType, proposal.ProposedState));
        if (isHighImpact && assessment.InitiatedBy == actorId)
        {
            throw new CoastalOperationsException(StatusCodes.Status403Forbidden, "independent_reviewer_required", "A different reviewer is required", "The assessment initiator cannot approve this high-impact operation.");
        }

        var now = DateTimeOffset.UtcNow;
        var decisionRecord = new ReviewerDecision
        {
            Id = Guid.CreateVersion7(),
            AssessmentId = assessment.Id,
            ProposalId = proposal.Id,
            ProposalVersion = proposal.ProposalVersion,
            Decision = decision,
            ActorId = actorId,
            Explanation = string.IsNullOrWhiteSpace(request.Explanation) ? null : request.Explanation.Trim(),
            ExpectedTargetStateVersion = request.ExpectedTargetStateVersion,
            DecidedAt = now
        };
        db.ReviewerDecisions.Add(decisionRecord);

        string? operationalState = null;
        int? operationalStateVersion = null;
        if (decision == "APPROVE")
        {
            if (proposal.ExpectedTargetStateVersion is null || proposal.ExpectedTargetStateVersion != request.ExpectedTargetStateVersion)
            {
                throw Conflict("target_version_mismatch", "The target state changed", "The decision must reference the target-state version used to validate the proposal.");
            }

            var state = await db.TargetOperationalStates.SingleOrDefaultAsync(
                x => x.TargetType == proposal.TargetType && x.TargetId == proposal.TargetId,
                cancellationToken);
            if (state is null)
            {
                throw new CoastalOperationsException(StatusCodes.Status503ServiceUnavailable, "target_state_unavailable", "Current target state is unavailable", "The service cannot safely execute an operation until authoritative target state is available.");
            }

            if (state.Version != request.ExpectedTargetStateVersion)
            {
                throw Conflict("target_version_stale", "The target state changed", "Refresh the target status and obtain a current proposal before deciding.");
            }

            var nextState = proposal.ProposedState!.Trim().ToUpperInvariant();
            if (!OperationsValidation.IsAllowedTransition(proposal.TargetType, state.State, nextState))
            {
                throw Conflict("state_transition_not_allowed", "The operation is not allowed", "The proposed state transition is not valid for this target type and current state.");
            }

            var previousState = state.State;
            state.State = nextState;
            state.Version++;
            state.UpdatedBy = actorId;
            state.UpdatedAt = now;
            assessment.WorkflowStatus = "EXECUTED";
            assessment.Version++;
            assessment.UpdatedAt = now;
            operationalState = state.State;
            operationalStateVersion = state.Version;
            decisionRecord.AppliedOperationalState = state.State;
            decisionRecord.AppliedOperationalStateVersion = state.Version;
            db.OperationalHistory.Add(new OperationalHistoryEntry
            {
                Id = Guid.CreateVersion7(),
                TargetType = state.TargetType,
                TargetId = state.TargetId,
                PreviousState = previousState,
                NewState = state.State,
                AssessmentId = assessment.Id,
                ProposalId = proposal.Id,
                DecisionId = decisionRecord.Id,
                ActorId = actorId,
                CorrelationId = correlationId,
                CreatedAt = now
            });
            db.OperationsAudit.Add(Audit("target", state.Id, "STATE_CHANGED", actorId, correlationId, now));
        }
        else
        {
            assessment.WorkflowStatus = decision == "REJECT" ? "REJECTED" : "REVISION_REQUESTED";
            assessment.Version++;
            assessment.UpdatedAt = now;
        }

        decisionRecord.WorkflowStatusAfterDecision = assessment.WorkflowStatus;

        db.OperationsAudit.Add(Audit("assessment", assessment.Id, $"DECISION_{decision}", actorId, correlationId, now));
        var response = new ReviewerDecisionResponse(
            decisionRecord.Id,
            assessment.Id,
            proposal.Id,
            proposal.ProposalVersion,
            decision,
            assessment.WorkflowStatus,
            operationalState,
            operationalStateVersion,
            actorId,
            decisionRecord.Explanation,
            request.ExpectedTargetStateVersion,
            now);

        try
        {
            return await SaveIdempotentAsync(
                actorId, $"assessment.decide:{assessmentId:N}", key, digest,
                StatusCodes.Status200OK, response, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Conflict("proposal_already_decided", "The proposal already has a decision", "A concurrent reviewer decision was recorded first.");
        }
    }

    private async Task<AssessmentAiAvailability> ReadAiAvailabilityAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            return await proposalPort.GetAvailabilityAsync(timeout.Token).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AssessmentAiAvailability.Unavailable;
        }
        catch (TimeoutException)
        {
            return AssessmentAiAvailability.Unavailable;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            // Agentic AI is optional before G07. Transport failures must leave
            // the business assessment usable and explicitly retryable.
            return AssessmentAiAvailability.Unavailable;
        }
    }

    private async Task InitializeTargetOperationalStateIfConfirmedAsync(
        string targetType,
        Guid targetId,
        IReadOnlyList<ComponentDependencyResult> dependencies,
        Guid actorId,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var experience = dependencies.SingleOrDefault(x => x.Service == "member-1-experience");
        if (!TargetOperationalStateBootstrapPolicy.IsConfirmedTarget(experience)) return;

        var stateId = Guid.CreateVersion7();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH inserted_state AS (
                INSERT INTO coastal_operations."TargetOperationalStates"
                    ("Id", "TargetType", "TargetId", "State", "Version", "UpdatedBy", "UpdatedAt")
                VALUES ({stateId}, {targetType}, {targetId}, 'OPEN', 1, {actorId}, {now})
                ON CONFLICT ("TargetType", "TargetId") DO NOTHING
                RETURNING "Id"
            )
            INSERT INTO coastal_operations."OperationsAudit"
                ("Id", "ResourceType", "ResourceId", "Action", "ActorId", "CorrelationId", "CreatedAt")
            SELECT {Guid.CreateVersion7()}, 'target', "Id", 'STATE_INITIALIZED', {actorId}, {correlationId}, {now}
            FROM inserted_state
            """, cancellationToken);
    }

    private async Task<StoredOutcome<T>> SaveIdempotentAsync<T>(
        Guid actorId, string operation, string key, string digest, int statusCode, T response, CancellationToken cancellationToken) =>
        await idempotencyStore.SaveAsync(actorId, operation, key, digest, statusCode, response, cancellationToken);

    private static AssessmentResponse ToResponse(Assessment x) => new(
        x.Id, x.WorkflowId, x.TargetType, x.TargetId, x.SourceWorkflowId,
        x.PeriodStartsAt, x.PeriodEndsAt, x.Objective, x.WorkflowStatus,
        x.AiDependencyStatus, x.AiDispatchOutcome, x.AiDispatchRetryable,
        JsonSerializer.Deserialize<List<ComponentDependencyResult>>(x.ComponentDependenciesJson, OperationsValidation.JsonOptions) ?? [],
        x.Version, x.CreatedAt, x.UpdatedAt);

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new CoastalOperationsException(StatusCodes.Status401Unauthorized, "actor_invalid", "Authentication is required", "A valid authenticated actor ID is required.");
    }

    private static OperationsAuditEntry Audit(string type, Guid id, string action, Guid actorId, string correlationId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), ResourceType = type, ResourceId = id, Action = action,
        ActorId = actorId, CorrelationId = correlationId, CreatedAt = now
    };

    private static CoastalOperationsException Invalid(string code, string title, string detail) =>
        new(StatusCodes.Status422UnprocessableEntity, code, title, detail);

    private static CoastalOperationsException NotFound(string code, string title, string detail) =>
        new(StatusCodes.Status404NotFound, code, title, detail);

    private static CoastalOperationsException Conflict(string code, string title, string detail) =>
        new(StatusCodes.Status409Conflict, code, title, detail);
}
