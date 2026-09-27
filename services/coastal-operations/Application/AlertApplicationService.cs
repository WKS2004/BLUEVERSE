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
        var period = OperationsValidation.ParsePeriod(request.ValidFrom, request.ValidUntil);
        if (request.AssessmentId is Guid assessmentId)
        {
            var assessment = await db.Assessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
            if (assessment is null)
                throw NotFound("assessment_not_found", "Assessment not found", "An alert can only reference an existing assessment.");
            if (assessment.TargetType != targetType || assessment.TargetId != request.TargetId)
                throw Invalid("alert_assessment_scope_mismatch", "Alert scope does not match the assessment", "The alert target must match its linked assessment target.");
        }

        var now = DateTimeOffset.UtcNow;
        var alert = new OperationalAlert
        {
            Id = Guid.CreateVersion7(),
            TargetType = targetType,
            TargetId = request.TargetId,
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
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        if (!OperationsValidation.TryReadCursor(query.Cursor, out var cursorId))
            throw Invalid("cursor_invalid", "The alert cursor is invalid", "Use the cursor returned by the previous page.");

        var now = DateTimeOffset.UtcNow;
        var alerts = AlertVisibilityPolicy.Apply(db.OperationalAlerts.AsNoTracking(), canManage, now);
        if (!string.IsNullOrWhiteSpace(query.Lifecycle))
        {
            var lifecycle = query.Lifecycle.Trim().ToUpperInvariant();
            if (lifecycle is not ("PROPOSED" or "ACTIVE" or "RESOLVED" or "EXPIRED" or "SUPERSEDED"))
                throw Invalid("alert_lifecycle_invalid", "Alert lifecycle is invalid", "Use a documented alert lifecycle value.");
            alerts = alerts.Where(x => x.Lifecycle == lifecycle);
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
        if (alert.Lifecycle != "PROPOSED")
            throw Conflict("alert_not_draft", "The alert is not a draft", "Published alert content cannot be edited; create a replacement draft.");
        if (alert.Version != request.ExpectedVersion)
            throw Conflict("alert_version_stale", "The alert draft changed", "Reload the draft and submit the latest version.");

        var title = request.Title.Trim();
        var description = request.Description.Trim();
        if (title.Length == 0 || description.Length == 0)
            throw Invalid("alert_content_required", "Alert content is required", "Provide a title and description.");
        var period = OperationsValidation.ParsePeriod(request.ValidFrom, request.ValidUntil);
        var now = DateTimeOffset.UtcNow;
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
            alert.ValidUntil, alert.Version, alert.CreatedAt, alert.UpdatedAt);
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
