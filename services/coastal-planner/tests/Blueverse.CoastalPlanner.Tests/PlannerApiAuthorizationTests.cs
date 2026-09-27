using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.CoastalPlanner.Models.Dtos;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class PlannerApiAuthorizationTests(PlannerApiFactory factory) : IClassFixture<PlannerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("TestId", "PLANNER-AUTH-001")]
    public async Task Recommendations_require_create_permission_and_complete_with_unavailable_peers()
    {
        var request = CreateRequest();
        using var anonymousResponse = await _client.PostAsJsonAsync("/api/planner/recommendations", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var deniedRequest = CreateRequestMessage(request, factory.CreateToken(Guid.NewGuid(), "planner.recommendations.read"));
        using var deniedResponse = await _client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);

        var actorId = Guid.NewGuid();
        using var allowedRequest = CreateRequestMessage(request, factory.CreateToken(actorId, "planner.recommendations.create"));
        using var allowedResponse = await _client.SendAsync(allowedRequest);
        Assert.Equal(HttpStatusCode.Created, allowedResponse.StatusCode);
        Assert.True(allowedResponse.Headers.Location?.ToString().Contains("/api/planner/recommendations/", StringComparison.Ordinal));

        using var responseJson = JsonDocument.Parse(await allowedResponse.Content.ReadAsStringAsync());
        var root = responseJson.RootElement;
        Assert.Equal("COMPLETED", root.GetProperty("status").GetString());
        Assert.Empty(root.GetProperty("candidates").EnumerateArray());
        var recommendationId = root.GetProperty("recommendationId").GetGuid();
        var workflowId = root.GetProperty("workflowId").GetGuid();
        var notes = root.GetProperty("uncertaintyNotes").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains(notes, note => note?.Contains("Experience Catalogue endpoint has not responded", StringComparison.Ordinal) == true);
        Assert.Contains(notes, note => note?.Contains("Coastal Operations endpoint has not responded", StringComparison.Ordinal) == true);

        using var ownRecommendationRequest = CreateGetRequest(
            $"/api/planner/recommendations/{recommendationId}",
            factory.CreateToken(actorId, "planner.recommendations.read"));
        using var ownRecommendationResponse = await _client.SendAsync(ownRecommendationRequest);
        Assert.Equal(HttpStatusCode.OK, ownRecommendationResponse.StatusCode);
        using var ownRecommendationJson = JsonDocument.Parse(await ownRecommendationResponse.Content.ReadAsStringAsync());
        Assert.Equal(notes, ownRecommendationJson.RootElement.GetProperty("uncertaintyNotes").EnumerateArray()
            .Select(value => value.GetString()).ToArray());

        using var otherRecommendationRequest = CreateGetRequest(
            $"/api/planner/recommendations/{recommendationId}",
            factory.CreateToken(Guid.NewGuid(), "planner.recommendations.read"));
        using var otherRecommendationResponse = await _client.SendAsync(otherRecommendationRequest);
        Assert.Equal(HttpStatusCode.NotFound, otherRecommendationResponse.StatusCode);

        using var ownWorkflowRequest = CreateGetRequest(
            $"/api/planner/workflows/{workflowId}",
            factory.CreateToken(actorId, "planner.workflows.read"));
        using var ownWorkflowResponse = await _client.SendAsync(ownWorkflowRequest);
        Assert.Equal(HttpStatusCode.OK, ownWorkflowResponse.StatusCode);
        using var ownWorkflowJson = JsonDocument.Parse(await ownWorkflowResponse.Content.ReadAsStringAsync());
        Assert.Equal("COMPLETED", ownWorkflowJson.RootElement.GetProperty("status").GetString());

        using var otherWorkflowRequest = CreateGetRequest(
            $"/api/planner/workflows/{workflowId}",
            factory.CreateToken(Guid.NewGuid(), "planner.workflows.read"));
        using var otherWorkflowResponse = await _client.SendAsync(otherWorkflowRequest);
        Assert.Equal(HttpStatusCode.NotFound, otherWorkflowResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-AUTH-002")]
    public async Task Itinerary_and_biodiversity_routes_require_their_named_permissions()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var createItinerary = new CreateItineraryRequestDto(
            "Coastal day", null, startsAt, startsAt.AddHours(2),
            [new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), null,
                "Coastal activity", 0, startsAt, startsAt.AddHours(1))]);

        using var wrongItineraryPermission = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(createItinerary)
        };
        wrongItineraryPermission.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.CreateToken(ownerId, "planner.recommendations.create"));
        using var deniedItinerary = await _client.SendAsync(wrongItineraryPermission);
        Assert.Equal(HttpStatusCode.Forbidden, deniedItinerary.StatusCode);

        using var allowedItineraryRequest = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(createItinerary)
        };
        allowedItineraryRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.CreateToken(ownerId, "planner.itineraries.manage"));
        using var allowedItinerary = await _client.SendAsync(allowedItineraryRequest);
        Assert.Equal(HttpStatusCode.Created, allowedItinerary.StatusCode);
        using var itineraryJson = JsonDocument.Parse(await allowedItinerary.Content.ReadAsStringAsync());
        var item = Assert.Single(itineraryJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("UNKNOWN", item.GetProperty("lastAvailabilityStatus").GetString());
        Assert.Equal("UNKNOWN", item.GetProperty("lastSuitabilityStatus").GetString());
        Assert.Equal("UNKNOWN", item.GetProperty("lastOperationalStatus").GetString());

        var destinationId = Guid.NewGuid();
        using var deniedBiodiversityRequest = CreateGetRequest(
            $"/api/planner/biodiversity/predictions?destinationId={destinationId}",
            factory.CreateToken(ownerId, "planner.itineraries.manage"));
        using var deniedBiodiversity = await _client.SendAsync(deniedBiodiversityRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deniedBiodiversity.StatusCode);

        using var allowedBiodiversityRequest = CreateGetRequest(
            $"/api/planner/biodiversity/predictions?destinationId={destinationId}",
            factory.CreateToken(ownerId, "planner.biodiversity.read"));
        using var allowedBiodiversity = await _client.SendAsync(allowedBiodiversityRequest);
        Assert.Equal(HttpStatusCode.OK, allowedBiodiversity.StatusCode);
        using var biodiversityJson = JsonDocument.Parse(await allowedBiodiversity.Content.ReadAsStringAsync());
        Assert.Equal("UNAVAILABLE", biodiversityJson.RootElement.GetProperty("status").GetString());
        Assert.Empty(biodiversityJson.RootElement.GetProperty("predictedSpecies").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, biodiversityJson.RootElement.GetProperty("modelMetadata").ValueKind);
        Assert.Contains("Biodiversity ML endpoint has not responded",
            biodiversityJson.RootElement.GetProperty("limitations").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-VALIDATION-HTTP-001")]
    public async Task Biodiversity_endpoint_rejects_an_empty_activity_id()
    {
        var destinationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        using var request = CreateGetRequest(
            $"/api/planner/biodiversity/predictions?destinationId={destinationId}&activityId={Guid.Empty}",
            factory.CreateToken(actorId, "planner.biodiversity.read"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("activityId", body.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    private static RecommendationRequestDto CreateRequest()
    {
        var startsAt = DateTime.UtcNow.AddDays(1);
        return new RecommendationRequestDto(Guid.NewGuid(), startsAt, startsAt.AddHours(4), 2, null, "INTERMEDIATE", false);
    }

    private static HttpRequestMessage CreateRequestMessage(RecommendationRequestDto request, string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/planner/recommendations")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    private static HttpRequestMessage CreateGetRequest(string path, string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }
}
