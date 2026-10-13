using System.Text.Json;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Data.Entities;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class WorkflowAgentExecutionSummaryTests
{
    [Fact]
    [Trait("TestId", "PLANNER-AGENT-SUMMARY-001")]
    public async Task Workflow_status_returns_bounded_agent_summary_without_raw_state_or_source_identifiers()
    {
        await using var db = CreateDatabase();
        var workflowId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        db.PlanningWorkflows.Add(new PlanningWorkflow
        {
            WorkflowId = workflowId,
            WorkflowType = "OPERATIONAL_ASSESSMENT",
            Status = "COMPLETED",
            InitiatorUserId = ownerId,
            CreatedAtUtc = DateTime.UtcNow
        });
        db.AgentExecutionStates.Add(new AgentExecutionState
        {
            WorkflowId = workflowId,
            Status = "COMPLETED",
            CorrelationId = "internal-correlation-id",
            UpdatedAtUtc = DateTime.UtcNow,
            StateJson = JsonSerializer.Serialize(new
            {
                contractVersion = 1,
                workflowId,
                status = "COMPLETED",
                correlationId = "internal-correlation-id",
                plan = new { objectiveSummary = "Review the selected coastal activity." },
                steps = new object[]
                {
                    new
                    {
                        agentId = "experience_biodiversity",
                        status = "SUCCEEDED",
                        summary = "Relevant experience evidence was checked.",
                        sourceReferences = new[] { "private-experience-reference", "private-catalogue-reference" },
                        uncertaintyNotes = new[] { "Wildlife presence can vary by season.", "API_KEY=discard-this-model-text" },
                        errorCode = (string?)null,
                        producedAtUtc = "2026-10-13T08:00:00Z",
                        report = new { rawProviderText = "discard-this-raw-provider-output", credentials = "discard-this-secret" }
                    },
                    new
                    {
                        stepId = "safety",
                        agentId = "safety_operations",
                        status = "SUCCEEDED",
                        summary = "A reviewer should confirm the proposed operational change.",
                        sourceReferences = new[] { "private-safety-reference" },
                        uncertaintyNotes = Array.Empty<string>(),
                        errorCode = (string?)null,
                        producedAtUtc = "2026-10-13T08:01:00Z",
                        report = new { hiddenModelPayload = "discard-this-report" }
                    }
                },
                result = new
                {
                    objectiveSummary = "Review the selected coastal activity.",
                    safetyProposal = new
                    {
                        contractVersion = 1,
                        workflowId,
                        stepId = "safety",
                        agentId = "safety_operations",
                        reportType = "SafetyOperationsRecommendation.v1",
                        status = "SUCCEEDED",
                        report = new
                        {
                            schemaVersion = "safety-operations.recommendation.v1",
                            workflowId,
                            stepId = "safety",
                            assessmentId = Guid.NewGuid(),
                            targetId = Guid.NewGuid(),
                            targetStateVersion = 7,
                            recommendation = "PROPOSE_ACTION",
                            proposedAction = "CLOSE_ACCESS",
                            summary = "Restrict access until conditions are reviewed.",
                            evidenceReferences = new[] { "private-safety-evidence" },
                            uncertainties = new[] { "The conditions may change before review." },
                            reviewSuggestion = "REVIEW_RECOMMENDED"
                        },
                        sourceReferences = new[] { "private-safety-evidence" },
                        uncertaintyNotes = new[] { "The conditions may change before review." },
                        producedAtUtc = "2026-10-13T08:01:00Z"
                    }
                },
                errorCode = (string?)null,
                updatedAtUtc = "2026-10-13T08:01:00Z"
            })
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetWorkflowStatusAsync(workflowId, ownerId);

        Assert.NotNull(status);
        Assert.Equal("COMPLETED", status.AiExecutionStatus);
        var summary = Assert.IsType<Blueverse.CoastalPlanner.Models.Dtos.PlannerAgentExecutionSummaryDto>(status.AiExecutionSummary);
        Assert.Equal("COMPLETED", summary.Status);
        Assert.Equal("Review the selected coastal activity.", summary.ObjectiveSummary);
        Assert.Equal(2, summary.Steps.Count);

        var experience = summary.Steps[0];
        Assert.Equal("experience_biodiversity", experience.AgentId);
        Assert.Equal("SUCCEEDED", experience.Status);
        Assert.Equal("Relevant experience evidence was checked.", experience.Summary);
        Assert.Equal(2, experience.SourceCount);
        Assert.Equal(new[] { "evidence-1", "evidence-2" }, experience.SourceReferences);
        Assert.Equal(new[] { "Wildlife presence can vary by season." }, experience.UncertaintyNotes);
        Assert.Equal(experience.UncertaintyNotes, experience.Uncertainties);

        var proposal = Assert.IsType<Blueverse.CoastalPlanner.Models.Dtos.PlannerSafetyProposalDto>(summary.SafetyProposal);
        Assert.Equal(7, proposal.TargetStateVersion);
        Assert.Equal("PROPOSE_ACTION", proposal.Recommendation);
        Assert.Equal("CLOSE_ACCESS", proposal.ProposedAction);
        Assert.Equal(DateTimeKind.Utc, proposal.ProducedAtUtc.Kind);
        Assert.Equal(new[] { "evidence-1" }, proposal.EvidenceReferences);

        var publicJson = JsonSerializer.Serialize(status, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("aiExecutionSummary", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("private-experience-reference", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("private-safety-evidence", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("discard-this-model-text", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("discard-this-raw-provider-output", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("discard-this-secret", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-correlation-id", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"report\"", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"stateJson\"", publicJson, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-AGENT-SUMMARY-002")]
    public async Task Workflow_status_omits_invalid_persisted_summary_and_keeps_owner_scope()
    {
        await using var db = CreateDatabase();
        var workflowId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        db.PlanningWorkflows.Add(new PlanningWorkflow
        {
            WorkflowId = workflowId,
            WorkflowType = "TOURIST_RECOMMENDATION",
            Status = "COMPLETED",
            InitiatorUserId = ownerId,
            CreatedAtUtc = DateTime.UtcNow
        });
        db.AgentExecutionStates.Add(new AgentExecutionState
        {
            WorkflowId = workflowId,
            Status = "COMPLETED",
            CorrelationId = "correlation",
            StateJson = JsonSerializer.Serialize(new
            {
                contractVersion = 1,
                workflowId = Guid.NewGuid(),
                status = "COMPLETED",
                steps = Array.Empty<object>()
            }),
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var ownStatus = await service.GetWorkflowStatusAsync(workflowId, ownerId);
        var otherStatus = await service.GetWorkflowStatusAsync(workflowId, Guid.NewGuid());

        Assert.NotNull(ownStatus);
        Assert.Equal("COMPLETED", ownStatus.AiExecutionStatus);
        Assert.Null(ownStatus.AiExecutionSummary);
        Assert.Null(otherStatus);
    }

    private static CoastalPlannerDbContext CreateDatabase() => new(
        new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static CoastalPlannerService CreateService(CoastalPlannerDbContext db) => new(
        db,
        new TestPeerServicesClient(),
        NullLogger<CoastalPlannerService>.Instance);
}
