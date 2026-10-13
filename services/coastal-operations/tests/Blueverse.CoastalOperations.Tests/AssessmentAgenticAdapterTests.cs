using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Tests;

public sealed class AssessmentAgenticAdapterTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "COASTAL-AGENT-DISPATCH-001 internal dispatch authenticates and minimizes current assessment evidence")]
    [Trait("TestId", "COASTAL-AGENT-DISPATCH-001")]
    public async Task DispatchBuildsAuthenticatedCoordinatorContractAndProjectsFourAgentSummary()
    {
        await using var db = Db();
        var targetId = Guid.NewGuid();
        var assessment = Dispatch(targetId);
        db.TargetOperationalStates.Add(new TargetOperationalState
        {
            Id = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = targetId,
            State = "OPEN", Version = 4, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var response = CoordinatorResponse(assessment, recommendation: "PROPOSE_ACTION", action: "CAUTION");
        var handler = new RecordingHandler(assessment, response);
        var port = Port(db, handler);
        var outcome = await port.DispatchAsync(assessment, CancellationToken.None);

        Assert.Equal("ACCEPTED", outcome.Outcome);
        Assert.NotNull(outcome.Summary);
        Assert.Equal("COMPLETED", outcome.Summary.Status);
        Assert.Equal(4, outcome.Summary.Steps.Count);
        Assert.Equal(new[] { "planning_coordination", "experience_biodiversity", "marine_conditions", "safety_operations" },
            outcome.Summary.Steps.Select(step => step.AgentId));
        Assert.Equal("CAUTION", outcome.Summary.SafetyProposal!.ProposedAction);
        Assert.Equal(4, outcome.Summary.SafetyProposal.TargetStateVersion);
        Assert.Equal(2, handler.Requests.Count);
        Assert.False(handler.Requests[0].Headers.Contains("Authorization"));
        Assert.False(handler.Requests[1].Headers.Contains("Authorization"));
        Assert.Equal("synthetic-test-service-key", Assert.Single(handler.Requests[1].Headers.GetValues("X-Blueverse-Service-Key")));
        using var payload = JsonDocument.Parse(handler.DispatchBody!);
        Assert.Equal("OPERATIONAL_ASSESSMENT", payload.RootElement.GetProperty("workflowType").GetString());
        Assert.Equal(assessment.WorkflowId, payload.RootElement.GetProperty("workflowId").GetGuid());
        var agentContext = payload.RootElement.GetProperty("agentContext");
        Assert.Equal(targetId, agentContext.GetProperty("activityId").GetGuid());
        Assert.Equal(assessment.TargetId, payload.RootElement.GetProperty("assessment").GetProperty("target").GetProperty("id").GetGuid());
        var reports = payload.RootElement.GetProperty("assessment").GetProperty("reports");
        Assert.Equal(3, reports.GetArrayLength());
        Assert.Contains(reports.EnumerateArray(), report =>
            report.GetProperty("category").GetString() == "safety_profile" && report.GetProperty("status").GetString() == "UNKNOWN");
        Assert.DoesNotContain("actorId", handler.DispatchBody!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", handler.DispatchBody!, StringComparison.OrdinalIgnoreCase);
        var persistedProjection = JsonSerializer.Serialize(outcome.Summary, WebJson);
        Assert.DoesNotContain("schemaVersion", persistedProjection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chainofthought", persistedProjection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "COASTAL-AGENT-DISPATCH-002 mismatched or malformed coordinator identity fails closed")]
    [Trait("TestId", "COASTAL-AGENT-DISPATCH-002")]
    public async Task DispatchRejectsMismatchedWorkflowResponse()
    {
        await using var db = Db();
        var assessment = Dispatch(Guid.NewGuid());
        db.TargetOperationalStates.Add(new TargetOperationalState
        {
            Id = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = assessment.TargetId,
            State = "OPEN", Version = 1, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var invalid = CoordinatorResponse(assessment, recommendation: "CONTINUE", action: null,
            workflowIdOverride: Guid.NewGuid());
        var outcome = await Port(db, new RecordingHandler(assessment, invalid))
            .DispatchAsync(assessment, CancellationToken.None);

        Assert.Equal("INVALID_RESULT", outcome.Outcome);
        Assert.Null(outcome.Summary);
    }

    [Fact(DisplayName = "COASTAL-AGENT-DISPATCH-003 unavailable configuration performs no network call")]
    [Trait("TestId", "COASTAL-AGENT-DISPATCH-003")]
    public async Task DisabledAgenticRuntimeReportsNotConnected()
    {
        await using var db = Db();
        var handler = new RecordingHandler(Dispatch(Guid.NewGuid()), "{}");
        var port = new HttpAssessmentProposalPort(
            new TestClientFactory(handler),
            Options.Create(new AgenticAiOptions { Enabled = false, BaseUrl = "http://agent-planning-coordination:8080", ServiceKey = "test" }),
            Options.Create(new ComponentDependencyOptions()), db);

        Assert.Equal(AssessmentAiAvailability.NotConnected, await port.GetAvailabilityAsync(CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact(DisplayName = "COASTAL-AGENT-DISPATCH-004 validated actions create a proposal without approval or execution")]
    [Trait("TestId", "COASTAL-AGENT-DISPATCH-004")]
    public async Task DeliveryPersistsSummaryAndProposalButDoesNotApplyProposedState()
    {
        await using var db = Db();
        var published = await AddSubmittedAssessmentAsync(db);
        var summary = SafeSummary(published.Assessment, "PROPOSE_ACTION", "TEMPORARILY_SUSPENDED", published.State.Version);
        var port = new FixedPort(new ProposalDispatchOutcome("ACCEPTED", false, summary));

        Assert.True(await new AssessmentDispatchDelivery(db, port).DeliverNextAsync(CancellationToken.None));

        var saved = await db.Assessments.SingleAsync();
        var proposal = await db.AssessmentProposals.SingleAsync();
        var state = await db.TargetOperationalStates.SingleAsync();
        Assert.Equal("PENDING_APPROVAL", saved.WorkflowStatus);
        Assert.NotNull(saved.AgentExecutionSummaryJson);
        Assert.Equal("ACCEPTED", (await db.AssessmentDispatches.SingleAsync()).Status);
        Assert.Equal("VALIDATED", proposal.ValidationStatus);
        Assert.Equal("TEMPORARILY_SUSPENDED", proposal.ProposedState);
        Assert.Equal(published.State.Version, proposal.ExpectedTargetStateVersion);
        Assert.True(proposal.RequiresSeparateReviewer);
        Assert.Equal("OPEN", state.State);
        Assert.Equal(published.State.Version, state.Version);
        Assert.Empty(await db.ReviewerDecisions.ToListAsync());
        Assert.Empty(await db.OperationalHistory.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-AGENT-DISPATCH-006 read-only recommendations retain agent output without creating proposals")]
    [Trait("TestId", "COASTAL-AGENT-DISPATCH-006")]
    public async Task DeliveryPersistsReadOnlySummaryWithoutAdvancingWorkflow()
    {
        await using var db = Db();
        var published = await AddSubmittedAssessmentAsync(db);
        var summary = SafeSummary(published.Assessment, "CONTINUE_WITH_CAUTION", null, published.State.Version);

        Assert.True(await new AssessmentDispatchDelivery(db,
            new FixedPort(new ProposalDispatchOutcome("ACCEPTED", false, summary)))
            .DeliverNextAsync(CancellationToken.None));

        var assessment = await db.Assessments.SingleAsync();
        Assert.Equal("SUBMITTED", assessment.WorkflowStatus);
        Assert.NotNull(assessment.AgentExecutionSummaryJson);
        Assert.Empty(await db.AssessmentProposals.ToListAsync());
        Assert.Empty(await db.ReviewerDecisions.ToListAsync());
        Assert.Equal("OPEN", (await db.TargetOperationalStates.SingleAsync()).State);
    }

    [Fact(DisplayName = "COASTAL-AGENT-DISPATCH-005 invalid actions are rejected")]
    [Trait("TestId", "COASTAL-AGENT-DISPATCH-005")]
    public async Task DeliveryRejectsInvalidActionAndStaleTargetVersion()
    {
        await using var db = Db();
        var published = await AddSubmittedAssessmentAsync(db);
        var invalid = SafeSummary(published.Assessment, "PROPOSE_ACTION", "COMPLETED", published.State.Version);
        var port = new FixedPort(new ProposalDispatchOutcome("ACCEPTED", false, invalid));

        Assert.True(await new AssessmentDispatchDelivery(db, port).DeliverNextAsync(CancellationToken.None));

        Assert.Equal("SAFE_FAILURE", (await db.AssessmentDispatches.SingleAsync()).Status);
        Assert.Equal("INVALID_RESULT", (await db.Assessments.SingleAsync()).AiDispatchOutcome);
        Assert.Equal("SAFE_FAILURE", (await db.Assessments.SingleAsync()).WorkflowStatus);
        Assert.Null((await db.Assessments.SingleAsync()).AgentExecutionSummaryJson);
        Assert.Empty(await db.AssessmentProposals.ToListAsync());
        Assert.Equal("OPEN", (await db.TargetOperationalStates.SingleAsync()).State);
    }

    [Fact(DisplayName = "COASTAL-AGENT-DISPATCH-007 target version changes invalidate a late agent proposal")]
    [Trait("TestId", "COASTAL-AGENT-DISPATCH-007")]
    public async Task DeliveryRejectsLateProposalWhenTargetVersionChanged()
    {
        await using var db = Db();
        var published = await AddSubmittedAssessmentAsync(db);
        var stale = SafeSummary(published.Assessment, "PROPOSE_ACTION", "CAUTION", published.State.Version - 1);

        Assert.True(await new AssessmentDispatchDelivery(db,
            new FixedPort(new ProposalDispatchOutcome("ACCEPTED", false, stale)))
            .DeliverNextAsync(CancellationToken.None));

        var assessment = await db.Assessments.SingleAsync();
        Assert.Equal("INVALID_RESULT", assessment.AiDispatchOutcome);
        Assert.Equal("SAFE_FAILURE", assessment.WorkflowStatus);
        Assert.Null(assessment.AgentExecutionSummaryJson);
        Assert.Empty(await db.AssessmentProposals.ToListAsync());
        Assert.Equal("OPEN", (await db.TargetOperationalStates.SingleAsync()).State);
    }

    private static HttpAssessmentProposalPort Port(CoastalOperationsDbContext db, RecordingHandler handler) => new(
        new TestClientFactory(handler),
        Options.Create(new AgenticAiOptions
        {
            Enabled = true,
            BaseUrl = "http://agent-planning-coordination:8080",
            ServiceKey = "synthetic-test-service-key"
        }),
        Options.Create(new ComponentDependencyOptions
        {
            Member1Experience = new ComponentEndpointOptions { BaseAddress = "http://experience-biodiversity:8080" }
        }), db);

    private static CoastalOperationsDbContext Db() => new(new DbContextOptionsBuilder<CoastalOperationsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static PublishedAssessmentDispatch Dispatch(Guid targetId)
    {
        var now = DateTimeOffset.UtcNow;
        return new PublishedAssessmentDispatch(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, "ACTIVITY", targetId, null,
            now.AddDays(1), now.AddDays(2), "Review coastal activity access", Guid.NewGuid(),
            "assessment-test-correlation", now, [], []);
    }

    private static string CoordinatorResponse(
        PublishedAssessmentDispatch request,
        string recommendation,
        string? action,
        Guid? workflowIdOverride = null)
    {
        var experienceStep = "experience-context";
        var marineStep = "marine-context";
        var safetyStep = "safety-review";
        var reports = new[]
        {
            request.WorkflowId,
            request.AssessmentId,
            request.DispatchId,
            Uuid5($"blueverse:{request.WorkflowId:D}:{experienceStep}:experience_biodiversity"),
            Uuid5($"blueverse:{request.WorkflowId:D}:{marineStep}:marine_conditions")
        };
        var produced = DateTimeOffset.UtcNow;
        var steps = new[]
        {
            new { stepId = experienceStep, agentId = "experience_biodiversity", status = "SUCCEEDED", reportType = "ExperienceBiodiversityContextReport.v1", summary = "Published activity context is available.", sourceReferences = new[] { "catalogue:offering" }, uncertaintyNotes = Array.Empty<string>(), producedAtUtc = produced, errorCode = (string?)null },
            new { stepId = marineStep, agentId = "marine_conditions", status = "SUCCEEDED", reportType = "MarineConditionsReport.v1", summary = "Marine conditions were checked.", sourceReferences = new[] { "marine:forecast" }, uncertaintyNotes = Array.Empty<string>(), producedAtUtc = produced, errorCode = (string?)null },
            new { stepId = safetyStep, agentId = "safety_operations", status = "SUCCEEDED", reportType = "SafetyOperationsRecommendation.v1", summary = "Evidence and operational policy were reviewed.", sourceReferences = reports.Select(id => id.ToString("D")).ToArray(), uncertaintyNotes = Array.Empty<string>(), producedAtUtc = produced, errorCode = (string?)null }
        };
        var result = new
        {
            workflowId = workflowIdOverride ?? request.WorkflowId,
            objectiveSummary = "Coordinator validated the specialist responses.",
            agentSummaries = Array.Empty<object>(),
            safetyProposal = new
            {
                schemaVersion = "safety-operations.recommendation.v1",
                workflowId = request.WorkflowId,
                stepId = safetyStep,
                assessmentId = request.AssessmentId,
                targetId = request.TargetId,
                recommendation,
                summary = "Review the current activity status and supporting evidence.",
                proposedAction = action,
                evidenceReferences = reports.Select(id => id.ToString("D")).ToArray(),
                uncertainties = Array.Empty<string>(),
                reviewSuggestion = "REVIEW_RECOMMENDED"
            }
        };
        return JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            workflowId = request.WorkflowId,
            status = "COMPLETED",
            steps,
            result,
            errorCode = (string?)null
        }, WebJson);
    }

    private static Guid Uuid5(string name)
    {
        var namespaceBytes = Convert.FromHexString("6ba7b8119dad11d180b400c04fd430c8");
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = namespaceBytes.Concat(nameBytes).ToArray();
        var digest = SHA1.HashData(input);
        digest[6] = (byte)((digest[6] & 0x0f) | 0x50);
        digest[8] = (byte)((digest[8] & 0x3f) | 0x80);
        var hex = Convert.ToHexString(digest.AsSpan(0, 16)).ToLowerInvariant();
        return Guid.ParseExact($"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}", "D");
    }

    private static AssessmentAgentExecutionSummaryResponse SafeSummary(Assessment assessment, string recommendation, string? action, int stateVersion) =>
        new("COMPLETED", "Four agents completed the assessment.",
            [
                new("planning_coordination", "SUCCEEDED", "The work was coordinated.", [], []),
                new("experience_biodiversity", "SUCCEEDED", "Published experience context was checked.", ["experience:catalogue"], []),
                new("marine_conditions", "SUCCEEDED", "Marine evidence was checked.", ["marine:forecast"], []),
                new("safety_operations", "SUCCEEDED", "Policy and evidence were validated.", ["safety:assessment"], [])
            ],
            new(assessment.Id, assessment.TargetId, stateVersion, recommendation, action,
                "The requested operational change needs reviewer approval.",
                ["planner-report", "experience-report", "marine-report", "operations-report", "safety-profile"],
                [], "REVIEW_RECOMMENDED", DateTimeOffset.UtcNow));

    private static async Task<(Assessment Assessment, TargetOperationalState State)> AddSubmittedAssessmentAsync(CoastalOperationsDbContext db)
    {
        var actor = Guid.NewGuid();
        var target = new TargetOperationalState
        {
            Id = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), State = "OPEN",
            Version = 3, UpdatedBy = actor, UpdatedAt = DateTimeOffset.UtcNow
        };
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TargetType = target.TargetType, TargetId = target.TargetId,
            PeriodStartsAt = DateTimeOffset.UtcNow.AddDays(1), PeriodEndsAt = DateTimeOffset.UtcNow.AddDays(2),
            Objective = "Review activity conditions", WorkflowStatus = "SUBMITTED", InitiatedBy = actor,
            Version = 2, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        var request = new PublishedAssessmentDispatch(Guid.NewGuid(), assessment.WorkflowId, assessment.Id, 2,
            assessment.TargetType, assessment.TargetId, null, assessment.PeriodStartsAt, assessment.PeriodEndsAt,
            assessment.Objective, actor, "test-correlation", DateTimeOffset.UtcNow, [], []);
        var dispatch = new AssessmentDispatch
        {
            Id = request.DispatchId, AssessmentId = assessment.Id, WorkflowId = assessment.WorkflowId,
            PayloadJson = JsonSerializer.Serialize(request, OperationsValidation.JsonOptions), ActorId = actor,
            CorrelationId = request.CorrelationId, NextAttemptAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assessments.Add(assessment);
        db.AssessmentDispatches.Add(dispatch);
        db.TargetOperationalStates.Add(target);
        await db.SaveChangesAsync();
        return (assessment, target);
    }

    private sealed class TestClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(PublishedAssessmentDispatch request, string coordinatorResponse) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public string? DispatchBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
        {
            Requests.Add(message);
            if (message.RequestUri?.AbsolutePath == "/api/experiences/catalogue")
            {
                var now = DateTimeOffset.UtcNow;
                var catalogue = new[]
                {
                    new
                    {
                        destinationId = Guid.NewGuid(), destinationName = "Synthetic coast", region = "South",
                        latitude = 6.0535, longitude = 80.2209, activityId = request.TargetId,
                        activityName = "Guided reef walk", offeringId = Guid.NewGuid(), title = "Reef access",
                        availabilityStatus = "AVAILABLE", publicationState = "Published",
                        availableFrom = request.PeriodStartsAt.AddHours(-1), availableUntil = request.PeriodEndsAt.AddHours(1),
                        experienceLevels = new[] { "BEGINNER" }, checkedAt = now, timeZone = "Asia/Colombo"
                    }
                };
                return Json(catalogue);
            }
            if (message.RequestUri?.AbsolutePath.EndsWith("/dispatch", StringComparison.Ordinal) == true)
            {
                DispatchBody = await message.Content!.ReadAsStringAsync(cancellationToken);
                return Json(coordinatorResponse);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(value is string text ? text : JsonSerializer.Serialize(value, WebJson), Encoding.UTF8, "application/json")
        };
    }

    private sealed class FixedPort(ProposalDispatchOutcome result) : IAssessmentProposalPort
    {
        public Task<AssessmentAiAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
            Task.FromResult(AssessmentAiAvailability.Available);

        public Task<ProposalDispatchOutcome> DispatchAsync(Guid workflowId, Guid assessmentId, CancellationToken cancellationToken) =>
            Task.FromResult(new ProposalDispatchOutcome("INVALID_RESULT", false));

        public Task<ProposalDispatchOutcome> DispatchAsync(PublishedAssessmentDispatch request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
