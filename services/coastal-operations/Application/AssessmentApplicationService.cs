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
    IComponentDependencyCollector componentDependencyCollector)
{
    private static readonly HashSet<string> WorkflowStatuses =
    ["DRAFT", "SUBMITTED", "PROPOSAL_READY", "PENDING_APPROVAL", "REVISION_REQUESTED", "REJECTED", "APPROVED", "EXECUTED", "BLOCKED", "SAFE_FAILURE", "CANCELLED"];

    public async Task<StoredOutcome<AssessmentResponse>> CreateAsync(
        CreateAssessmentRequest request,
        Guid actorId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var key = OperationsValidation.ValidateIdempotencyKey(idempotencyKey);
        var digest = OperationsValidation.AssessmentDraftDigest(request);
        var replay = await idempotencyStore.TryReplayAsync<AssessmentResponse>(
            actorId, "assessment.create", key, digest, cancellationToken);
        if (replay is not null) return replay;

        var targetType = OperationsValidation.NormalizeTargetType(request.TargetType);
        if (request.TargetId == Guid.Empty || (request.TargetId is null && string.IsNullOrWhiteSpace(request.Title)) || (request.SourceWorkflowId == Guid.Empty))
        {
            throw Invalid("target_identity_invalid", "Target identity is invalid", "Provide a non-empty target ID and omit an empty source workflow ID.");
        }

        var objective = request.Objective.Trim();
        if (objective.Length == 0)
        {
            throw Invalid("objective_required", "An assessment objective is required", "Describe the operational question to assess.");
        }

        var title = OperationsTimeZones.Title(request.Title, objective);
        var period = await OperationsTimeZones.ParsePeriodAsync(db, request.TimeZoneId, request.PeriodStartsAt, request.PeriodEndsAt, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var assessment = new Assessment
        {
            Id = Guid.CreateVersion7(),
            WorkflowId = Guid.CreateVersion7(),
            TargetType = targetType,
            TargetId = request.TargetId ?? Guid.Empty,
            Title = title, TimeZoneId = request.TimeZoneId,
            SourceWorkflowId = request.SourceWorkflowId,
            PeriodStartsAt = period.StartsAt,
            PeriodEndsAt = period.EndsAt,
            Objective = objective,
            WorkflowStatus = "DRAFT",
            AiDependencyStatus = "NOT_CONNECTED",
            AiDispatchOutcome = "NOT_REQUESTED",
            AiDispatchRetryable = false,
            ComponentDependenciesJson = "[]",
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

    public async Task<AssessmentResponse> UpdateDraftAsync(
        Guid assessmentId,
        UpdateAssessmentDraftRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        if (request.ExpectedVersion <= 0)
            throw Invalid("assessment_version_invalid", "Assessment version is invalid", "Provide a positive expected assessment version.");

        var assessment = await db.Assessments.SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null || assessment.InitiatedBy != actorId)
            throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist or is outside the caller's scope.");
        if (assessment.WorkflowStatus != "DRAFT")
            throw Conflict("assessment_draft_closed", "The assessment draft is closed", "Only an owned DRAFT assessment can be edited.");
        if (assessment.Version != request.ExpectedVersion)
            throw Conflict("assessment_version_stale", "The assessment draft changed", "Reload the draft and submit the latest version.");

        var targetType = OperationsValidation.NormalizeTargetType(request.TargetType);
        if (request.TargetId == Guid.Empty || (request.TargetId is null && string.IsNullOrWhiteSpace(request.Title)) || request.SourceWorkflowId == Guid.Empty)
            throw Invalid("target_identity_invalid", "Target identity is invalid", "Provide a non-empty target ID and omit an empty source workflow ID.");
        var objective = request.Objective.Trim();
        if (objective.Length == 0)
            throw Invalid("objective_required", "An assessment objective is required", "Describe the operational question to assess.");
        var title = OperationsTimeZones.Title(request.Title, objective);
        var period = await OperationsTimeZones.ParsePeriodAsync(db, request.TimeZoneId, request.PeriodStartsAt, request.PeriodEndsAt, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        assessment.TargetType = targetType;
        assessment.TargetId = request.TargetId ?? Guid.Empty;
        assessment.Title = title;
        assessment.TimeZoneId = request.TimeZoneId;
        assessment.SourceWorkflowId = request.SourceWorkflowId;
        assessment.PeriodStartsAt = period.StartsAt;
        assessment.PeriodEndsAt = period.EndsAt;
        assessment.Objective = objective;
        assessment.Version++;
        assessment.UpdatedAt = now;
        db.OperationsAudit.Add(Audit("assessment", assessment.Id, "DRAFT_UPDATED", actorId, correlationId, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict("assessment_version_stale", "The assessment draft changed", "Reload the draft and submit the latest version.");
        }

        return ToResponse(assessment);
    }

    public async Task<StoredOutcome<AssessmentResponse>> CancelDraftAsync(
        Guid assessmentId,
        CancelAssessmentDraftRequest request,
        Guid actorId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        if (request.ExpectedVersion <= 0)
            throw Invalid("assessment_version_invalid", "Assessment version is invalid", "Provide a positive expected assessment version.");
        var key = OperationsValidation.ValidateIdempotencyKey(idempotencyKey);
        var operation = $"assessment.cancel:{assessmentId:N}";
        var digest = OperationsValidation.RequestDigest(request);
        var replay = await idempotencyStore.TryReplayAsync<AssessmentResponse>(actorId, operation, key, digest, cancellationToken);
        if (replay is not null) return replay;

        var assessment = await db.Assessments.SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null || assessment.InitiatedBy != actorId)
            throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist or is outside the caller's scope.");
        if (assessment.WorkflowStatus != "DRAFT")
            throw Conflict("assessment_draft_closed", "The assessment draft is closed", "Only an owned DRAFT assessment can be cancelled.");
        if (assessment.Version != request.ExpectedVersion)
            throw Conflict("assessment_version_stale", "The assessment draft changed", "Reload the draft and submit the latest version.");

        var now = DateTimeOffset.UtcNow;
        assessment.WorkflowStatus = "CANCELLED";
        assessment.CancelledBy = actorId;
        assessment.CancelledAt = now;
        assessment.Version++;
        assessment.UpdatedAt = now;
        db.OperationsAudit.Add(Audit("assessment", assessment.Id, "CANCELLED", actorId, correlationId, now));
        return await SaveIdempotentAsync(
            actorId, operation, key, digest, StatusCodes.Status200OK, ToResponse(assessment), cancellationToken);
    }

    public async Task<StoredOutcome<AssessmentResponse>> SubmitDraftAsync(
        Guid assessmentId,
        SubmitAssessmentDraftRequest request,
        Guid actorId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        if (request.ExpectedVersion <= 0)
            throw Invalid("assessment_version_invalid", "Assessment version is invalid", "Provide a positive expected assessment version.");
        var key = OperationsValidation.ValidateIdempotencyKey(idempotencyKey);
        var operation = $"assessment.submit:{assessmentId:N}";
        var digest = OperationsValidation.RequestDigest(request);
        var replay = await idempotencyStore.TryReplayAsync<AssessmentResponse>(actorId, operation, key, digest, cancellationToken);
        if (replay is not null) return replay;

        var assessment = await db.Assessments.SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null || assessment.InitiatedBy != actorId)
            throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist or is outside the caller's scope.");
        if (assessment.WorkflowStatus != "DRAFT")
            throw Conflict("assessment_draft_closed", "The assessment draft is closed", "Only an owned DRAFT assessment can be submitted.");
        if (assessment.Version != request.ExpectedVersion)
            throw Conflict("assessment_version_stale", "The assessment draft changed", "Reload the draft and submit the latest version.");

        if (assessment.TargetId == Guid.Empty)
            throw Invalid("target_required_for_publication", "Choose a coastal record before publishing", "Link this draft to a real coastal record when the catalogue is available.");
        var componentDependencies = await componentDependencyCollector.CollectAsync(
            assessment.TargetType,
            assessment.TargetId,
            assessment.PeriodStartsAt,
            assessment.PeriodEndsAt,
            assessment.SourceWorkflowId,
            cancellationToken);
        return await SaveSubmissionIdempotentlyAsync(
            assessmentId, request.ExpectedVersion, componentDependencies, actorId, operation, key, digest,
            correlationId, cancellationToken);
    }

    public async Task<AssessmentQueueResponse> GetQueueAsync(
        AssessmentListQuery query,
        Guid actorId,
        bool canReadQueue,
        CancellationToken cancellationToken, bool auditView = false)
    {
        EnsureActor(actorId);
        if (query.IncludeCancelled && !canReadQueue && !auditView)
            throw new CoastalOperationsException(StatusCodes.Status403Forbidden, "assessment_audit_forbidden", "Assessment audit access is required", "Only an authorized assessment queue reader can include cancelled draft tombstones.");
        if (!OperationsValidation.TryReadCursor(query.Cursor, out var cursorId))
        {
            throw Invalid("cursor_invalid", "The assessment cursor is invalid", "Use the cursor returned by the previous page.");
        }

        var search = CoastalRecordQueries.Search(query.Search, query.PageSize);
        var items = db.Assessments.AsNoTracking().AsQueryable();
        if (canReadQueue && !query.OnlyMine)
        {
            items = items.Where(x => (x.WorkflowStatus != "DRAFT" || x.InitiatedBy == actorId) && (auditView || query.IncludeCancelled || x.WorkflowStatus != "CANCELLED"));
        }
        else
        {
            items = items.Where(x => x.InitiatedBy == actorId && (auditView || query.IncludeCancelled || x.WorkflowStatus != "CANCELLED"));
        }
        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            var targetType = OperationsValidation.NormalizeTargetType(query.TargetType);
            items = items.Where(x => x.TargetType == targetType);
        }
        if (query.TargetId is Guid targetId) items = items.Where(x => x.TargetId == targetId);
        if (query.RecordId is Guid recordId) items = items.Where(x => x.Id == recordId);
        if (query.PublishedOnly) items = items.Where(x => x.WorkflowStatus != "DRAFT" && x.WorkflowStatus != "CANCELLED");
        if (search is not null)
        {
            items = items.Where(x => (x.Title != "" ? x.Title.ToLower().Contains(search) : x.Objective.ToLower().Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(query.WorkflowStatus))
        {
            var status = query.WorkflowStatus.Trim().ToUpperInvariant();
            if (!WorkflowStatuses.Contains(status))
            {
                throw Invalid("workflow_status_invalid", "The workflow status is invalid", "Use a documented Coastal Operations workflow status.");
            }
            if (status == "CANCELLED" && !query.IncludeCancelled && !auditView)
                throw new CoastalOperationsException(StatusCodes.Status403Forbidden, "assessment_audit_forbidden", "Assessment audit access is required", "Use the authorized includeCancelled audit filter to read cancelled draft tombstones.");

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
        if (assessment is null || (assessment.InitiatedBy != actorId && (!canReadQueue || assessment.WorkflowStatus == "DRAFT")))
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

    private async Task<StoredOutcome<AssessmentResponse>> SaveSubmissionIdempotentlyAsync(
        Guid assessmentId,
        int expectedVersion,
        IReadOnlyList<ComponentDependencyResult> dependencies,
        Guid actorId,
        string operation,
        string key,
        string digest,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            var assessment = RequireSubmittableDraft(
                await db.Assessments.SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken),
                actorId, expectedVersion);
            var now = DateTimeOffset.UtcNow;
            await MarkSubmittedAsync(assessment, dependencies, actorId, correlationId, now, cancellationToken);
            await InitializeTargetOperationalStateIfConfirmedAsync(
                assessment.TargetType, assessment.TargetId, dependencies, actorId, correlationId, now, cancellationToken);
            return await SaveIdempotentAsync(
                actorId, operation, key, digest, StatusCodes.Status200OK, ToResponse(assessment), cancellationToken);
        }

        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    db.ChangeTracker.Clear();
                    var replay = await idempotencyStore.TryReplayAsync<AssessmentResponse>(
                        actorId, operation, key, digest, cancellationToken);
                    if (replay is not null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return replay;
                    }

                    var assessment = RequireSubmittableDraft(
                        await db.Assessments.SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken),
                        actorId, expectedVersion);
                    var now = DateTimeOffset.UtcNow;
                    await MarkSubmittedAsync(assessment, dependencies, actorId, correlationId, now, cancellationToken);
                    await InitializeTargetOperationalStateIfConfirmedAsync(
                        assessment.TargetType, assessment.TargetId, dependencies, actorId, correlationId, now, cancellationToken);
                    var result = await SaveIdempotentAsync(
                        actorId, operation, key, digest, StatusCodes.Status200OK, ToResponse(assessment), cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var replay = await idempotencyStore.TryReplayAsync<AssessmentResponse>(
                actorId, operation, key, digest, cancellationToken);
            if (replay is not null) return replay;
            throw Conflict("assessment_version_stale", "The assessment draft changed", "Reload the draft and retry submission with its current version.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            var replay = await idempotencyStore.TryReplayAsync<AssessmentResponse>(actorId, operation, key, digest, cancellationToken);
            if (replay is not null) return replay;
            throw Conflict("assessment_submission_conflict", "The assessment draft changed", "A concurrent submission or update was recorded first.");
        }
    }

    private static Assessment RequireSubmittableDraft(Assessment? assessment, Guid actorId, int expectedVersion)
    {
        if (assessment is null || assessment.InitiatedBy != actorId)
            throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist or is outside the caller's scope.");
        if (assessment.WorkflowStatus != "DRAFT")
            throw Conflict("assessment_draft_closed", "The assessment draft is closed", "Only an owned DRAFT assessment can be submitted.");
        if (assessment.Version != expectedVersion)
            throw Conflict("assessment_version_stale", "The assessment draft changed", "Reload the draft and submit the latest version.");
        return assessment;
    }

    private async Task MarkSubmittedAsync(
        Assessment assessment,
        IReadOnlyList<ComponentDependencyResult> dependencies,
        Guid actorId,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        assessment.WorkflowStatus = "SUBMITTED";
        assessment.AiDependencyStatus = "NOT_CONNECTED";
        assessment.AiDispatchOutcome = "NOT_REQUESTED";
        assessment.AiDispatchRetryable = false;
        assessment.ComponentDependenciesJson = JsonSerializer.Serialize(dependencies, OperationsValidation.JsonOptions);
        assessment.Version++;
        assessment.UpdatedAt = now;
        db.OperationsAudit.Add(Audit("assessment", assessment.Id, "SUBMITTED", actorId, correlationId, now));
        var evidence = await db.AssessmentEvidence.AsNoTracking()
            .Where(x => x.AssessmentId == assessment.Id && x.InspectionStatus == "AVAILABLE" && x.ExpiresAt > now)
            .OrderBy(x => x.UploadedAt)
            .Select(x => new AssessmentEvidenceResponse(x.Id, x.AssessmentVersion, x.MediaType,
                x.ByteLength, x.ContentSha256, x.InspectionStatus, x.UploadedAt, x.ExpiresAt))
            .ToListAsync(cancellationToken);
        var dispatchId = Guid.CreateVersion7();
        var payload = new PublishedAssessmentDispatch(dispatchId, assessment.WorkflowId,
            assessment.Id, assessment.Version, assessment.TargetType, assessment.TargetId,
            assessment.SourceWorkflowId, assessment.PeriodStartsAt, assessment.PeriodEndsAt,
            assessment.Objective, actorId, correlationId, now, dependencies, evidence, assessment.Title, assessment.TimeZoneId);
        db.AssessmentDispatches.Add(new AssessmentDispatch
        {
            Id = dispatchId, AssessmentId = assessment.Id, WorkflowId = assessment.WorkflowId,
            PayloadJson = JsonSerializer.Serialize(payload, OperationsValidation.JsonOptions),
            ActorId = actorId, CorrelationId = correlationId, NextAttemptAt = now,
            CreatedAt = now, UpdatedAt = now
        });
    }

    private static AssessmentResponse ToResponse(Assessment x) => new(
        x.Id, x.WorkflowId, x.TargetType, x.TargetId, x.SourceWorkflowId,
        x.PeriodStartsAt, x.PeriodEndsAt, x.Objective, x.WorkflowStatus,
        x.AiDependencyStatus, x.AiDispatchOutcome, x.AiDispatchRetryable,
        JsonSerializer.Deserialize<List<ComponentDependencyResult>>(x.ComponentDependenciesJson, OperationsValidation.JsonOptions) ?? [],
        x.Version, x.CreatedAt, x.UpdatedAt, x.CancelledBy, x.CancelledAt, x.Title, x.TimeZoneId, OperationsTimeZones.LocalValue(x.PeriodStartsAt, x.TimeZoneId), OperationsTimeZones.LocalValue(x.PeriodEndsAt, x.TimeZoneId));

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
