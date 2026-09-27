using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class AssessmentApplicationServiceTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly DateTimeOffset PeriodStartsAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEndsAt = PeriodStartsAt.AddHours(2);

    [Fact(DisplayName = "COASTAL-ASSESSMENT-001 create persists a pending workflow and exact idempotent replay")]
    public async Task COASTAL_ASSESSMENT_001_CreatePersistsAndReplaysOnce()
    {
        await using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var service = CreateService(db);
        var request = ValidRequest();

        var first = await service.CreateAsync(request, actorId, "correlation-one", "assessment-one", CancellationToken.None);
        var replay = await service.CreateAsync(request, actorId, "correlation-one", "assessment-one", CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, first.StatusCode);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Body.AssessmentId, replay.Body.AssessmentId);
        Assert.NotEqual(Guid.Empty, first.Body.WorkflowId);
        Assert.Equal("SUBMITTED", first.Body.WorkflowStatus);
        Assert.Equal("NOT_CONNECTED", first.Body.AiDependencyStatus);
        Assert.Equal("NOT_REQUESTED", first.Body.AiDispatchOutcome);
        Assert.Equal("UNAVAILABLE", Assert.Single(first.Body.ComponentDependencies, x => x.Service == "member-1-experience").Status);
        Assert.Equal(1, await db.Assessments.CountAsync());
        Assert.Equal(1, await db.OperationsAudit.CountAsync());
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-002 reused idempotency key with changed request conflicts")]
    public async Task COASTAL_ASSESSMENT_002_ChangedRequestCannotReuseKey()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var actorId = Guid.NewGuid();

        await service.CreateAsync(ValidRequest(), actorId, "correlation-one", "assessment-one", CancellationToken.None);
        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.CreateAsync(ValidRequest(objective: "Changed objective"), actorId, "correlation-one", "assessment-one", CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("idempotency_key_reused", exception.Code);
        Assert.Equal(1, await db.Assessments.CountAsync());
        Assert.Equal(1, await db.OperationsAudit.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-003 assessment reads remain owner-scoped unless queue permission is present")]
    public async Task COASTAL_ASSESSMENT_003_ReadsRespectOwnerAndQueueScope()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var ownerId = Guid.NewGuid();
        var otherActorId = Guid.NewGuid();
        var created = await service.CreateAsync(ValidRequest(), ownerId, "correlation-one", "owner-assessment", CancellationToken.None);

        var ownerQueue = await service.GetQueueAsync(new AssessmentListQuery(), ownerId, canReadQueue: false, CancellationToken.None);
        var otherQueue = await service.GetQueueAsync(new AssessmentListQuery(), otherActorId, canReadQueue: false, CancellationToken.None);
        var authorizedQueue = await service.GetQueueAsync(new AssessmentListQuery(), otherActorId, canReadQueue: true, CancellationToken.None);
        var detail = await service.GetDetailAsync(created.Body.AssessmentId, ownerId, canReadQueue: false, canReadEvidence: false, CancellationToken.None);
        var denied = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.GetDetailAsync(created.Body.AssessmentId, otherActorId, canReadQueue: false, canReadEvidence: false, CancellationToken.None));

        Assert.Single(ownerQueue.Items);
        Assert.Empty(otherQueue.Items);
        Assert.Single(authorizedQueue.Items);
        Assert.Equal(created.Body.AssessmentId, detail.Assessment.AssessmentId);
        Assert.Empty(detail.Evidence);
        Assert.Equal(StatusCodes.Status404NotFound, denied.StatusCode);
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-004 timestamps without explicit offsets are rejected")]
    public async Task COASTAL_ASSESSMENT_004_RejectsOffsetlessPeriods()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var request = WithPeriod(ValidRequest(), "2026-10-01T08:00:00", "2026-10-01T10:00:00");

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.CreateAsync(request, Guid.NewGuid(), "correlation-one", "assessment-one", CancellationToken.None));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal("period_offset_required", exception.Code);
        Assert.Empty(await db.Assessments.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-005 decisions cannot proceed without a validated proposal")]
    public async Task COASTAL_ASSESSMENT_005_RejectsDecisionWithoutProposal()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var created = await service.CreateAsync(ValidRequest(), Guid.NewGuid(), "correlation-one", "assessment-one", CancellationToken.None);
        var decision = new ReviewerDecisionRequest
        {
            ProposalId = Guid.NewGuid(),
            ProposalVersion = 1,
            ExpectedTargetStateVersion = 1,
            Decision = "APPROVE"
        };

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.DecideAsync(created.Body.AssessmentId, decision, Guid.NewGuid(), "correlation-two", "decision-one", CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("proposal_not_available", exception.Code);
        Assert.Empty(await db.ReviewerDecisions.ToListAsync());
        Assert.Empty(await db.TargetOperationalStates.ToListAsync());
    }

    private static CoastalOperationsDbContext CreateDb() => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-operations-tests-{Guid.NewGuid():N}")
            .Options);

    private static AssessmentApplicationService CreateService(CoastalOperationsDbContext db) => new(
        db,
        new IdempotencyStore(db),
        new DisconnectedAssessmentProposalPort(),
        new UnavailableComponentDependencyCollector());

    private static CreateAssessmentRequest ValidRequest(string objective = "Assess the requested coastal activity.") => new()
    {
        TargetType = "ACTIVITY",
        TargetId = TargetId,
        PeriodStartsAt = PeriodStartsAt.ToString("O"),
        PeriodEndsAt = PeriodEndsAt.ToString("O"),
        Objective = objective
    };

    private static CreateAssessmentRequest WithPeriod(
        CreateAssessmentRequest request,
        string start,
        string end) => new()
    {
        TargetType = request.TargetType,
        TargetId = request.TargetId,
        SourceWorkflowId = request.SourceWorkflowId,
        PeriodStartsAt = start,
        PeriodEndsAt = end,
        Objective = request.Objective
    };

    private sealed class UnavailableComponentDependencyCollector : IComponentDependencyCollector
    {
        public Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(
            string targetType,
            Guid targetId,
            DateTimeOffset periodStartsAt,
            DateTimeOffset periodEndsAt,
            Guid? sourceWorkflowId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ComponentDependencyResult>>
        ([
            Result("member-1-experience", "experience-availability"),
            Result("member-2-marine-safety", "marine-suitability"),
            new ComponentDependencyResult(
                "member-3-coastal-planner", "planner-workflow", "NOT_REQUESTED", 0, 0, false,
                null, "No source workflow was provided.", DateTimeOffset.UtcNow, null)
        ]);

        private static ComponentDependencyResult Result(string service, string endpoint) => new(
            service, endpoint, "UNAVAILABLE", 3, 2, true, "SERVICE_UNAVAILABLE",
            "The endpoint did not respond after 3 attempts (2 retries).", DateTimeOffset.UtcNow, null);
    }
}
