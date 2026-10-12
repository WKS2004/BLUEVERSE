using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blueverse.CoastalPlanner.Models.Dtos;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class ItinerariesApiAuthTests(PlannerApiFactory factory) : IClassFixture<PlannerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _destId = Guid.NewGuid();
    private readonly DateTime _startsAt = DateTime.UtcNow.AddDays(1);
    private readonly CreateItineraryRequestDto _createRequest = new(
        "Coastal Weekend",
        null,
        DateTime.UtcNow.AddDays(1),
        DateTime.UtcNow.AddDays(1).AddHours(4),
        []);

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-AUTH-001")]
    public async Task Create_itinerary_without_token_returns_unauthorized()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(_createRequest)
        };
        using var response = await _client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-AUTH-002")]
    public async Task Get_itinerary_without_token_returns_unauthorized()
    {
        using var response = await _client.GetAsync($"/api/planner/itineraries/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
