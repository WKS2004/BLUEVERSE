using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blueverse.CoastalPlanner.Models.Dtos;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class RecommendationsApiAuthTests(PlannerApiFactory factory) : IClassFixture<PlannerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly DateTime _startsAt = DateTime.UtcNow.AddDays(1);
    private readonly Guid _destId = Guid.NewGuid();
    private readonly Guid _actId = Guid.NewGuid();
    private readonly RecommendationRequestDto _request = new(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(4), 2, null, "INTERMEDIATE", false);

    [Fact]
    [Trait("TestId", "PLANNER-REC-AUTH-001")]
    public async Task Create_recommendations_without_token_returns_unauthorized()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/planner/recommendations")
        {
            Content = JsonContent.Create(_request)
        };
        using var response = await _client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-AUTH-002")]
    public async Task Get_recommendation_without_token_returns_unauthorized()
    {
        using var response = await _client.GetAsync($"/api/planner/recommendations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
