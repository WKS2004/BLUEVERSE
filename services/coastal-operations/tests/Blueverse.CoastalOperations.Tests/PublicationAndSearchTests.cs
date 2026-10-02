using System.Security.Claims;
using System.Text.Json;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Blueverse.CoastalOperations.Security;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class PublicationAndSearchTests
{
    [Fact(DisplayName = "COASTAL-PUBLICATION-001 publication atomically captures full context and replay retains one envelope")]
    [Trait("TestId", "COASTAL-PUBLICATION-001")]
    public async Task PublicationCapturesImmutableContextAndReplay()
    {
        await using var db = Db();
        var actor = Guid.NewGuid();
        var service = Service(db);
        var source = Guid.NewGuid();
        var created = await service.CreateAsync(Request(source), actor, "create", "draft", CancellationToken.None);
        Assert.Empty(await db.AssessmentDispatches.ToListAsync());
        var evidence = new AssessmentEvidence { Id = Guid.CreateVersion7(), AssessmentId = created.Body.AssessmentId,
            AssessmentVersion = 1, UploadedBy = actor, MediaType = "image/png", ByteLength = 20, ContentSha256 = new string('a', 64),
            UploadedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(2) };
        db.AssessmentEvidence.Add(evidence);
        db.AssessmentEvidence.Add(new AssessmentEvidence { Id = Guid.CreateVersion7(), AssessmentId = created.Body.AssessmentId,
            AssessmentVersion = 1, InspectionStatus = "EXPIRED", ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) });
        db.AssessmentEvidence.Add(new AssessmentEvidence { Id = Guid.CreateVersion7(), AssessmentId = created.Body.AssessmentId,
            AssessmentVersion = 1, InspectionStatus = "REMOVED", RemovedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(2) });
        await db.SaveChangesAsync();
        var request = new SubmitAssessmentDraftRequest { ExpectedVersion = 1 };
        var first = await service.SubmitDraftAsync(created.Body.AssessmentId, request, actor, "publication", "publish", CancellationToken.None);
        var replay = await service.SubmitDraftAsync(created.Body.AssessmentId, request, actor, "ignored", "publish", CancellationToken.None);
        var envelope = await db.AssessmentDispatches.SingleAsync();
        var payload = JsonSerializer.Deserialize<PublishedAssessmentDispatch>(envelope.PayloadJson, OperationsValidation.JsonOptions)!;
        Assert.True(replay.Replayed); Assert.Equal(first.SerializedBody, replay.SerializedBody);
        Assert.Equal("SUBMITTED", first.Body.WorkflowStatus); Assert.Equal(2, payload.PublishedVersion);
        Assert.Equal(envelope.Id, payload.DispatchId); Assert.Equal(first.Body.WorkflowId, payload.WorkflowId);
        Assert.Equal(created.Body.AssessmentId, payload.AssessmentId); Assert.Equal(source, payload.SourceWorkflowId);
        Assert.Equal("ACTIVITY", payload.TargetType); Assert.Equal(created.Body.TargetId, payload.TargetId);
        Assert.Equal(created.Body.PeriodStartsAt, payload.PeriodStartsAt); Assert.Equal(created.Body.PeriodEndsAt, payload.PeriodEndsAt);
        Assert.Equal("Inspect coastal access", payload.Objective); Assert.Equal(actor, payload.ActorId);
        Assert.Equal("publication", payload.CorrelationId); Assert.NotEqual(default, payload.PublishedAt);
        Assert.Equal("NOT_CONNECTED", envelope.Status); Assert.Equal(0, envelope.Attempts);
        Assert.Single(payload.ComponentDependencies); Assert.Equal("UNAVAILABLE", payload.ComponentDependencies[0].Status);
        Assert.Equal(evidence.Id, Assert.Single(payload.Evidence).EvidenceId);
        Assert.DoesNotContain("storage", envelope.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await db.OperationsAudit.CountAsync()); Assert.Equal(2, await db.IdempotencyRecords.CountAsync());
        Assert.Empty(await db.AssessmentProposals.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-PUBLICATION-002 disconnected delivery never dispatches or manufactures AI progress")]
    [Trait("TestId", "COASTAL-PUBLICATION-002")]
    public async Task DisconnectedDeliveryHasNoWrites()
    {
        await using var db = Db(); var envelope = await Publish(db);
        var port = new Port { Availability = AssessmentAiAvailability.NotConnected };
        Assert.False(await new AssessmentDispatchDelivery(db, port).DeliverNextAsync(CancellationToken.None));
        Assert.Equal(0, port.Calls); Assert.Equal(0, (await db.AssessmentDispatches.SingleAsync()).Attempts);
        Assert.Equal("NOT_CONNECTED", envelope.Status); Assert.Equal(2, await db.OperationsAudit.CountAsync());
        Assert.Equal("SUBMITTED", (await db.Assessments.SingleAsync()).WorkflowStatus);
    }

    [Fact(DisplayName = "COASTAL-PUBLICATION-003 accepted delivery uses full payload and preserves human approval boundary")]
    [Trait("TestId", "COASTAL-PUBLICATION-003")]
    public async Task AcceptedDeliveryDoesNotCreateAProposalOrExecute()
    {
        await using var db = Db(); var envelope = await Publish(db); var port = new Port();
        var delivery = new AssessmentDispatchDelivery(db, port);
        Assert.True(await delivery.DeliverNextAsync(CancellationToken.None));
        Assert.False(await delivery.DeliverNextAsync(CancellationToken.None));
        var saved = await db.AssessmentDispatches.SingleAsync(); var assessment = await db.Assessments.SingleAsync();
        Assert.Equal(envelope.Id, port.Payload!.DispatchId); Assert.Equal(1, port.Calls);
        Assert.Equal("ACCEPTED", saved.Status); Assert.Equal(1, saved.Attempts); Assert.Null(saved.LeaseId); Assert.Null(saved.LeaseUntil);
        Assert.Equal("SUBMITTED", assessment.WorkflowStatus); Assert.Equal("SUCCEEDED", assessment.AiDispatchOutcome);
        Assert.False(assessment.AiDispatchRetryable); Assert.Empty(await db.AssessmentProposals.ToListAsync());
        Assert.Empty(await db.ReviewerDecisions.ToListAsync()); Assert.Empty(await db.TargetOperationalStates.ToListAsync());
        Assert.Equal(new[] { "CREATED", "SUBMITTED", "AI_DISPATCH_ATTEMPT", "AI_DISPATCH_ACCEPTED" }, await db.OperationsAudit.OrderBy(x => x.CreatedAt).Select(x => x.Action).ToArrayAsync());
    }

    [Theory(DisplayName = "COASTAL-PUBLICATION-004 unavailable delivery is bounded and eventually fails safely")]
    [Trait("TestId", "COASTAL-PUBLICATION-004")]
    [InlineData(false)] [InlineData(true)]
    public async Task BoundedRetries(bool throwsNetworkFailure)
    {
        await using var db = Db(); await Publish(db);
        var port = new Port { Result = new("UNAVAILABLE", true), ThrowsNetworkFailure = throwsNetworkFailure };
        var delivery = new AssessmentDispatchDelivery(db, port);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.True(await delivery.DeliverNextAsync(CancellationToken.None));
            var saved = await db.AssessmentDispatches.SingleAsync();
            Assert.Equal(attempt, saved.Attempts); Assert.Null(saved.LeaseId);
            Assert.Equal(attempt == 3 ? "SAFE_FAILURE" : "UNAVAILABLE", saved.Status);
            saved.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        }
        Assert.False(await delivery.DeliverNextAsync(CancellationToken.None)); Assert.Equal(3, port.Calls);
        var assessment = await db.Assessments.SingleAsync(); Assert.Equal("SAFE_FAILURE", assessment.WorkflowStatus);
        Assert.False(assessment.AiDispatchRetryable); Assert.Empty(await db.TargetOperationalStates.ToListAsync());
        Assert.Equal(8, await db.OperationsAudit.CountAsync());
    }

    [Theory(DisplayName = "COASTAL-PUBLICATION-005 malformed dispatch payload and response fail closed")]
    [Trait("TestId", "COASTAL-PUBLICATION-005")]
    [InlineData("{}", 0)] [InlineData("invalid", 0)] [InlineData(null, 1)]
    public async Task MalformedDelivery(string? payload, int expectedCalls)
    {
        await using var db = Db(); var envelope = await Publish(db);
        if (payload is not null) { envelope.PayloadJson = payload; await db.SaveChangesAsync(); }
        var port = new Port { Result = new("UNRECOGNIZED", false) };
        Assert.True(await new AssessmentDispatchDelivery(db, port).DeliverNextAsync(CancellationToken.None));
        Assert.Equal(expectedCalls, port.Calls); Assert.Equal("SAFE_FAILURE", (await db.AssessmentDispatches.SingleAsync()).Status);
        Assert.Equal("INVALID_RESULT", (await db.Assessments.SingleAsync()).AiDispatchOutcome);
        Assert.Empty(await db.TargetOperationalStates.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-PUBLICATION-006 lease recovery respects final attempt and cancellation")]
    [Trait("TestId", "COASTAL-PUBLICATION-006")]
    public async Task LeaseRecoveryAndCancellation()
    {
        await using var db = Db(); var envelope = await Publish(db);
        envelope.Status = "LEASED"; envelope.LeaseId = Guid.NewGuid(); envelope.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(1);
        await db.SaveChangesAsync(); var port = new Port(); var delivery = new AssessmentDispatchDelivery(db, port);
        Assert.False(await delivery.DeliverNextAsync(CancellationToken.None)); Assert.Equal(0, port.Calls);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery.DeliverNextAsync(cancelled.Token));
        envelope.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(-1); envelope.Attempts = 3; await db.SaveChangesAsync();
        Assert.True(await delivery.DeliverNextAsync(CancellationToken.None)); Assert.Equal(0, port.Calls);
        Assert.Equal("SAFE_FAILURE", (await db.AssessmentDispatches.SingleAsync()).Status);
    }

    [Fact(DisplayName = "COASTAL-SEARCH-001 assessment search scopes ownership before filtering and pagination")]
    [Trait("TestId", "COASTAL-SEARCH-001")]
    public async Task AssessmentSearchAndScope()
    {
        await using var db = Db(); var actor = Guid.NewGuid(); var service = Service(db);
        for (var index = 0; index < 3; index++) await service.CreateAsync(Request(), actor, "create", $"draft-{index}", CancellationToken.None);
        var other = await service.CreateAsync(Request(), Guid.NewGuid(), "other", "other", CancellationToken.None);
        var first = await service.GetQueueAsync(new() { Search = "COASTAL", PageSize = 1, OnlyMine = true, WorkflowStatus = "DRAFT" }, actor, true, CancellationToken.None);
        var next = await service.GetQueueAsync(new() { Search = "coastal", PageSize = 1, OnlyMine = true, WorkflowStatus = "DRAFT", Cursor = first.NextCursor }, actor, true, CancellationToken.None);
        Assert.Single(first.Items); Assert.Single(next.Items); Assert.NotNull(first.NextCursor);
        Assert.NotEqual(first.Items[0].AssessmentId, next.Items[0].AssessmentId);
        Assert.DoesNotContain(first.Items.Concat(next.Items), x => x.AssessmentId == other.Body.AssessmentId);
        Assert.Empty((await service.GetQueueAsync(new() { PublishedOnly = true }, actor, true, CancellationToken.None)).Items);
        var denied = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetDetailAsync(other.Body.AssessmentId, actor, true, false, CancellationToken.None));
        Assert.Equal(404, denied.StatusCode);
        Assert.Empty((await service.GetQueueAsync(new() { TargetType = "SESSION" }, actor, false, CancellationToken.None)).Items);
    }

    [Theory(DisplayName = "COASTAL-SEARCH-002 invalid search and pagination are rejected without writes")]
    [Trait("TestId", "COASTAL-SEARCH-002")]
    [InlineData(0, 0)] [InlineData(101, 0)] [InlineData(25, 161)]
    public async Task InvalidSearch(int pageSize, int searchLength)
    {
        await using var db = Db(); var error = await Assert.ThrowsAsync<CoastalOperationsException>(() => Service(db).GetQueueAsync(new() { PageSize = pageSize, Search = new string('x', searchLength) }, Guid.NewGuid(), false, CancellationToken.None));
        Assert.Equal(422, error.StatusCode); Assert.Empty(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-SEARCH-003 alert filtering cannot reveal drafts or internal history to public readers")]
    [Trait("TestId", "COASTAL-SEARCH-003")]
    public async Task AlertSearchRespectsVisibilityAndHistory()
    {
        await using var db = Db(); var now = DateTimeOffset.UtcNow;
        foreach (var (state, visibility, ends) in new[] { ("PROPOSED", "PUBLIC", now.AddHours(1)), ("ACTIVE", "PUBLIC", now.AddHours(1)), ("ACTIVE", "OPERATIONS", now.AddHours(1)), ("ACTIVE", "PUBLIC", now.AddMinutes(-1)), ("WITHDRAWN", "PUBLIC", now.AddHours(1)) })
            db.OperationalAlerts.Add(new OperationalAlert { Id = Guid.CreateVersion7(), TargetId = Guid.NewGuid(), TargetType = "ACTIVITY", Title = "Surf notice", Description = "Coastal conditions", Lifecycle = state, Visibility = visibility, Severity = "MODERATE", ValidFrom = now.AddHours(-2), ValidUntil = ends, CreatedBy = Guid.NewGuid(), CreatedAt = now });
        await db.SaveChangesAsync(); var service = new AlertApplicationService(db, new IdempotencyStore(db));
        var visible = await service.GetQueueAsync(new() { Search = "SURF", Severity = "MODERATE" }, Guid.NewGuid(), false, CancellationToken.None);
        var only = Assert.Single(visible.Items); Assert.Equal("ACTIVE", only.Lifecycle); Assert.Equal("PUBLIC", only.Visibility);
        Assert.Empty((await service.GetQueueAsync(new() { Lifecycle = "PROPOSED" }, Guid.NewGuid(), false, CancellationToken.None)).Items);
        var error = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetQueueAsync(new() { History = true }, Guid.NewGuid(), false, CancellationToken.None)); Assert.Equal(403, error.StatusCode);
        var history = await service.GetQueueAsync(new() { History = true }, Guid.NewGuid(), true, CancellationToken.None);
        Assert.Equal(2, history.Items.Count); Assert.Contains(history.Items, x => x.Lifecycle == "EXPIRED"); Assert.Contains(history.Items, x => x.Lifecycle == "WITHDRAWN");
        Assert.Single((await service.GetQueueAsync(new() { Visibility = "OPERATIONS", Lifecycle = "ACTIVE" }, Guid.NewGuid(), true, CancellationToken.None)).Items);
    }

    [Fact(DisplayName = "COASTAL-AUDIT-001 activity scope protects drafts and paginates without leaking other records")]
    [Trait("TestId", "COASTAL-AUDIT-001")]
    public async Task AuditScopeAndPagination()
    {
        await using var db = Db(); var envelope = await Publish(db); var reader = new OperationsAuditReader(db);
        var page = await reader.GetAssessmentAsync(envelope.AssessmentId, envelope.ActorId, false, new() { PageSize = 1 }, CancellationToken.None);
        Assert.Equal("SUBMITTED", Assert.Single(page.Items).Action); Assert.NotNull(page.NextCursor);
        var older = await reader.GetAssessmentAsync(envelope.AssessmentId, envelope.ActorId, false, new() { PageSize = 1, Cursor = page.NextCursor }, CancellationToken.None);
        Assert.Equal("CREATED", Assert.Single(older.Items).Action); Assert.Null(older.NextCursor);
        var denied = await Assert.ThrowsAsync<CoastalOperationsException>(() => reader.GetAssessmentAsync(envelope.AssessmentId, Guid.NewGuid(), false, new(), CancellationToken.None)); Assert.Equal(404, denied.StatusCode);
        var draft = await Service(db).CreateAsync(Request(), Guid.NewGuid(), "draft", "draft-other", CancellationToken.None);
        denied = await Assert.ThrowsAsync<CoastalOperationsException>(() => reader.GetAssessmentAsync(draft.Body.AssessmentId, envelope.ActorId, true, new(), CancellationToken.None)); Assert.Equal(404, denied.StatusCode);
        var invalid = await Assert.ThrowsAsync<CoastalOperationsException>(() => reader.GetAssessmentAsync(envelope.AssessmentId, envelope.ActorId, false, new() { Cursor = "broken" }, CancellationToken.None)); Assert.Equal(422, invalid.StatusCode);
    }

    [Theory(DisplayName = "COASTAL-PERMISSION-001 publication and resolution grants cannot authorize each other")]
    [Trait("TestId", "COASTAL-PERMISSION-001")]
    [InlineData("operations.alert.publish", "PUBLISH", true)] [InlineData("operations.alert.publish", "RESOLVE", false)]
    [InlineData("operations.alert.resolve", "PUBLISH", false)] [InlineData("operations.alert.resolve", "RESOLVE", true)]
    [InlineData("operations.alert.manage", "PUBLISH", false)] [InlineData("operations.alert.decide", "PUBLISH", true)]
    [InlineData("operations.alert.read", "PUBLISH", false)] [InlineData("operations.alert.decide", "UNKNOWN", false)]
    public void DecisionGrantsAreDistinct(string grant, string decision, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", grant)], "test"));
        Assert.Equal(expected, CoastalAlertAccess.CanDecide(user, decision));
    }

    private static CoastalOperationsDbContext Db() => new(new DbContextOptionsBuilder<CoastalOperationsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [Theory(DisplayName = "COASTAL-PUBLICATION-007 uncooperative availability and dispatch calls have bounded timeouts")]
    [Trait("TestId", "COASTAL-PUBLICATION-007")]
    [InlineData(true)] [InlineData(false)]
    public async Task UncooperativePortTimeout(bool availability)
    {
        await using var db = Db(); await Publish(db);
        var port = new Port { AvailabilityNeverCompletes = availability, DispatchNeverCompletes = !availability };
        Assert.True(await new AssessmentDispatchDelivery(db, port).DeliverNextAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15)));
        var envelope = await db.AssessmentDispatches.SingleAsync();
        Assert.Equal("UNAVAILABLE", envelope.Status); Assert.Equal(1, envelope.Attempts); Assert.Null(envelope.LeaseId);
        Assert.Equal(availability ? 0 : 1, port.Calls); Assert.True((await db.Assessments.SingleAsync()).AiDispatchRetryable);
        Assert.Empty(await db.AssessmentProposals.ToListAsync());
    }
    private static AssessmentApplicationService Service(CoastalOperationsDbContext db) => new(db, new IdempotencyStore(db), new Collector());
    private static CreateAssessmentRequest Request(Guid? source = null) => new() { TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), SourceWorkflowId = source, PeriodStartsAt = "2026-10-01T08:00:00Z", PeriodEndsAt = "2026-10-01T10:00:00Z", Objective = "Inspect coastal access" };
    private static async Task<AssessmentDispatch> Publish(CoastalOperationsDbContext db)
    {
        var service = Service(db); var actor = Guid.NewGuid();
        var draft = await service.CreateAsync(Request(), actor, "create", "draft", CancellationToken.None);
        await service.SubmitDraftAsync(draft.Body.AssessmentId, new() { ExpectedVersion = 1 }, actor, "publication", "publication", CancellationToken.None);
        return await db.AssessmentDispatches.SingleAsync();
    }
    private sealed class Collector : IComponentDependencyCollector
    {
        public Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(string targetType, Guid targetId, DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? sourceId, CancellationToken token) => Task.FromResult<IReadOnlyList<ComponentDependencyResult>>([new("marine-safety", "suitability", "UNAVAILABLE", 1, 0, true, "UNAVAILABLE", "Unavailable", DateTimeOffset.UtcNow, null)]);
    }
    private sealed class Port : IAssessmentProposalPort
    {
        public AssessmentAiAvailability Availability { get; init; } = AssessmentAiAvailability.Available;
        public ProposalDispatchOutcome Result { get; init; } = new("ACCEPTED", false);
        public bool ThrowsNetworkFailure { get; init; }
        public bool AvailabilityNeverCompletes { get; init; }
        public bool DispatchNeverCompletes { get; init; }
        public int Calls { get; private set; }
        public PublishedAssessmentDispatch? Payload { get; private set; }
        public Task<AssessmentAiAvailability> GetAvailabilityAsync(CancellationToken token) => AvailabilityNeverCompletes ? new TaskCompletionSource<AssessmentAiAvailability>().Task : Task.FromResult(Availability);
        public Task<ProposalDispatchOutcome> DispatchAsync(Guid workflowId, Guid assessmentId, CancellationToken token) => throw new InvalidOperationException("ID-only dispatch must never be used.");
        public Task<ProposalDispatchOutcome> DispatchAsync(PublishedAssessmentDispatch request, CancellationToken token)
        {
            Calls++; Payload = request;
            if (ThrowsNetworkFailure) throw new HttpRequestException("fixture failure");
            if (DispatchNeverCompletes) return new TaskCompletionSource<ProposalDispatchOutcome>().Task;
            return Task.FromResult(Result);
        }
    }
}
