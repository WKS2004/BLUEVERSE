using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Blueverse.CoastalOperations.Application;

public sealed class AlertApplicationService(CoastalOperationsDbContext db, IdempotencyStore idempotencyStore)
{
    public async Task<AlertResponse> CreateAsync(
        CreateAlertRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var targetType = OperationsValidation.NormalizeTargetType(request.TargetType);
        if (request.TargetId == Guid.Empty || request.AssessmentId == Guid.Empty)
            throw Invalid("alert_identity_invalid", "Alert scope is invalid", "Provide a non-empty target ID and omit an empty assessment ID.");

        var title = request.Title.Trim();
        var description = request.Description.Trim();
        if (title.Length == 0 || description.Length == 0)
            throw Invalid("alert_content_required", "Alert content is required", "Provide a title and description.");

        var severity = OperationsValidation.NormalizeSeverity(request.Severity);
        var visibility = OperationsValidation.NormalizeAlertVisibility(request.Visibility);
        var period = await OperationsTimeZones.ParsePeriodAsync(db, request.TimeZoneId, request.ValidFrom, request.ValidUntil, cancellationToken);
        if (request.AssessmentId is Guid assessmentId)
        {
            var assessment = await db.Assessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
            if (assessment is null)
                throw NotFound("assessment_not_found", "Assessment not found", "An alert can only reference an existing assessment.");
            if (assessment.TargetType != targetType || assessment.TargetId != (request.TargetId ?? Guid.Empty))
                throw Invalid("alert_assessment_scope_mismatch", "Alert scope does not match the assessment", "The alert target must match its linked assessment target.");
        }

        var now = DateTimeOffset.UtcNow;
        var alert = new OperationalAlert
        {
            Id = Guid.CreateVersion7(),
            TargetType = targetType,
            TargetId = request.TargetId ?? Guid.Empty,
            TimeZoneId = request.TimeZoneId,
            AssessmentId = request.AssessmentId,
            Title = title,
            Description = description,
            Severity = severity,
            Visibility = visibility,
            Lifecycle = "PROPOSED",
            ValidFrom = period.StartsAt,
            ValidUntil = period.EndsAt,
            CreatedBy = actorId,
            UpdatedBy = actorId,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.OperationalAlerts.Add(alert);
        db.OperationsAudit.Add(Audit(alert.Id, "CREATED", actorId, correlationId, now));
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(alert, now);
    }

    public async Task<AlertQueueResponse> GetQueueAsync(
        AlertListQuery query,
        Guid actorId,
        bool canManage,
        CancellationToken cancellationToken, bool auditView = false)
    {
        EnsureActor(actorId);
        if (!OperationsValidation.TryReadCursor(query.Cursor, out var cursorId))
            throw Invalid("cursor_invalid", "The alert cursor is invalid", "Use the cursor returned by the previous page.");

        var search = CoastalRecordQueries.Search(query.Search, query.PageSize);
        var severity = CoastalRecordQueries.Filter(query.Severity, "severity", "LOW", "MODERATE", "HIGH", "CRITICAL");
        var visibility = CoastalRecordQueries.Filter(query.Visibility, "audience", "PUBLIC", "OPERATIONS");
        var now = DateTimeOffset.UtcNow;
        var alerts = auditView
            ? db.OperationalAlerts.AsNoTracking().Where(x => canManage || x.CreatedBy == actorId)
            : AlertVisibilityPolicy.Apply(db.OperationalAlerts.AsNoTracking(), canManage, now);
        if (query.History)
        {
            if (!canManage && !auditView)
                throw new CoastalOperationsException(403, "alert_audit_forbidden", "Alert management access is required", "Historical alerts are available to authorized alert managers.");
            alerts = alerts.Where(x => x.Lifecycle == "RESOLVED" || x.Lifecycle == "EXPIRED" ||
                x.Lifecycle == "SUPERSEDED" || x.Lifecycle == "WITHDRAWN" || (x.Lifecycle == "ACTIVE" && x.ValidUntil <= now));
        }
        if (query.RecordId is Guid recordId) alerts = alerts.Where(x => x.Id == recordId);
        if (severity is not null) alerts = alerts.Where(x => x.Severity == severity);
        if (visibility is not null) alerts = alerts.Where(x => x.Visibility == visibility);
        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            var targetType = OperationsValidation.NormalizeTargetType(query.TargetType);
            alerts = alerts.Where(x => x.TargetType == targetType);
        }
        if (search is not null)
        {
            alerts = alerts.Where(x => x.Title.ToLower().Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.Lifecycle))
        {
            var lifecycle = query.Lifecycle.Trim().ToUpperInvariant();
            if (lifecycle is not ("PROPOSED" or "ACTIVE" or "RESOLVED" or "EXPIRED" or "SUPERSEDED" or "WITHDRAWN"))
                throw Invalid("alert_lifecycle_invalid", "Alert lifecycle is invalid", "Use a documented alert lifecycle value.");
            if (lifecycle == "WITHDRAWN" && !canManage && !auditView)
                throw new CoastalOperationsException(StatusCodes.Status403Forbidden, "alert_audit_forbidden", "Alert audit access is required", "Only an authorized alert manager can inspect withdrawn draft tombstones.");
            alerts = lifecycle == "EXPIRED"
                ? alerts.Where(x => x.Lifecycle == "EXPIRED" || (x.Lifecycle == "ACTIVE" && x.ValidUntil <= now))
                : lifecycle == "ACTIVE"
                    ? alerts.Where(x => x.Lifecycle == "ACTIVE" && x.ValidUntil > now)
                    : alerts.Where(x => x.Lifecycle == lifecycle);
        }
        else if (!query.History && !auditView)
        {
            alerts = alerts.Where(x => x.Lifecycle != "WITHDRAWN");
        }
        if (query.TargetId is Guid targetId) alerts = alerts.Where(x => x.TargetId == targetId);
        if (cursorId != Guid.Empty) alerts = alerts.Where(x => x.Id.CompareTo(cursorId) < 0);

        var page = await alerts.OrderByDescending(x => x.Id).Take(query.PageSize + 1).ToListAsync(cancellationToken);
        var hasMore = page.Count > query.PageSize;
        if (hasMore) page.RemoveAt(page.Count - 1);
        return new AlertQueueResponse(
            page.Select(x => ToResponse(x, now)).ToArray(),
            hasMore && page.Count > 0 ? OperationsValidation.EncodeCursor(page[^1].Id) : null);
    }

