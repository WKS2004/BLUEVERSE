using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.CoastalPlanner.Models.Dtos;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class RecommendationsAndWorkflowsApiTests(PlannerApiFactory factory) : IClassFixture<PlannerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("TestId", "PLANNER-REC-API-001")]
    public async Task Create_recommendations_returns_bad_request_on_invalid_parameters()
    {
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        // EndsAt <= StartsAt
        var invalidReq = new RecommendationRequestDto(Guid.NewGuid(), now.AddDays(2), now.AddDays(1), 2, null, "INTERMEDIATE", false);

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/planner/recommendations")
        {
            Content = JsonContent.Create(invalidReq)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(actorId, "planner.recommendations.create"));

        using var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Invalid planning parameters", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-API-002")]
    public async Task Get_recommendation_returns_not_found_for_random_id()
    {
        var actorId = Guid.NewGuid();
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/planner/recommendations/{Guid.NewGuid()}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(actorId, "planner.recommendations.read"));

        using var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Recommendation session not found", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "PLANNER-WORKFLOW-API-001")]
    public async Task Get_workflow_returns_not_found_for_random_id()
    {
        var actorId = Guid.NewGuid();
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/planner/workflows/{Guid.NewGuid()}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(actorId, "planner.workflows.read"));

        using var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Workflow not found", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "PLANNER-WORKFLOW-API-002")]
    public async Task Workflow_endpoint_rejects_missing_token_with_unauthorized()
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/planner/workflows/{Guid.NewGuid()}");
        using var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-API-003")]
    public async Task Recommendations_endpoint_accepts_cookie_authentication()
    {
        var actorId = Guid.NewGuid();
        var token = factory.CreateToken(actorId, "planner.recommendations.read");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/planner/recommendations/{Guid.NewGuid()}");
        request.Headers.Add("Cookie", $"blueverse_access_token={token}");

        using var response = await _client.SendAsync(request);

        // Authenticated and passed permission check, but session does not exist -> 404 NotFound
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
