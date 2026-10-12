using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class AssessmentWorkflowCoverageTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly DateTimeOffset StartsAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory(DisplayName = "COASTAL-ASSESSMENT-006 invalid assessment input is rejected before peer calls or persistence")]
    [Trait("TestId", "COASTAL-ASSESSMENT-006")]
    [InlineData("target-type")]
    [InlineData("target-id")]
    [InlineData("source-workflow")]
    [InlineData("objective")]
    [InlineData("date")]
    [InlineData("period-order")]
    public async Task CreateRejectsInvalidInputBeforeCallingDependencies(string invalidCase)
    {
        await using var db = CreateDb();
        var collector = new CountingCollector();
        var service = CreateService(db, collector: collector);
        var request = Request();
        request = invalidCase switch
        {
            "target-type" => Copy(targetType: "BEACH"),
            "target-id" => Copy(targetId: Guid.Empty),
            "source-workflow" => Copy(sourceWorkflowId: Guid.Empty),
            "objective" => Copy(objective: "   "),
            "date" => Copy(start: "not-a-date"),
            "period-order" => Copy(end: StartsAt.ToString("O")),
            _ => request
        };

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.CreateAsync(
            request, Guid.NewGuid(), "invalid-input", "assessment-invalid", CancellationToken.None));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal(0, collector.Calls);
        Assert.Empty(await db.Assessments.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
        Assert.Empty(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-007 actor and idempotency validation fail before external work")]
    [Trait("TestId", "COASTAL-ASSESSMENT-007")]
    public async Task CreateRequiresAuthenticatedActorAndValidIdempotencyKey()
    {
        await using var db = CreateDb();
        var collector = new CountingCollector();
        var service = CreateService(db, collector: collector);
        var anonymous = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.CreateAsync(
            Request(), Guid.Empty, "corr", "valid-key", CancellationToken.None));
        var missingKey = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.CreateAsync(
            Request(), Guid.NewGuid(), "corr", " ", CancellationToken.None));
        var malformedKey = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.CreateAsync(
            Request(), Guid.NewGuid(), "corr", "contains spaces", CancellationToken.None));

        Assert.Equal("actor_invalid", anonymous.Code);
        Assert.Equal("idempotency_key_invalid", missingKey.Code);
        Assert.Equal("idempotency_key_invalid", malformedKey.Code);
        Assert.Equal(0, collector.Calls);
        Assert.Empty(await db.Assessments.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-008 assessment creation remains local and does not start optional calls")]
    [Trait("TestId", "COASTAL-ASSESSMENT-008")]
    public async Task CreateLeavesDraftWithoutPeerOrAiCalls()
    {
        await using var db = CreateDb();
        var collector = new CountingCollector();
        var created = await CreateService(db, collector: collector).CreateAsync(
            Request(), Guid.NewGuid(), "ai-availability", "ai-availability-key", CancellationToken.None);

        Assert.Equal("DRAFT", created.Body.WorkflowStatus);
        Assert.Equal("NOT_CONNECTED", created.Body.AiDependencyStatus);
        Assert.Equal("NOT_REQUESTED", created.Body.AiDispatchOutcome);
        Assert.False(created.Body.AiDispatchRetryable);
        Assert.Equal(0, collector.Calls);
        Assert.Empty(created.Body.ComponentDependencies);
        Assert.Equal(1, await db.Assessments.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-009 submission collects peer outcomes once and replays idempotently")]
    [Trait("TestId", "COASTAL-ASSESSMENT-009")]
    public async Task SubmitCollectsPeerResultsAndReplaysWithoutRepeatingCalls()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var collector = new CountingCollector();
        var service = CreateService(db, collector: collector);
        var created = await service.CreateAsync(Request(), actor, "draft", "draft-key", CancellationToken.None);
        var request = new SubmitAssessmentDraftRequest { ExpectedVersion = created.Body.Version };

        var submitted = await service.SubmitDraftAsync(created.Body.AssessmentId, request, actor, "submit", "submit-key", CancellationToken.None);
        var replay = await service.SubmitDraftAsync(created.Body.AssessmentId, request, actor, "ignored", "submit-key", CancellationToken.None);
        var persisted = await db.Assessments.AsNoTracking().SingleAsync();

        Assert.Equal("SUBMITTED", submitted.Body.WorkflowStatus);
        Assert.Equal("NOT_CONNECTED", submitted.Body.AiDependencyStatus);
        Assert.Equal("NOT_REQUESTED", submitted.Body.AiDispatchOutcome);
        Assert.False(submitted.Body.AiDispatchRetryable);
        Assert.Equal(3, submitted.Body.ComponentDependencies.Count);
        Assert.Equal("SUBMITTED", persisted.WorkflowStatus);
        Assert.Equal("SUBMITTED", replay.Body.WorkflowStatus);
        Assert.True(replay.Replayed);
        Assert.Equal(1, collector.Calls);
        Assert.Empty(await db.AssessmentProposals.ToListAsync());
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
        Assert.Equal(1, await db.Assessments.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-016 unavailable peer outcomes do not block submission or start AI")]
    [Trait("TestId", "COASTAL-ASSESSMENT-016")]
    public async Task SubmitPersistsUnavailablePeerOutcomesWithoutAnAiProposal()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(Request(), actor, "draft", "draft-key", CancellationToken.None);
        var submitted = await service.SubmitDraftAsync(
            created.Body.AssessmentId,
            new SubmitAssessmentDraftRequest { ExpectedVersion = created.Body.Version },
            actor,
            "submit-unavailable",
            "submit-unavailable-key",
            CancellationToken.None);

        var persisted = await db.Assessments.AsNoTracking().SingleAsync();
        Assert.Equal(StatusCodes.Status200OK, submitted.StatusCode);
        Assert.Equal("SUBMITTED", submitted.Body.WorkflowStatus);
        Assert.Equal("NOT_CONNECTED", submitted.Body.AiDependencyStatus);
        Assert.Equal("NOT_REQUESTED", submitted.Body.AiDispatchOutcome);
        Assert.False(submitted.Body.AiDispatchRetryable);
        Assert.Contains(submitted.Body.ComponentDependencies, item => item.Status == "UNAVAILABLE");
        Assert.Contains("member-1-experience", persisted.ComponentDependenciesJson);
        Assert.Empty(await db.AssessmentProposals.ToListAsync());
        Assert.Equal(1, await db.OperationsAudit.CountAsync(item => item.Action == "SUBMITTED"));
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-010 assessment detail filters evidence by permission and returns ordered decisions")]
    [Trait("TestId", "COASTAL-ASSESSMENT-010")]
    public async Task DetailAppliesEvidencePermissionAndIncludesOrderedDecisions()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        var proposal = AddProposal(assessment, Guid.NewGuid());
        var older = AddDecision(assessment, proposal, Guid.NewGuid(), StartsAt.AddMinutes(1));
        var newer = AddDecision(assessment, proposal, Guid.NewGuid(), StartsAt.AddMinutes(2));
        db.AssessmentProposals.Add(proposal);
        db.ReviewerDecisions.AddRange(older, newer);
        db.AssessmentEvidence.AddRange(
            Evidence(assessment.Id, Guid.NewGuid(), "AVAILABLE", StartsAt.AddMinutes(1)),
            Evidence(assessment.Id, Guid.NewGuid(), "EXPIRED", StartsAt.AddMinutes(2)));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var restricted = await service.GetDetailAsync(assessment.Id, owner, false, false, CancellationToken.None);
        var authorized = await service.GetDetailAsync(assessment.Id, Guid.NewGuid(), true, true, CancellationToken.None);

        Assert.Equal(new[] { older.Id, newer.Id }, restricted.Decisions.Select(item => item.DecisionId));
        Assert.Empty(restricted.Evidence);
        Assert.Single(authorized.Evidence);
        Assert.Equal("AVAILABLE", authorized.Evidence[0].InspectionStatus);
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-011 queue pagination and workflow filters are stable")]
    [Trait("TestId", "COASTAL-ASSESSMENT-011")]
    public async Task QueueAppliesOwnerStatusAndCursorFilters()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var first = AddAssessment(db, owner, id: Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1"));
        var second = AddAssessment(db, owner, id: Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2"));
        var hidden = AddAssessment(db, Guid.NewGuid(), id: Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
        hidden.WorkflowStatus = "REJECTED";
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var page1 = await service.GetQueueAsync(new AssessmentListQuery { PageSize = 1 }, owner, false, CancellationToken.None);
        var page2 = await service.GetQueueAsync(new AssessmentListQuery { PageSize = 1, Cursor = page1.NextCursor }, owner, false, CancellationToken.None);
        var filtered = await service.GetQueueAsync(new AssessmentListQuery { WorkflowStatus = " rejected " }, Guid.NewGuid(), true, CancellationToken.None);

        Assert.Equal(second.Id, Assert.Single(page1.Items).AssessmentId);
        Assert.NotNull(page1.NextCursor);
        Assert.Equal(first.Id, Assert.Single(page2.Items).AssessmentId);
        Assert.Null(page2.NextCursor);
        Assert.Equal(hidden.Id, Assert.Single(filtered.Items).AssessmentId);
    }

    [Theory(DisplayName = "COASTAL-ASSESSMENT-012 invalid queue inputs are rejected")]
    [Trait("TestId", "COASTAL-ASSESSMENT-012")]
    [InlineData("actor")]
    [InlineData("cursor")]
    [InlineData("status")]
    public async Task QueueRejectsInvalidInputs(string invalidCase)
    {
        await using var db = CreateDb();
        var query = invalidCase switch
        {
            "cursor" => new AssessmentListQuery { Cursor = "%%%" },
            "status" => new AssessmentListQuery { WorkflowStatus = "UNKNOWN" },
            _ => new AssessmentListQuery()
        };

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => CreateService(db).GetQueueAsync(
            query, invalidCase == "actor" ? Guid.Empty : Guid.NewGuid(), true, CancellationToken.None));

        Assert.Equal(invalidCase == "actor" ? "actor_invalid" : invalidCase == "cursor" ? "cursor_invalid" : "workflow_status_invalid", exception.Code);
        Assert.Empty(await db.Assessments.ToListAsync());
    }

    [Theory(DisplayName = "COASTAL-ASSESSMENT-013 reviewer rejection and revision decisions advance workflow once")]
    [Trait("TestId", "COASTAL-ASSESSMENT-013")]
    [InlineData("REJECT", "REJECTED")]
    [InlineData("REQUEST_REVISION", "REVISION_REQUESTED")]
    public async Task NonExecutionDecisionIsPersistedAndIdempotent(string decision, string nextStatus)
    {
        await using var db = CreateDb();
        var assessment = AddAssessment(db, Guid.NewGuid(), "PENDING_APPROVAL");
        var proposal = AddProposal(assessment, Guid.NewGuid(), proposedState: null);
        db.AssessmentProposals.Add(proposal);
        await db.SaveChangesAsync();
        var request = DecisionRequest(proposal, decision, expectedTargetVersion: 0);
        var service = CreateService(db);

        var first = await service.DecideAsync(assessment.Id, request, Guid.NewGuid(), "decision", "decision-key", CancellationToken.None);
        var replay = await service.DecideAsync(assessment.Id, request, first.Body.ActorId, "ignored", "decision-key", CancellationToken.None);
        var savedAssessment = await db.Assessments.AsNoTracking().SingleAsync();

        Assert.Equal(StatusCodes.Status200OK, first.StatusCode);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Body.DecisionId, replay.Body.DecisionId);
        Assert.Equal(decision, first.Body.Decision);
        Assert.Equal(nextStatus, savedAssessment.WorkflowStatus);
        Assert.Equal(2, savedAssessment.Version);
        Assert.Equal(1, await db.ReviewerDecisions.CountAsync());
        Assert.Equal(1, await db.OperationsAudit.CountAsync(item => item.Action == $"DECISION_{decision}"));
    }

    [Fact(DisplayName = "COASTAL-ASSESSMENT-014 approved proposal atomically changes target and records decision history")]
    [Trait("TestId", "COASTAL-ASSESSMENT-014")]
    public async Task ApproveChangesOperationalStateAndAuditsDecision()
    {
        await using var db = CreateDb();
        var assessment = AddAssessment(db, Guid.NewGuid(), "PENDING_APPROVAL");
        var proposal = AddProposal(assessment, Guid.NewGuid(), proposedState: "CAUTION", expectedVersion: 4);
        var state = TargetState("OPEN", 4);
        db.AssessmentProposals.Add(proposal);
        db.TargetOperationalStates.Add(state);
        await db.SaveChangesAsync();

        var result = await CreateService(db).DecideAsync(
            assessment.Id, DecisionRequest(proposal, "APPROVE", 4), Guid.NewGuid(), "approve", "approve-key", CancellationToken.None);

        var savedState = await db.TargetOperationalStates.AsNoTracking().SingleAsync();
        var history = await db.OperationalHistory.SingleAsync();
        Assert.Equal("EXECUTED", result.Body.WorkflowStatus);
        Assert.Equal("CAUTION", result.Body.OperationalState);
        Assert.Equal(5, result.Body.OperationalStateVersion);
        Assert.Equal("CAUTION", savedState.State);
        Assert.Equal(5, savedState.Version);
        Assert.Equal("OPEN", history.PreviousState);
        Assert.Equal("CAUTION", history.NewState);
        Assert.Equal(proposal.Id, history.ProposalId);
        Assert.Equal(result.Body.DecisionId, history.DecisionId);
        Assert.Equal("STATE_CHANGED", (await db.OperationsAudit.SingleAsync(item => item.ResourceType == "target")).Action);
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
    }

    [Theory(DisplayName = "COASTAL-ASSESSMENT-015 non-actionable reviewer decisions have no persisted side effects")]
    [Trait("TestId", "COASTAL-ASSESSMENT-015")]
    [InlineData("missing-assessment", "assessment_not_found", 404)]
    [InlineData("missing-proposal", "proposal_not_available", 409)]
    [InlineData("wrong-proposal-version", "proposal_not_available", 409)]
    [InlineData("assessment-not-pending", "proposal_not_actionable", 409)]
    [InlineData("expired-proposal", "proposal_not_actionable", 409)]
    [InlineData("unvalidated-proposal", "proposal_not_actionable", 409)]
    [InlineData("already-decided", "proposal_already_decided", 409)]
    [InlineData("read-only-approve", "proposal_has_no_action", 409)]
    [InlineData("high-impact-self-review", "independent_reviewer_required", 403)]
    [InlineData("target-version-mismatch", "target_version_mismatch", 409)]
    [InlineData("missing-target-state", "target_state_unavailable", 503)]
    [InlineData("stale-target-state", "target_version_stale", 409)]
    [InlineData("disallowed-transition", "state_transition_not_allowed", 409)]
    public async Task NonActionableDecisionIsRejectedWithoutPersisting(string scenario, string expectedCode, int expectedStatus)
    {
        await using var db = CreateDb();
        var initiator = Guid.NewGuid();
        var assessment = AddAssessment(db, initiator, scenario == "assessment-not-pending" ? "SUBMITTED" : "PENDING_APPROVAL");
        var expectedVersion = 2;
        var proposedState = scenario == "read-only-approve" ? null : scenario switch
        {
            "high-impact-self-review" => "TEMPORARILY_SUSPENDED",
            "disallowed-transition" => "COMPLETED",
            _ => "CAUTION"
        };
        var proposal = AddProposal(
            assessment,
            Guid.NewGuid(),
            proposedState: proposedState,
            expectedVersion: scenario == "target-version-mismatch" ? 1 : expectedVersion,
            validationStatus: scenario == "unvalidated-proposal" ? "PENDING" : "VALIDATED",
            expiresAt: scenario == "expired-proposal" ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddMinutes(5));
        db.AssessmentProposals.Add(proposal);
        if (scenario is "stale-target-state" or "disallowed-transition")
            db.TargetOperationalStates.Add(TargetState("OPEN", scenario == "stale-target-state" ? 3 : expectedVersion));
        if (scenario == "already-decided")
            db.ReviewerDecisions.Add(AddDecision(assessment, proposal, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var actor = scenario == "high-impact-self-review" ? initiator : Guid.NewGuid();
        var targetVersion = scenario == "target-version-mismatch" ? 9 : expectedVersion;
        var assessmentId = scenario == "missing-assessment" ? Guid.NewGuid() : assessment.Id;
        var proposalId = scenario == "missing-proposal" ? Guid.NewGuid() : proposal.Id;
        var proposalVersion = scenario == "wrong-proposal-version" ? proposal.ProposalVersion + 1 : proposal.ProposalVersion;
        var priorDecisions = await db.ReviewerDecisions.CountAsync();
        var priorAudit = await db.OperationsAudit.CountAsync();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            assessmentId,
            new ReviewerDecisionRequest
            {
                ProposalId = proposalId,
                ProposalVersion = proposalVersion,
                ExpectedTargetStateVersion = targetVersion,
                Decision = "APPROVE"
            },
            actor,
            "rejected-decision",
            "rejected-" + scenario,
            CancellationToken.None));

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(priorDecisions, await db.ReviewerDecisions.CountAsync());
        Assert.Equal(priorAudit, await db.OperationsAudit.CountAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
        if (scenario is "stale-target-state" or "disallowed-transition")
        {
            var savedState = await db.TargetOperationalStates.AsNoTracking().SingleAsync();
            Assert.Equal("OPEN", savedState.State);
            Assert.Equal(scenario == "stale-target-state" ? 3 : expectedVersion, savedState.Version);
        }
    }

    private static CoastalOperationsDbContext CreateDb() => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-assessment-coverage-{Guid.NewGuid():N}")
            .Options);

    private static AssessmentApplicationService CreateService(
        CoastalOperationsDbContext db,
        CountingCollector? collector = null) => new(
        db, new IdempotencyStore(db), collector ?? new CountingCollector());

    private static CreateAssessmentRequest Request() => new()
    {
        TargetType = "ACTIVITY",
        TargetId = TargetId,
        PeriodStartsAt = StartsAt.ToString("O"),
        PeriodEndsAt = StartsAt.AddHours(2).ToString("O"),
        Objective = "Review the proposed coastal activity."
    };

    private static CreateAssessmentRequest Copy(
        string? targetType = null,
        Guid? targetId = null,
        Guid? sourceWorkflowId = null,
        string? objective = null,
        string? start = null,
        string? end = null) => new()
    {
        TargetType = targetType ?? "ACTIVITY",
        TargetId = targetId ?? TargetId,
        SourceWorkflowId = sourceWorkflowId,
        PeriodStartsAt = start ?? StartsAt.ToString("O"),
        PeriodEndsAt = end ?? StartsAt.AddHours(2).ToString("O"),
        Objective = objective ?? "Review the proposed coastal activity."
    };

    private static Assessment AddAssessment(CoastalOperationsDbContext db, Guid owner, string status = "SUBMITTED", Guid? id = null)
    {
        var assessment = new Assessment
        {
            Id = id ?? Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = TargetId,
            PeriodStartsAt = StartsAt, PeriodEndsAt = StartsAt.AddHours(2), Objective = "Assessment fixture",
            WorkflowStatus = status, InitiatedBy = owner, Version = 1, CreatedAt = StartsAt, UpdatedAt = StartsAt
        };
        db.Assessments.Add(assessment);
        return assessment;
    }

    private static AssessmentProposal AddProposal(
        Assessment assessment,
        Guid id,
        string? proposedState = "CAUTION",
        int? expectedVersion = 1,
        string validationStatus = "VALIDATED",
        DateTimeOffset? expiresAt = null) => new()
    {
        Id = id, AssessmentId = assessment.Id, ProposalVersion = 1,
        ValidationStatus = validationStatus, TargetType = assessment.TargetType, TargetId = assessment.TargetId,
        ProposedState = proposedState, ExpectedTargetStateVersion = expectedVersion,
        ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5), CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
    };

    private static ReviewerDecisionRequest DecisionRequest(AssessmentProposal proposal, string decision, int expectedTargetVersion) => new()
    {
        ProposalId = proposal.Id, ProposalVersion = proposal.ProposalVersion,
        ExpectedTargetStateVersion = expectedTargetVersion, Decision = decision, Explanation = " Reviewed "
    };

    private static ReviewerDecision AddDecision(Assessment assessment, AssessmentProposal proposal, Guid actor, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(), AssessmentId = assessment.Id, ProposalId = proposal.Id,
        ProposalVersion = proposal.ProposalVersion, Decision = "REJECT", ActorId = actor,
        ExpectedTargetStateVersion = 1, WorkflowStatusAfterDecision = "REJECTED", DecidedAt = at
    };

    private static AssessmentEvidence Evidence(Guid assessmentId, Guid uploader, string status, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(), AssessmentId = assessmentId, AssessmentVersion = 1, UploadedBy = uploader,
        MediaType = "image/png", ByteLength = 8, ContentSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        InspectionStatus = status, UploadedAt = at, ExpiresAt = at.AddDays(1)
    };

    private static TargetOperationalState TargetState(string state, int version) => new()
    {
        Id = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = TargetId, State = state,
        Version = version, UpdatedBy = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class CountingCollector : IComponentDependencyCollector
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(
            string targetType, Guid targetId, DateTimeOffset periodStartsAt, DateTimeOffset periodEndsAt,
            Guid? sourceWorkflowId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            IReadOnlyList<ComponentDependencyResult> results =
            [
                new("member-1-experience", "experience-availability", "UNAVAILABLE", 3, 2, true,
                    "SERVICE_UNAVAILABLE", "Unavailable after bounded retries.", DateTimeOffset.UtcNow, null),
                new("member-2-marine-safety", "marine-suitability", "UNAVAILABLE", 3, 2, true,
                    "SERVICE_UNAVAILABLE", "Unavailable after bounded retries.", DateTimeOffset.UtcNow, null),
                new("member-3-coastal-planner", "planner-workflow", "NOT_REQUESTED", 0, 0, false,
                    null, "No workflow supplied.", DateTimeOffset.UtcNow, null)
            ];
            return Task.FromResult(results);
        }
    }
}
