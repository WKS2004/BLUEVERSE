using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Data.Entities;

namespace Blueverse.CoastalPlanner.Controllers;

[ServiceKeyRequired]
[ApiController]
[Route("internal/agentic/executions")]
public sealed class AgentExecutionStateController(CoastalPlannerDbContext db, IConfiguration configuration) : ControllerBase
{
    private static readonly HashSet<string> AllowedStatuses = ["PLANNING", "DELEGATING", "BLOCKED", "COMPLETED", "UNAVAILABLE", "SAFE_FAILURE"];

    [HttpPut("{workflowId:guid}")]
    [RequestSizeLimit(262_144)]
    public async Task<IActionResult> Put(Guid workflowId, [FromBody] JsonElement state, CancellationToken cancellationToken)
    {
        var expected = configuration["AgenticAi:ServiceKey"];
        var provided = Request.Headers["X-Blueverse-Service-Key"].ToString();
        if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
            return Unauthorized();

        if (workflowId == Guid.Empty || state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("contractVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var contractVersion) || contractVersion != 1 ||
            !state.TryGetProperty("workflowId", out var stateWorkflow) || stateWorkflow.ValueKind != JsonValueKind.String ||
            stateWorkflow.GetString() != workflowId.ToString() ||
            !state.TryGetProperty("status", out var statusValue) || statusValue.ValueKind != JsonValueKind.String ||
            !AllowedStatuses.Contains(statusValue.GetString()!) ||
            !state.TryGetProperty("correlationId", out var correlation) || correlation.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(correlation.GetString()) || correlation.GetString()!.Length > 80)
            return BadRequest(new { code = "EXECUTION_STATE_INVALID" });
        var allowedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "contractVersion", "workflowId", "correlationId", "status", "plan", "steps", "result", "errorCode", "updatedAtUtc"
        };
        if (state.EnumerateObject().Any(property => !allowedProperties.Contains(property.Name)))
            return BadRequest(new { code = "EXECUTION_STATE_PROPERTIES_INVALID" });

        if (!await db.PlanningWorkflows.AnyAsync(x => x.WorkflowId == workflowId, cancellationToken))
            return NotFound(new { code = "WORKFLOW_NOT_FOUND" });

        var current = await db.AgentExecutionStates.SingleOrDefaultAsync(x => x.WorkflowId == workflowId, cancellationToken);
        if (current is null)
        {
            current = new AgentExecutionState { WorkflowId = workflowId };
            db.AgentExecutionStates.Add(current);
        }
        else if (!string.Equals(current.CorrelationId, correlation.GetString(), StringComparison.Ordinal))
        {
            return Conflict(new { code = "EXECUTION_ALREADY_STARTED" });
        }
        current.Status = statusValue.GetString()!;
        current.CorrelationId = correlation.GetString()!;
        current.StateJson = state.GetRawText();
        current.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ServiceKeyRequiredAttribute : Attribute { }
