using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class AlertApplicationServiceTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");

    [Fact(DisplayName = "COASTAL-ALERT-003 alert creation trims content and persists an unpublished draft and audit")]
    [Trait("TestId", "COASTAL-ALERT-003")]
    public async Task CreatePersistsNormalizedDraft()
    {
        await using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var request = CreateRequest(title: "  High surf advisory  ", description: "  Monitor shoreline conditions.  ", severity: "moderate", visibility: "public");

        var response = await CreateService(db).CreateAsync(request, actorId, "corr-alert-create", CancellationToken.None);
        var saved = await db.OperationalAlerts.SingleAsync();

        Assert.Equal(response.AlertId, saved.Id);
        Assert.Equal("ACTIVITY", response.TargetType);
        Assert.Equal("High surf advisory", response.Title);
        Assert.Equal("Monitor shoreline conditions.", response.Description);
        Assert.Equal("MODERATE", response.Severity);
        Assert.Equal("PUBLIC", response.Visibility);
        Assert.Equal("PROPOSED", response.Lifecycle);
        Assert.Equal(1, response.Version);
        Assert.Equal(actorId, saved.CreatedBy);
        Assert.Equal(actorId, saved.UpdatedBy);
        var audit = await db.OperationsAudit.SingleAsync();
        Assert.Equal("CREATED", audit.Action);
        Assert.Equal("alert", audit.ResourceType);
        Assert.Equal(response.AlertId, audit.ResourceId);
        Assert.Equal("corr-alert-create", audit.CorrelationId);
    }

    [Fact(DisplayName = "COASTAL-ALERT-004 missing actor and target identity are rejected without writes")]
    [Trait("TestId", "COASTAL-ALERT-004")]
    public async Task CreateRejectsMissingActorAndTarget()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var actorError = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.CreateAsync(CreateRequest(), Guid.Empty, "corr", CancellationToken.None));
        var targetError = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.CreateAsync(CreateRequest(targetId: Guid.Empty), Guid.NewGuid(), "corr", CancellationToken.None));

        Assert.Equal(StatusCodes.Status401Unauthorized, actorError.StatusCode);
        Assert.Equal("actor_invalid", actorError.Code);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, targetError.StatusCode);
        Assert.Equal("alert_identity_invalid", targetError.Code);
        Assert.Empty(await db.OperationalAlerts.ToListAsync());
        Assert.Empty(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-005 optional assessment must exist and match the alert target")]
    [Trait("TestId", "COASTAL-ALERT-005")]
    public async Task CreateValidatesLinkedAssessmentScope()
    {
        await using var db = CreateDb();
        var assessmentId = Guid.NewGuid();
        db.Assessments.Add(Assessment(assessmentId, TargetId, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var unknown = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.CreateAsync(
            CreateRequest(assessmentId: Guid.NewGuid()), Guid.NewGuid(), "corr", CancellationToken.None));
        var mismatch = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.CreateAsync(
            CreateRequest(targetId: Guid.NewGuid(), assessmentId: assessmentId), Guid.NewGuid(), "corr", CancellationToken.None));
        var created = await service.CreateAsync(
            CreateRequest(assessmentId: assessmentId), Guid.NewGuid(), "corr", CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, unknown.StatusCode);
        Assert.Equal("assessment_not_found", unknown.Code);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, mismatch.StatusCode);
        Assert.Equal("alert_assessment_scope_mismatch", mismatch.Code);
        Assert.Equal(assessmentId, created.AssessmentId);
        Assert.Single(await db.OperationalAlerts.ToListAsync());
    }

    [Theory(DisplayName = "COASTAL-ALERT-006 blank content and invalid alert classifications are rejected")]
    [Trait("TestId", "COASTAL-ALERT-006")]
    [InlineData("   ", "Alert text", "LOW", "PUBLIC", "alert_content_required", null, null)]
    [InlineData("Title", "   ", "LOW", "PUBLIC", "alert_content_required", null, null)]
    [InlineData("Title", "Alert text", "EXTREME", "PUBLIC", "alert_severity_invalid", null, null)]
    [InlineData("Title", "Alert text", "LOW", "PRIVATE", "alert_visibility_invalid", null, null)]
    [InlineData("Title", "Alert text", "LOW", "PUBLIC", "period_offset_required", "invalid-date", "2026-10-01T10:00:00Z")]
    public async Task CreateRejectsInvalidContentAndScopeFields(
        string title,
        string description,
        string severity,
        string visibility,
        string expectedCode,
        string? startsAt = null,
        string? endsAt = null)
    {
        await using var db = CreateDb();
        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => CreateService(db).CreateAsync(
            CreateRequest(title, description, severity, visibility, startsAt: startsAt, endsAt: endsAt),
            Guid.NewGuid(), "corr", CancellationToken.None));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal(expectedCode, exception.Code);
        Assert.Empty(await db.OperationalAlerts.ToListAsync());
        Assert.Empty(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-007 only a proposed draft can be edited with its current version")]
    [Trait("TestId", "COASTAL-ALERT-007")]
    public async Task DraftUpdateAdvancesVersionAndAudit()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), actor, "create", CancellationToken.None);

        var updated = await service.UpdateDraftAsync(created.AlertId, UpdateRequest(expectedVersion: 1), actor, "update", CancellationToken.None);
        var entity = await db.OperationalAlerts.SingleAsync();

        Assert.Equal(2, updated.Version);
        Assert.Equal("Updated alert", updated.Title);
        Assert.Equal("CRITICAL", updated.Severity);
        Assert.Equal("OPERATIONS", updated.Visibility);
        Assert.Equal("PROPOSED", updated.Lifecycle);
        Assert.Equal(actor, entity.UpdatedBy);
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
        Assert.Contains(await db.OperationsAudit.ToListAsync(), audit => audit.Action == "DRAFT_UPDATED" && audit.CorrelationId == "update");
    }

    [Fact(DisplayName = "COASTAL-ALERT-008 omitted visibility retains the draft audience")]
    [Trait("TestId", "COASTAL-ALERT-008")]
    public async Task DraftUpdateRetainsVisibilityWhenOmitted()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(visibility: "PUBLIC"), owner, "create", CancellationToken.None);

        var updated = await service.UpdateDraftAsync(created.AlertId, UpdateRequest(visibility: null), owner, "update", CancellationToken.None);

        Assert.Equal("PUBLIC", updated.Visibility);
        Assert.Equal(2, updated.Version);
    }

    [Fact(DisplayName = "COASTAL-ALERT-009 draft update rejects missing, closed and stale alerts without mutation")]
    [Trait("TestId", "COASTAL-ALERT-009")]
    public async Task DraftUpdateRejectsMissingClosedAndStaleRows()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), actor, "create", CancellationToken.None);
        var alert = await db.OperationalAlerts.SingleAsync();

        var missing = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.UpdateDraftAsync(Guid.NewGuid(), UpdateRequest(1), actor, "update", CancellationToken.None));
        alert.Version = 2;
        var stale = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.UpdateDraftAsync(created.AlertId, UpdateRequest(1), actor, "update", CancellationToken.None));
        alert.Lifecycle = "ACTIVE";
        var closed = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.UpdateDraftAsync(created.AlertId, UpdateRequest(2), actor, "update", CancellationToken.None));

        Assert.Equal("alert_not_found", missing.Code);
        Assert.Equal("alert_version_stale", stale.Code);
        Assert.Equal("alert_not_draft", closed.Code);
        Assert.Equal(1, await db.OperationsAudit.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-010 visible alert queries enforce audience, lifecycle, target and cursor filters")]
    [Trait("TestId", "COASTAL-ALERT-010")]
    public async Task ListAppliesAllRequestedFilters()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var visibleIds = new[]
        {
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2")
        };
        db.OperationalAlerts.AddRange(
            Alert(visibleIds[0], "PUBLIC", "ACTIVE", now.AddMinutes(-1), now.AddMinutes(10), TargetId, Guid.NewGuid()),
            Alert(visibleIds[1], "PUBLIC", "ACTIVE", now.AddMinutes(-1), now.AddMinutes(10), TargetId, Guid.NewGuid()),
            Alert(Guid.NewGuid(), "OPERATIONS", "ACTIVE", now.AddMinutes(-1), now.AddMinutes(10), TargetId, Guid.NewGuid()),
            Alert(Guid.NewGuid(), "PUBLIC", "PROPOSED", now.AddMinutes(-1), now.AddMinutes(10), TargetId, Guid.NewGuid()),
            Alert(Guid.NewGuid(), "PUBLIC", "ACTIVE", now.AddMinutes(-1), now.AddMinutes(10), Guid.NewGuid(), Guid.NewGuid()));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var publicOnly = await service.GetQueueAsync(new AlertListQuery { Lifecycle = "active", TargetId = TargetId, PageSize = 1 }, Guid.NewGuid(), false, CancellationToken.None);
        var nextPage = await service.GetQueueAsync(new AlertListQuery { Lifecycle = "ACTIVE", TargetId = TargetId, PageSize = 1, Cursor = publicOnly.NextCursor }, Guid.NewGuid(), false, CancellationToken.None);
        var manager = await service.GetQueueAsync(new AlertListQuery { Lifecycle = "PROPOSED", TargetId = TargetId }, Guid.NewGuid(), true, CancellationToken.None);

        Assert.Single(publicOnly.Items);
        Assert.Equal(visibleIds[1], publicOnly.Items[0].AlertId);
        Assert.NotNull(publicOnly.NextCursor);
        Assert.Single(nextPage.Items);
        Assert.Equal(visibleIds[0], nextPage.Items[0].AlertId);
        Assert.Null(nextPage.NextCursor);
        Assert.Single(manager.Items);
        Assert.Equal("PROPOSED", manager.Items[0].Lifecycle);
    }

    [Fact(DisplayName = "COASTAL-ALERT-011 invalid actor, cursor and lifecycle are rejected")]
    [Trait("TestId", "COASTAL-ALERT-011")]
    public async Task ListRejectsInvalidActorAndFilters()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var actor = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.GetQueueAsync(new AlertListQuery(), Guid.Empty, false, CancellationToken.None));
        var cursor = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.GetQueueAsync(new AlertListQuery { Cursor = "%%%" }, Guid.NewGuid(), false, CancellationToken.None));
        var lifecycle = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.GetQueueAsync(new AlertListQuery { Lifecycle = "ARCHIVED" }, Guid.NewGuid(), false, CancellationToken.None));

        Assert.Equal("actor_invalid", actor.Code);
        Assert.Equal("cursor_invalid", cursor.Code);
        Assert.Equal("alert_lifecycle_invalid", lifecycle.Code);
        Assert.Empty(await db.OperationalAlerts.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-012 publishing is idempotent and records the active lifecycle")]
    [Trait("TestId", "COASTAL-ALERT-012")]
    public async Task PublishIsIdempotentAndAudited()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        db.TargetOperationalStates.Add(TargetState("OPEN"));
        await db.SaveChangesAsync();
        var created = await service.CreateAsync(CreateRequest(severity: "LOW"), actor, "create", CancellationToken.None);
        var request = new AlertDecisionRequest { Decision = "publish", ExpectedVersion = 1 };

        var first = await service.DecideAsync(created.AlertId, request, actor, "publish", "publish-1", CancellationToken.None);
        var replay = await service.DecideAsync(created.AlertId, request, actor, "ignored-on-replay", "publish-1", CancellationToken.None);
        var alert = await db.OperationalAlerts.SingleAsync();

        Assert.Equal(StatusCodes.Status200OK, first.StatusCode);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Body, replay.Body);
        Assert.Equal("ACTIVE", first.Body.Lifecycle);
        Assert.Equal(2, alert.Version);
        Assert.Equal("ACTIVE", alert.Lifecycle);
        Assert.Single(await db.AlertDecisions.ToListAsync());
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-013 reusing a decision key with changed content is conflict without a second action")]
    [Trait("TestId", "COASTAL-ALERT-013")]
    public async Task ChangedDecisionCannotReuseIdempotencyKey()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        db.TargetOperationalStates.Add(TargetState("OPEN"));
        await db.SaveChangesAsync();
        var created = await service.CreateAsync(CreateRequest(severity: "LOW"), actor, "create", CancellationToken.None);
        await service.DecideAsync(created.AlertId, new AlertDecisionRequest { Decision = "PUBLISH", ExpectedVersion = 1 }, actor, "publish", "same-key", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            created.AlertId, new AlertDecisionRequest { Decision = "RESOLVE", ExpectedVersion = 1 }, actor, "resolve", "same-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("idempotency_key_reused", exception.Code);
        Assert.Equal("ACTIVE", (await db.OperationalAlerts.SingleAsync()).Lifecycle);
        Assert.Single(await db.AlertDecisions.ToListAsync());
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-014 target authority is required before publishing")]
    [Trait("TestId", "COASTAL-ALERT-014")]
    public async Task PublishFailsClosedWithoutTargetState()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(severity: "LOW"), Guid.NewGuid(), "create", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            created.AlertId, new AlertDecisionRequest { Decision = "PUBLISH", ExpectedVersion = 1 }, Guid.NewGuid(), "publish", "publish-1", CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
        Assert.Equal("target_state_unavailable", exception.Code);
        Assert.Equal("PROPOSED", (await db.OperationalAlerts.SingleAsync()).Lifecycle);
        Assert.Empty(await db.AlertDecisions.ToListAsync());
        Assert.Single(await db.OperationsAudit.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Theory(DisplayName = "COASTAL-ALERT-015 alerts cannot be published for terminal session states")]
    [Trait("TestId", "COASTAL-ALERT-015")]
    [InlineData("CANCELLED")]
    [InlineData("COMPLETED")]
    public async Task PublishRejectsTerminalSession(string state)
    {
        await using var db = CreateDb();
        db.TargetOperationalStates.Add(TargetState(state, targetType: "SESSION"));
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(targetType: "SESSION", severity: "LOW"), Guid.NewGuid(), "create", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            created.AlertId, new AlertDecisionRequest { Decision = "PUBLISH", ExpectedVersion = 1 }, Guid.NewGuid(), "publish", "publish-1", CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("target_terminal", exception.Code);
        Assert.Equal("PROPOSED", (await db.OperationalAlerts.SingleAsync()).Lifecycle);
        Assert.Empty(await db.AlertDecisions.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-016 high impact alert drafter cannot publish their own alert")]
    [Trait("TestId", "COASTAL-ALERT-016")]
    public async Task HighImpactAlertRequiresIndependentPublisher()
    {
        await using var db = CreateDb();
        db.TargetOperationalStates.Add(TargetState("OPEN"));
        await db.SaveChangesAsync();
        var drafter = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(severity: "CRITICAL"), drafter, "create", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            created.AlertId, new AlertDecisionRequest { Decision = "PUBLISH", ExpectedVersion = 1 }, drafter, "publish", "publish-1", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("independent_reviewer_required", exception.Code);
        Assert.Equal("PROPOSED", (await db.OperationalAlerts.SingleAsync()).Lifecycle);
        Assert.Empty(await db.AlertDecisions.ToListAsync());
        Assert.Single(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-017 high impact alert linked to an assessment requires an independent assessment reviewer")]
    [Trait("TestId", "COASTAL-ALERT-017")]
    public async Task HighImpactLinkedAlertBlocksAssessmentInitiator()
    {
        await using var db = CreateDb();
        db.TargetOperationalStates.Add(TargetState("OPEN"));
        var assessmentId = Guid.NewGuid();
        var initiator = Guid.NewGuid();
        db.Assessments.Add(Assessment(assessmentId, TargetId, initiator));
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(severity: "HIGH", assessmentId: assessmentId), Guid.NewGuid(), "create", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            created.AlertId, new AlertDecisionRequest { Decision = "PUBLISH", ExpectedVersion = 1 }, initiator, "publish", "publish-1", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("independent_reviewer_required", exception.Code);
        Assert.Equal("PROPOSED", (await db.OperationalAlerts.SingleAsync()).Lifecycle);
        Assert.Empty(await db.AlertDecisions.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-018 only active alerts can be resolved")]
    [Trait("TestId", "COASTAL-ALERT-018")]
    public async Task ResolveRequiresActiveLifecycleAndAdvancesVersion()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        var proposed = await service.CreateAsync(CreateRequest(severity: "LOW"), actor, "create", CancellationToken.None);
        var notActive = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            proposed.AlertId, new AlertDecisionRequest { Decision = "RESOLVE", ExpectedVersion = 1 }, actor, "resolve", "resolve-proposed", CancellationToken.None));

        db.OperationalAlerts.Add(Alert(Guid.NewGuid(), "PUBLIC", "ACTIVE", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), TargetId, actor));
        await db.SaveChangesAsync();
        var active = await db.OperationalAlerts.SingleAsync(item => item.Lifecycle == "ACTIVE");
        var resolved = await service.DecideAsync(active.Id, new AlertDecisionRequest { Decision = "RESOLVE", ExpectedVersion = 1 }, actor, "resolve", "resolve-active", CancellationToken.None);

        Assert.Equal("alert_not_resolvable", notActive.Code);
        Assert.Equal(StatusCodes.Status409Conflict, notActive.StatusCode);
        Assert.Equal("RESOLVED", resolved.Body.Lifecycle);
        Assert.Equal(2, resolved.Body.Version);
        Assert.Equal("RESOLVED", (await db.OperationalAlerts.SingleAsync(item => item.Id == active.Id)).Lifecycle);
        Assert.Single(await db.AlertDecisions.ToListAsync());
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-019 expired drafts and stale decisions do not mutate lifecycle")]
    [Trait("TestId", "COASTAL-ALERT-019")]
    public async Task PublishRejectsExpiredAndStaleDrafts()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        db.TargetOperationalStates.Add(TargetState("OPEN"));
        db.OperationalAlerts.Add(Alert(Guid.NewGuid(), "PUBLIC", "PROPOSED", DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1), TargetId, Guid.NewGuid()));
        db.OperationalAlerts.Add(Alert(Guid.NewGuid(), "PUBLIC", "PROPOSED", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), TargetId, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var alerts = await db.OperationalAlerts.OrderBy(x => x.ValidUntil).ToListAsync();

        var expired = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            alerts[0].Id, new AlertDecisionRequest { Decision = "PUBLISH", ExpectedVersion = 1 }, Guid.NewGuid(), "publish", "expired-1", CancellationToken.None));
        var stale = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            alerts[1].Id, new AlertDecisionRequest { Decision = "PUBLISH", ExpectedVersion = 2 }, Guid.NewGuid(), "publish", "stale-1", CancellationToken.None));

        Assert.Equal("alert_period_expired", expired.Code);
        Assert.Equal("alert_version_stale", stale.Code);
        Assert.All(await db.OperationalAlerts.ToListAsync(), alert => Assert.Equal("PROPOSED", alert.Lifecycle));
        Assert.Empty(await db.AlertDecisions.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-020 active alerts past their effective end are presented as expired")]
    [Trait("TestId", "COASTAL-ALERT-020")]
    public async Task ExpiredActiveAlertIsNotPresentedAsCurrent()
    {
        await using var db = CreateDb();
        db.OperationalAlerts.Add(Alert(Guid.NewGuid(), "PUBLIC", "ACTIVE", DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1), TargetId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetQueueAsync(new AlertListQuery(), Guid.NewGuid(), canManage: true, CancellationToken.None);

        Assert.Equal("EXPIRED", Assert.Single(result.Items).Lifecycle);
    }

    private static CoastalOperationsDbContext CreateDb() => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-alert-tests-{Guid.NewGuid():N}")
            .Options);

    private static AlertApplicationService CreateService(CoastalOperationsDbContext db) => new(db, new IdempotencyStore(db));

    private static CreateAlertRequest CreateRequest(
        string title = "High surf advisory",
        string description = "Monitor shoreline conditions.",
        string severity = "MODERATE",
        string visibility = "PUBLIC",
        string targetType = "ACTIVITY",
        Guid? targetId = null,
        Guid? assessmentId = null,
        string? startsAt = null,
        string? endsAt = null)
    {
        var starts = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new CreateAlertRequest
        {
            TargetType = targetType,
            TargetId = targetId ?? TargetId,
            AssessmentId = assessmentId,
            Title = title,
            Description = description,
            Severity = severity,
            Visibility = visibility,
            ValidFrom = startsAt ?? starts.ToString("O"),
            ValidUntil = endsAt ?? starts.AddHours(2).ToString("O")
        };
    }

    private static UpdateAlertRequest UpdateRequest(int expectedVersion = 1, string? visibility = "OPERATIONS")
    {
        var starts = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new UpdateAlertRequest
        {
            ExpectedVersion = expectedVersion,
            Title = " Updated alert ",
            Description = " Content updated ",
            Severity = "critical",
            Visibility = visibility,
            ValidFrom = starts.ToString("O"),
            ValidUntil = starts.AddHours(3).ToString("O")
        };
    }

    private static Assessment Assessment(Guid id, Guid targetId, Guid initiatedBy) => new()
    {
        Id = id, WorkflowId = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = targetId,
        PeriodStartsAt = DateTimeOffset.UtcNow, PeriodEndsAt = DateTimeOffset.UtcNow.AddHours(1),
        Objective = "Synthetic assessment", InitiatedBy = initiatedBy, Version = 1,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
    };

    private static TargetOperationalState TargetState(string state, string targetType = "ACTIVITY") => new()
    {
        Id = Guid.NewGuid(), TargetType = targetType, TargetId = TargetId,
        State = state, Version = 1, UpdatedBy = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow
    };

    private static OperationalAlert Alert(
        Guid id,
        string visibility,
        string lifecycle,
        DateTimeOffset starts,
        DateTimeOffset ends,
        Guid targetId,
        Guid createdBy) => new()
    {
        Id = id, TargetType = "ACTIVITY", TargetId = targetId, Title = "Test alert",
        Description = "Synthetic test alert", Severity = "LOW", Visibility = visibility,
        Lifecycle = lifecycle, ValidFrom = starts, ValidUntil = ends,
        CreatedBy = createdBy, UpdatedBy = createdBy, Version = 1,
        CreatedAt = starts, UpdatedAt = starts
    };
}
