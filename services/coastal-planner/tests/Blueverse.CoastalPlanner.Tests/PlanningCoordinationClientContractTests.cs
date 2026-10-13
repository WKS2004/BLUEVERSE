using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Blueverse.CoastalPlanner.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class PlanningCoordinationClientContractTests
{
    [Fact]
    [Trait("TestId", "PLANNER-AI-SEAM-004")]
    public async Task Dispatch_forwards_validated_planning_context_to_private_agent()
    {
        var workflowId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var startsAt = DateTime.SpecifyKind(new DateTime(2026, 11, 1, 10, 0, 0), DateTimeKind.Utc);
        var endsAt = startsAt.AddHours(4);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgenticAi:Enabled"] = "true",
            ["AgenticAi:BaseUrl"] = "http://agentic.test",
            ["AgenticAi:ServiceKey"] = "private-agent-test-key",
            ["PeerServices:ExperienceCatalogueUrl"] = "http://api.test"
        }).Build();

        var dispatchObserved = false;
        var http = new HttpClient(new Handler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/internal/agentic/agents/planning-coordination/health")
            {
                Assert.True(request.Headers.TryGetValues("X-Blueverse-Service-Key", out var values));
                Assert.Equal("private-agent-test-key", Assert.Single(values!));
                return Json(HttpStatusCode.OK, new { status = "AVAILABLE" });
            }

            if (request.RequestUri.AbsolutePath == "/api/experiences/catalogue")
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("Bearer catalogue-read-token", request.Headers.Authorization?.ToString());
                Assert.False(request.Headers.Contains("X-Blueverse-Service-Key"));
                return Json(HttpStatusCode.OK, new[]
                {
                    new
                    {
                        destinationId,
                        activityId,
                        offeringId = Guid.NewGuid(),
                        title = "Mirissa coastal walk",
                        availabilityStatus = "AVAILABLE",
                        publicationState = "PUBLISHED",
                        availableFrom = DateTime.UtcNow.AddDays(-1),
                        availableUntil = endsAt.AddDays(1),
                        experienceLevels = new[] { "ADVANCED" },
                        checkedAt = DateTime.UtcNow,
                        timeZone = "Asia/Colombo",
                        latitude = 5.9,
                        longitude = 80.4,
                        activityName = "Coastal walk",
                        region = "Southern",
                        destinationName = "Mirissa"
                    }
                });
            }

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/internal/agentic/agents/planning-coordination/dispatch", request.RequestUri.AbsolutePath);
            Assert.True(request.Content?.Headers.ContentLength > 0);
            Assert.True(request.Headers.TryGetValues("X-Blueverse-Service-Key", out var serviceKeyValues));
            Assert.Equal("private-agent-test-key", Assert.Single(serviceKeyValues!));
            Assert.Null(request.Headers.Authorization);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var root = body.RootElement;
            Assert.Equal(workflowId, root.GetProperty("workflowId").GetGuid());
            Assert.Equal(recommendationId, root.GetProperty("recommendationId").GetGuid());
            Assert.Equal("Coastal planning for Mirissa", root.GetProperty("objective").GetString());
            Assert.Equal(destinationId, root.GetProperty("destinationId").GetGuid());
            Assert.Equal(activityId, root.GetProperty("preferredActivityIds")[0].GetGuid());
            var constraints = root.GetProperty("constraints");
            Assert.Equal("ADVANCED", constraints.GetProperty("experienceLevel").GetString());
            Assert.Equal(4, constraints.GetProperty("durationHours").GetInt32());
            Assert.False(constraints.GetProperty("includeBiodiversityContext").GetBoolean());
            dispatchObserved = true;
            return Json(HttpStatusCode.OK, new { workflowId, status = "COMPLETED" });
        }));

        var caller = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "planner-user")], "test"))
        };
        caller.Request.Headers.Authorization = "Bearer catalogue-read-token";
        var client = new PlanningCoordinationClient(http, config, new HttpContextAccessor { HttpContext = caller });
        var result = await client.DispatchAsync(new PlanningCoordinationRequest(
            workflowId, recommendationId, destinationId, startsAt, endsAt, [activityId],
            "Coastal planning for Mirissa", "ADVANCED", 4, IncludeBiodiversityContext: false), default);

        Assert.True(dispatchObserved);
        Assert.Equal(workflowId, result.WorkflowId);
        Assert.Equal("COMPLETED", result.Status);
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T value) =>
        new(statusCode) { Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