    public async Task<AlertResponse> UpdateDraftAsync(
        Guid alertId,
        UpdateAlertRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var alert = await db.OperationalAlerts.SingleOrDefaultAsync(x => x.Id == alertId, cancellationToken)
            ?? throw NotFound("alert_not_found", "Alert not found", "The alert does not exist.");
        if (alert.CreatedBy != actorId)
            throw NotFound("alert_not_found", "Alert not found", "The alert does not exist or is outside the caller's scope.");
        if (alert.Lifecycle != "PROPOSED")
            throw Conflict("alert_not_draft", "The alert is not a draft", "Published alert content cannot be edited; create a replacement draft.");
        if (alert.Version != request.ExpectedVersion)
            throw Conflict("alert_version_stale", "The alert draft changed", "Reload the draft and submit the latest version.");

        var title = request.Title.Trim();
        var description = request.Description.Trim();
        if (title.Length == 0 || description.Length == 0)
            throw Invalid("alert_content_required", "Alert content is required", "Provide a title and description.");
        var period = await OperationsTimeZones.ParsePeriodAsync(db, request.TimeZoneId, request.ValidFrom, request.ValidUntil, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (request.TargetId == Guid.Empty) throw Invalid("alert_identity_invalid", "Alert scope is invalid", "Select a real coastal record.");
        if (request.TargetId is Guid targetId)
        {
            var targetType = OperationsValidation.NormalizeTargetType(request.TargetType ?? alert.TargetType);
            if (alert.AssessmentId is Guid linked && !await db.Assessments.AnyAsync(x => x.Id == linked && x.TargetId == targetId && x.TargetType == targetType, cancellationToken))
                throw Invalid("alert_assessment_scope_mismatch", "Alert scope does not match the assessment", "Choose the linked assessment coastal record.");
            alert.TargetId = targetId; alert.TargetType = targetType;
        }
        alert.TimeZoneId = request.TimeZoneId;
        alert.Title = title;
        alert.Description = description;
        alert.Severity = OperationsValidation.NormalizeSeverity(request.Severity);
        if (request.Visibility is not null)
            alert.Visibility = OperationsValidation.NormalizeAlertVisibility(request.Visibility);
        alert.ValidFrom = period.StartsAt;
        alert.ValidUntil = period.EndsAt;
        alert.UpdatedBy = actorId;
        alert.UpdatedAt = now;
        alert.Version++;
        db.OperationsAudit.Add(Audit(alert.Id, "DRAFT_UPDATED", actorId, correlationId, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict("alert_version_stale", "The alert draft changed", "Reload the draft and submit the latest version.");
        }

        return ToResponse(alert, now);
    }

    public async Task<StoredOutcome<AlertResponse>> WithdrawDraftAsync(
        Guid alertId,
        WithdrawAlertDraftRequest request,
        Guid actorId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        if (request.ExpectedVersion <= 0)
            throw Invalid("alert_version_invalid", "Alert version is invalid", "Provide a positive expected alert version.");
        var key = OperationsValidation.ValidateIdempotencyKey(idempotencyKey);
        var operation = $"alert.withdraw:{alertId:N}";
        var digest = OperationsValidation.RequestDigest(request);
        var replay = await idempotencyStore.TryReplayAsync<AlertResponse>(actorId, operation, key, digest, cancellationToken);
        if (replay is not null) return replay;

        var alert = await db.OperationalAlerts.SingleOrDefaultAsync(x => x.Id == alertId, cancellationToken);
        if (alert is null || alert.CreatedBy != actorId)
            throw NotFound("alert_not_found", "Alert not found", "The alert does not exist or is outside the caller's scope.");
        if (alert.Lifecycle != "PROPOSED")
            throw Conflict("alert_not_withdrawable", "The alert draft is closed", "Only an owned PROPOSED alert draft can be withdrawn.");
        if (alert.Version != request.ExpectedVersion)
            throw Conflict("alert_version_stale", "The alert draft changed", "Reload the draft and submit the latest version.");

        var now = DateTimeOffset.UtcNow;
        alert.Lifecycle = "WITHDRAWN";
        alert.WithdrawnBy = actorId;
        alert.WithdrawnAt = now;
        alert.UpdatedBy = actorId;
        alert.UpdatedAt = now;
        alert.Version++;
        db.OperationsAudit.Add(Audit(alert.Id, "DRAFT_WITHDRAWN", actorId, correlationId, now));
        try
        {
            var response = ToResponse(alert, now);
            return await idempotencyStore.SaveAsync(
                actorId, operation, key, digest, StatusCodes.Status200OK, response, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict("alert_version_stale", "The alert draft changed", "A concurrent alert update was recorded first.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Conflict("alert_withdrawal_conflict", "The alert draft changed", "A concurrent alert update was recorded first.");
        }
    }

    public async Task<StoredOutcome<AlertDecisionResponse>> DecideAsync(
        Guid alertId,
        AlertDecisionRequest request,
        Guid actorId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var decision = OperationsValidation.NormalizeDecision(request.Decision, "PUBLISH", "RESOLVE");
        var key = OperationsValidation.ValidateIdempotencyKey(idempotencyKey);
        var digest = OperationsValidation.RequestDigest(request);
        var operation = $"alert.decide:{alertId:N}";
        var replay = await idempotencyStore.TryReplayAsync<AlertDecisionResponse>(actorId, operation, key, digest, cancellationToken);
        if (replay is not null) return replay;

        var alert = await db.OperationalAlerts.SingleOrDefaultAsync(x => x.Id == alertId, cancellationToken)
            ?? throw NotFound("alert_not_found", "Alert not found", "The alert does not exist.");
        if (alert.Version != request.ExpectedVersion)
            throw Conflict("alert_version_stale", "The alert changed", "Reload the alert and submit the latest version.");

        var now = DateTimeOffset.UtcNow;
        if (decision == "PUBLISH")
        {
            if (alert.TargetId == Guid.Empty) throw Invalid("target_required_for_publication", "Choose a coastal record before publishing", "Link this draft to a real coastal record when the catalogue is available.");
            if (alert.Lifecycle != "PROPOSED")
                throw Conflict("alert_not_publishable", "The alert cannot be published", "Only a proposed alert draft can be published.");
            if (alert.ValidUntil <= now)
                throw Conflict("alert_period_expired", "The alert period has expired", "Create a new alert with a future validity period.");

            var targetState = await db.TargetOperationalStates.AsNoTracking().SingleOrDefaultAsync(
                x => x.TargetType == alert.TargetType && x.TargetId == alert.TargetId,
                cancellationToken);
            if (targetState is null)
            {
                throw new CoastalOperationsException(
                    StatusCodes.Status503ServiceUnavailable,
                    "target_state_unavailable",
                    "Current target state is unavailable",
                    "The service cannot safely publish an alert until the target's authoritative state is available.");
            }
            if (targetState.State is "CANCELLED" or "COMPLETED")
                throw Conflict("target_terminal", "The target is no longer active", "An alert cannot be published for a cancelled or completed session.");

            if (alert.Severity is "HIGH" or "CRITICAL")
            {
                if (alert.CreatedBy == actorId)
                    throw new CoastalOperationsException(StatusCodes.Status403Forbidden, "independent_reviewer_required", "A different reviewer is required", "The alert drafter cannot publish a HIGH or CRITICAL alert.");
                if (alert.AssessmentId is Guid assessmentId &&
                    await db.Assessments.AnyAsync(x => x.Id == assessmentId && x.InitiatedBy == actorId, cancellationToken))
                {
                    throw new CoastalOperationsException(StatusCodes.Status403Forbidden, "independent_reviewer_required", "A different reviewer is required", "The associated assessment initiator cannot publish a HIGH or CRITICAL alert.");
                }
            }

            alert.Lifecycle = "ACTIVE";
        }
        else
        {
            if (alert.Lifecycle != "ACTIVE")
                throw Conflict("alert_not_resolvable", "The alert is not active", "Only an active alert can be resolved.");
            alert.Lifecycle = "RESOLVED";
        }

        alert.UpdatedBy = actorId;
        alert.UpdatedAt = now;
        alert.Version++;
        var decisionRecord = new AlertDecision
        {
            Id = Guid.CreateVersion7(), AlertId = alert.Id, Decision = decision,
            ActorId = actorId, ExpectedVersion = request.ExpectedVersion, DecidedAt = now
        };
        db.AlertDecisions.Add(decisionRecord);
        db.OperationsAudit.Add(Audit(alert.Id, decision, actorId, correlationId, now));
        var response = new AlertDecisionResponse(decisionRecord.Id, alert.Id, decision, alert.Lifecycle, alert.Version, now);
        try
        {
            return await idempotencyStore.SaveAsync(actorId, operation, key, digest, StatusCodes.Status200OK, response, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict("alert_version_stale", "The alert changed", "A concurrent alert decision was recorded first.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Conflict("alert_decision_conflict", "The alert changed", "A concurrent alert decision was recorded first.");
        }
    }

    private static AlertResponse ToResponse(OperationalAlert alert, DateTimeOffset now)
    {
        var lifecycle = alert.Lifecycle == "ACTIVE" && alert.ValidUntil <= now ? "EXPIRED" : alert.Lifecycle;
        return new AlertResponse(alert.Id, alert.TargetType, alert.TargetId, alert.AssessmentId,
            alert.Title, alert.Description, alert.Severity, alert.Visibility, lifecycle, alert.ValidFrom,
            alert.ValidUntil, alert.Version, alert.CreatedAt, alert.UpdatedAt, alert.WithdrawnBy, alert.WithdrawnAt, alert.TimeZoneId, OperationsTimeZones.LocalValue(alert.ValidFrom, alert.TimeZoneId), OperationsTimeZones.LocalValue(alert.ValidUntil, alert.TimeZoneId));
    }

    private static OperationsAuditEntry Audit(Guid alertId, string action, Guid actorId, string correlationId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), ResourceType = "alert", ResourceId = alertId,
        Action = action, ActorId = actorId, CorrelationId = correlationId, CreatedAt = now
    };

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new CoastalOperationsException(StatusCodes.Status401Unauthorized, "actor_invalid", "Authentication is required", "A valid authenticated actor ID is required.");
    }

    private static CoastalOperationsException Invalid(string code, string title, string detail) =>
        new(StatusCodes.Status422UnprocessableEntity, code, title, detail);
    private static CoastalOperationsException NotFound(string code, string title, string detail) =>
        new(StatusCodes.Status404NotFound, code, title, detail);
    private static CoastalOperationsException Conflict(string code, string title, string detail) =>
        new(StatusCodes.Status409Conflict, code, title, detail);
}
