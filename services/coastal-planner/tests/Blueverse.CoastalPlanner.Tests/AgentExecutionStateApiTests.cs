using System.Net;
using System.Net.Http.Json;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class AgentExecutionStateApiTests
{
    [Fact]
    [Trait("TestId", "PLANNER-AI-STATE-HTTP-001")]
    public async Task Private_state_write_requires_service_identity_and_existing_workflow()
    {
        using var root = new PlannerApiFactory();
        using var configured = root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgenticAi:ServiceKey"] = "planner-agent-test-key"
            })));
        using var client = configured.CreateClient();
        var workflowId = Guid.NewGuid();
        var state = new { contractVersion = 1, workflowId, correlationId = "execution-one", status = "PLANNING" };
        var route = $"/internal/agentic/executions/{workflowId}";

        using var unauthenticated = await client.PutAsJsonAsync(route, state);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        using var authenticatedRequest = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(state) };
        authenticatedRequest.Headers.Add("X-Blueverse-Service-Key", "planner-agent-test-key");
        using var missingWorkflow = await client.SendAsync(authenticatedRequest);
        Assert.Equal(HttpStatusCode.NotFound, missingWorkflow.StatusCode);

        using var malformedRequest = new HttpRequestMessage(HttpMethod.Put, route)
        {
            Content = JsonContent.Create(new { contractVersion = 1, workflowId = 17, correlationId = "bad", status = "PLANNING" })
        };
        malformedRequest.Headers.Add("X-Blueverse-Service-Key", "planner-agent-test-key");
        using var malformed = await client.SendAsync(malformedRequest);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        using var scope = configured.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoastalPlannerDbContext>();
        db.PlanningWorkflows.Add(new PlanningWorkflow
        {
            WorkflowId = workflowId,
            WorkflowType = "TOURIST_RECOMMENDATION",
            Status = "COMPLETED",
            InitiatorUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        using var validRequest = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(state) };
        validRequest.Headers.Add("X-Blueverse-Service-Key", "planner-agent-test-key");
        using var accepted = await client.SendAsync(validRequest);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Equal("PLANNING", await db.AgentExecutionStates.Where(x => x.WorkflowId == workflowId)
            .Select(x => x.Status).SingleAsync());

        var duplicate = new { contractVersion = 1, workflowId, correlationId = "different-execution", status = "PLANNING" };
        using var duplicateRequest = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(duplicate) };
        duplicateRequest.Headers.Add("X-Blueverse-Service-Key", "planner-agent-test-key");
        using var conflict = await client.SendAsync(duplicateRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("execution-one", await db.AgentExecutionStates.Where(x => x.WorkflowId == workflowId)
            .Select(x => x.CorrelationId).SingleAsync());
    }
}
