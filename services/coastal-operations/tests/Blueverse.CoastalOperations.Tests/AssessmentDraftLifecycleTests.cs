using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class AssessmentDraftLifecycleTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly DateTimeOffset StartsAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "COASTAL-ASSESSMENT-017 draft updates are caller-owned, versioned and audited")]
    [Trait("TestId", "COASTAL-ASSESSMENT-017")]
    public async Task UpdateChangesOnlyDraftBusinessFields()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var collector = new CountingCollector();
        var service = CreateService(db, collector);
        var created = await service.CreateAsync(CreateRequest(), actor, "create", "create-17", CancellationToken.None);
        var sourceWorkflowId = Guid.NewGuid();

        var updated = await service.UpdateDraftAsync(created.Body.AssessmentId, new UpdateAssessmentDraftRequest
        {
            ExpectedVersion = 1,
            TargetType = "SESSION",
            TargetId = Guid.NewGuid(),
            SourceWorkflowId = sourceWorkflowId,
            PeriodStartsAt = StartsAt.AddDays(1).ToString("O"),
            PeriodEndsAt = StartsAt.AddDays(1).AddHours(3).ToString("O"),
            Objective = "  Updated objective  "
        }, actor, "update", CancellationToken.None);

        var saved = await db.Assessments.AsNoTracking().SingleAsync();
        Assert.Equal("DRAFT", updated.WorkflowStatus);
        Assert.Equal(2, updated.Version);
        Assert.Equal("SESSION", saved.TargetType);
        Assert.Equal(sourceWorkflowId, saved.SourceWorkflowId);
        Assert.Equal("Updated objective", saved.Objective);
        Assert.Equal(actor, saved.InitiatedBy);
        Assert.Null(saved.CancelledBy);
        Assert.Null(saved.CancelledAt);
        Assert.Equal(0, collector.Calls);
        Assert.Contains(await db.OperationsAudit.ToListAsync(), item => item.Action == "DRAFT_UPDATED" && item.ActorId == actor);
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-018 stale and out-of-scope draft updates have no side effects")]
    [Trait("TestId", "COASTAL-ASSESSMENT-018")]
    public async Task UpdateRejectsWrongOwnerAndStaleVersion()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), actor, "create", "create-18", CancellationToken.None);
        var request = UpdateRequest(1);

        var outOfScope = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.UpdateDraftAsync(
            created.Body.AssessmentId, request, Guid.NewGuid(), "wrong-owner", CancellationToken.None));
        var stale = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.UpdateDraftAsync(
            created.Body.AssessmentId, UpdateRequest(2), actor, "stale", CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, outOfScope.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, stale.StatusCode);
        Assert.Equal("DRAFT", (await db.Assessments.AsNoTracking().SingleAsync()).WorkflowStatus);
        Assert.Equal(1, (await db.Assessments.AsNoTracking().SingleAsync()).Version);
        Assert.Single(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-019 cancellation keeps a versioned idempotent audit tombstone")]
    [Trait("TestId", "COASTAL-ASSESSMENT-019")]
    public async Task CancelDraftIsIdempotentAndRetained()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), actor, "create", "create-19", CancellationToken.None);
        var request = new CancelAssessmentDraftRequest { ExpectedVersion = 1 };

        var first = await service.CancelDraftAsync(created.Body.AssessmentId, request, actor, "cancel", "cancel-19", CancellationToken.None);
        var replay = await service.CancelDraftAsync(created.Body.AssessmentId, request, actor, "ignored", "cancel-19", CancellationToken.None);
        var saved = await db.Assessments.AsNoTracking().SingleAsync();

        Assert.Equal(StatusCodes.Status200OK, first.StatusCode);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.SerializedBody, replay.SerializedBody);
        Assert.Equal("CANCELLED", first.Body.WorkflowStatus);
        Assert.Equal(2, first.Body.Version);
        Assert.Equal(actor, first.Body.CancelledBy);
        Assert.NotNull(first.Body.CancelledAt);
        Assert.Equal("CANCELLED", saved.WorkflowStatus);
        Assert.Equal(actor, saved.CancelledBy);
        Assert.NotNull(saved.CancelledAt);
        Assert.Single(await db.Assessments.ToListAsync());
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
        Assert.Equal(2, await db.IdempotencyRecords.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-020 cancellation rejects changed replay, stale version and non-draft state")]
    [Trait("TestId", "COASTAL-ASSESSMENT-020")]
    public async Task CancelRejectsConflictingAndClosedRequests()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), actor, "create", "create-20", CancellationToken.None);
        var request = new CancelAssessmentDraftRequest { ExpectedVersion = 1 };
        await service.CancelDraftAsync(created.Body.AssessmentId, request, actor, "cancel", "cancel-20", CancellationToken.None);

        var changedReplay = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.CancelDraftAsync(
            created.Body.AssessmentId, new CancelAssessmentDraftRequest { ExpectedVersion = 2 }, actor, "cancel", "cancel-20", CancellationToken.None));
        var newKey = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.CancelDraftAsync(
            created.Body.AssessmentId, new CancelAssessmentDraftRequest { ExpectedVersion = 2 }, actor, "cancel", "cancel-20-new", CancellationToken.None));

        Assert.Equal("idempotency_key_reused", changedReplay.Code);
        Assert.Equal(StatusCodes.Status409Conflict, newKey.StatusCode);
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
        Assert.Equal(2, await db.IdempotencyRecords.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-021 queue separates operator drafts and submitted review work")]
    [Trait("TestId", "COASTAL-ASSESSMENT-021")]
    public async Task QueueScopesDraftsSubmittedWorkAndCancelledAuditRows()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var anotherOwner = Guid.NewGuid();
        var service = CreateService(db);
        var draft = await service.CreateAsync(CreateRequest(), owner, "draft", "draft-21", CancellationToken.None);
        var cancelled = await service.CreateAsync(CreateRequest("Cancel me"), owner, "cancelled", "cancelled-21", CancellationToken.None);
        var submitted = await service.CreateAsync(CreateRequest("Submit me"), anotherOwner, "submitted", "submitted-21", CancellationToken.None);
        await service.CancelDraftAsync(cancelled.Body.AssessmentId, new CancelAssessmentDraftRequest { ExpectedVersion = 1 }, owner, "cancel", "cancel-21", CancellationToken.None);
        await service.SubmitDraftAsync(submitted.Body.AssessmentId, new SubmitAssessmentDraftRequest { ExpectedVersion = 1 }, anotherOwner, "submit", "submit-21", CancellationToken.None);

        var ownerList = await service.GetQueueAsync(new AssessmentListQuery(), owner, false, CancellationToken.None);
        var reviewQueue = await service.GetQueueAsync(new AssessmentListQuery(), Guid.NewGuid(), true, CancellationToken.None);
        var auditQueue = await service.GetQueueAsync(new AssessmentListQuery { IncludeCancelled = true }, Guid.NewGuid(), true, CancellationToken.None);
        var deniedAudit = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetQueueAsync(
            new AssessmentListQuery { IncludeCancelled = true }, owner, false, CancellationToken.None));
        var cancelledDetail = await service.GetDetailAsync(cancelled.Body.AssessmentId, owner, false, false, CancellationToken.None);

        Assert.Equal(draft.Body.AssessmentId, Assert.Single(ownerList.Items).AssessmentId);
        Assert.Equal("SUBMITTED", Assert.Single(reviewQueue.Items).WorkflowStatus);
        Assert.Equal(2, auditQueue.Items.Count);
        Assert.Contains(auditQueue.Items, item => item.WorkflowStatus == "CANCELLED");
        Assert.DoesNotContain(auditQueue.Items, item => item.WorkflowStatus == "DRAFT");
        Assert.Equal(StatusCodes.Status403Forbidden, deniedAudit.StatusCode);
        Assert.Equal("CANCELLED", cancelledDetail.Assessment.WorkflowStatus);
    }

    private static CoastalOperationsDbContext CreateDb() => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-draft-lifecycle-{Guid.NewGuid():N}")
            .Options);

    private static AssessmentApplicationService CreateService(CoastalOperationsDbContext db, CountingCollector? collector = null) =>
        new(db, new IdempotencyStore(db), collector ?? new CountingCollector());

    private static CreateAssessmentRequest CreateRequest(string objective = "Review the coastal operation.") => new()
    {
        TargetType = "ACTIVITY",
        TargetId = TargetId,
        PeriodStartsAt = StartsAt.ToString("O"),
        PeriodEndsAt = StartsAt.AddHours(2).ToString("O"),
        Objective = objective
    };

    private static UpdateAssessmentDraftRequest UpdateRequest(int version) => new()
    {
        ExpectedVersion = version,
        TargetType = "ACTIVITY",
        TargetId = TargetId,
        PeriodStartsAt = StartsAt.ToString("O"),
        PeriodEndsAt = StartsAt.AddHours(2).ToString("O"),
        Objective = "Updated objective"
    };

    private sealed class CountingCollector : IComponentDependencyCollector
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(
            string targetType,
            Guid targetId,
            DateTimeOffset periodStartsAt,
            DateTimeOffset periodEndsAt,
            Guid? sourceWorkflowId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            IReadOnlyList<ComponentDependencyResult> results =
            [
                new("member-1-experience", "experience-availability", "UNAVAILABLE", 3, 2, true,
                    "SERVICE_UNAVAILABLE", "Peer unavailable after bounded retries.", DateTimeOffset.UtcNow, null),
                new("member-2-marine-safety", "marine-suitability", "UNAVAILABLE", 3, 2, true,
                    "SERVICE_UNAVAILABLE", "Peer unavailable after bounded retries.", DateTimeOffset.UtcNow, null),
                new("member-3-coastal-planner", "planner-workflow", "NOT_REQUESTED", 0, 0, false,
                    null, "No source workflow was provided.", DateTimeOffset.UtcNow, null)
            ];
            return Task.FromResult(results);
        }
    }
}
