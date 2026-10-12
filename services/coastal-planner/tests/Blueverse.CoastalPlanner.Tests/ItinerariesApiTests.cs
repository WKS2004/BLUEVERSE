using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.CoastalPlanner.Models.Dtos;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class ItinerariesApiTests(PlannerApiFactory factory) : IClassFixture<PlannerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-001")]
    public async Task Create_itinerary_returns_created_with_location_header_and_item_details()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Beach Trip", 0, startsAt, startsAt.AddHours(2));
        var request = new CreateItineraryRequestDto("Southern Coast Tour", "Relaxing tour", startsAt, startsAt.AddHours(4), [item]);

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(ownerId, "planner.itineraries.manage"));

        using var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/planner/itineraries/", response.Headers.Location.ToString(), StringComparison.Ordinal);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("Southern Coast Tour", root.GetProperty("title").GetString());
        Assert.Equal(1, root.GetProperty("concurrencyVersion").GetInt32());
        var items = root.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("Beach Trip", items[0].GetProperty("title").GetString());
        Assert.Equal("UNKNOWN", items[0].GetProperty("lastSuitabilityStatus").GetString());
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-002")]
    public async Task Create_itinerary_returns_bad_request_on_validation_failure()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        // Duplicate order index
        var item1 = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Trip 1", 0, startsAt, startsAt.AddHours(1));
        var item2 = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Trip 2", 0, startsAt.AddHours(1), startsAt.AddHours(2));
        var request = new CreateItineraryRequestDto("Invalid Itinerary", null, startsAt, startsAt.AddHours(3), [item1, item2]);

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(ownerId, "planner.itineraries.manage"));

        using var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Invalid itinerary", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-003")]
    public async Task List_itineraries_returns_paginated_list_scoped_to_caller()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var token = factory.CreateToken(ownerId, "planner.itineraries.manage");

        for (var i = 0; i < 3; i++)
        {
            var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"Activity {i}", 0, startsAt, startsAt.AddHours(1));
            var req = new CreateItineraryRequestDto($"Vacation {i}", null, startsAt, startsAt.AddHours(2), [item]);
            using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
            {
                Content = JsonContent.Create(req)
            };
            createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _client.SendAsync(createMsg);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        using var listMsg = new HttpRequestMessage(HttpMethod.Get, "/api/planner/itineraries?page=1&pageSize=2");
        listMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var listRes = await _client.SendAsync(listMsg);

        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        using var json = JsonDocument.Parse(await listRes.Content.ReadAsStringAsync());
        var array = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, array.Length);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-004")]
    public async Task Get_itinerary_returns_ok_for_owner_and_not_found_for_other_users()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Dive", 0, startsAt, startsAt.AddHours(2));
        var req = new CreateItineraryRequestDto("Private Dive Trip", null, startsAt, startsAt.AddHours(4), [item]);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(req)
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(ownerId, "planner.itineraries.manage"));
        using var createRes = await _client.SendAsync(createMsg);
        using var createdJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var itineraryId = createdJson.RootElement.GetProperty("itineraryId").GetGuid();

        // Owner gets 200 OK
        using var ownerMsg = new HttpRequestMessage(HttpMethod.Get, $"/api/planner/itineraries/{itineraryId}");
        ownerMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(ownerId, "planner.itineraries.manage"));
        using var ownerRes = await _client.SendAsync(ownerMsg);
        Assert.Equal(HttpStatusCode.OK, ownerRes.StatusCode);

        // Other user gets 404 NotFound
        using var otherMsg = new HttpRequestMessage(HttpMethod.Get, $"/api/planner/itineraries/{itineraryId}");
        otherMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(Guid.NewGuid(), "planner.itineraries.manage"));
        using var otherRes = await _client.SendAsync(otherMsg);
        Assert.Equal(HttpStatusCode.NotFound, otherRes.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-005")]
    public async Task Update_itinerary_increments_version_and_returns_ok()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Original", 0, startsAt, startsAt.AddHours(2));
        var createReq = new CreateItineraryRequestDto("Initial Plan", null, startsAt, startsAt.AddHours(3), [item]);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(createReq)
        };
        var token = factory.CreateToken(ownerId, "planner.itineraries.manage");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var createRes = await _client.SendAsync(createMsg);
        using var createdJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var itineraryId = createdJson.RootElement.GetProperty("itineraryId").GetGuid();

        var updatedItem = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Updated Activity", 0, startsAt, startsAt.AddHours(2));
        var updateReq = new UpdateItineraryRequestDto("Updated Plan Title", "New description", startsAt, startsAt.AddHours(4), 1, [updatedItem]);

        using var updateMsg = new HttpRequestMessage(HttpMethod.Put, $"/api/planner/itineraries/{itineraryId}")
        {
            Content = JsonContent.Create(updateReq)
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var updateRes = await _client.SendAsync(updateMsg);

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        using var updatedJson = JsonDocument.Parse(await updateRes.Content.ReadAsStringAsync());
        Assert.Equal("Updated Plan Title", updatedJson.RootElement.GetProperty("title").GetString());
        Assert.Equal(2, updatedJson.RootElement.GetProperty("concurrencyVersion").GetInt32());
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-006")]
    public async Task Update_itinerary_returns_409_conflict_when_concurrency_version_is_stale()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Act", 0, startsAt, startsAt.AddHours(2));
        var createReq = new CreateItineraryRequestDto("Concurrency Test", null, startsAt, startsAt.AddHours(3), [item]);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(createReq)
        };
        var token = factory.CreateToken(ownerId, "planner.itineraries.manage");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var createRes = await _client.SendAsync(createMsg);
        using var createdJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var itineraryId = createdJson.RootElement.GetProperty("itineraryId").GetGuid();

        // Stale concurrency version (version 99 instead of 1)
        var updateReq = new UpdateItineraryRequestDto("Conflicting Plan", null, startsAt, startsAt.AddHours(3), 99, [item]);

        using var updateMsg = new HttpRequestMessage(HttpMethod.Put, $"/api/planner/itineraries/{itineraryId}")
        {
            Content = JsonContent.Create(updateReq)
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var updateRes = await _client.SendAsync(updateMsg);

        Assert.Equal(HttpStatusCode.Conflict, updateRes.StatusCode);
        using var problemJson = JsonDocument.Parse(await updateRes.Content.ReadAsStringAsync());
        Assert.Equal(409, problemJson.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Concurrency Conflict", problemJson.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-007")]
    public async Task Update_itinerary_returns_404_for_other_users()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Act", 0, startsAt, startsAt.AddHours(2));
        var createReq = new CreateItineraryRequestDto("User Plan", null, startsAt, startsAt.AddHours(3), [item]);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(createReq)
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(ownerId, "planner.itineraries.manage"));
        using var createRes = await _client.SendAsync(createMsg);
        using var createdJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var itineraryId = createdJson.RootElement.GetProperty("itineraryId").GetGuid();

        var updateReq = new UpdateItineraryRequestDto("Tampered Plan", null, startsAt, startsAt.AddHours(3), 1, [item]);
        using var updateMsg = new HttpRequestMessage(HttpMethod.Put, $"/api/planner/itineraries/{itineraryId}")
        {
            Content = JsonContent.Create(updateReq)
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(Guid.NewGuid(), "planner.itineraries.manage"));
        using var updateRes = await _client.SendAsync(updateMsg);

        Assert.Equal(HttpStatusCode.NotFound, updateRes.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-008")]
    public async Task Delete_itinerary_returns_no_content_and_subsequent_get_returns_not_found()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Act", 0, startsAt, startsAt.AddHours(2));
        var createReq = new CreateItineraryRequestDto("Plan to Delete", null, startsAt, startsAt.AddHours(3), [item]);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(createReq)
        };
        var token = factory.CreateToken(ownerId, "planner.itineraries.manage");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var createRes = await _client.SendAsync(createMsg);
        using var createdJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var itineraryId = createdJson.RootElement.GetProperty("itineraryId").GetGuid();

        // Delete with owner token -> 204 NoContent
        using var deleteMsg = new HttpRequestMessage(HttpMethod.Delete, $"/api/planner/itineraries/{itineraryId}");
        deleteMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var deleteRes = await _client.SendAsync(deleteMsg);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // Verify GET returns 404
        using var getMsg = new HttpRequestMessage(HttpMethod.Get, $"/api/planner/itineraries/{itineraryId}");
        getMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var getRes = await _client.SendAsync(getMsg);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-009")]
    public async Task Delete_itinerary_returns_404_for_other_users()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Act", 0, startsAt, startsAt.AddHours(2));
        var createReq = new CreateItineraryRequestDto("Plan Protected", null, startsAt, startsAt.AddHours(3), [item]);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(createReq)
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(ownerId, "planner.itineraries.manage"));
        using var createRes = await _client.SendAsync(createMsg);
        using var createdJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var itineraryId = createdJson.RootElement.GetProperty("itineraryId").GetGuid();

        using var deleteMsg = new HttpRequestMessage(HttpMethod.Delete, $"/api/planner/itineraries/{itineraryId}");
        deleteMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(Guid.NewGuid(), "planner.itineraries.manage"));
        using var deleteRes = await _client.SendAsync(deleteMsg);

        Assert.Equal(HttpStatusCode.NotFound, deleteRes.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-010")]
    public async Task ReEvaluate_itinerary_returns_ok_with_results_and_404_for_non_owner()
    {
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Reeval Act", 0, startsAt, startsAt.AddHours(2));
        var createReq = new CreateItineraryRequestDto("Plan to Re-evaluate", null, startsAt, startsAt.AddHours(3), [item]);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(createReq)
        };
        var token = factory.CreateToken(ownerId, "planner.itineraries.manage");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var createRes = await _client.SendAsync(createMsg);
        using var createdJson = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var itineraryId = createdJson.RootElement.GetProperty("itineraryId").GetGuid();

        // Re-evaluate with owner token
        using var reevalMsg = new HttpRequestMessage(HttpMethod.Post, $"/api/planner/itineraries/{itineraryId}/re-evaluations")
        {
            Content = JsonContent.Create(new ItineraryReEvaluationRequestDto())
        };
        reevalMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var reevalRes = await _client.SendAsync(reevalMsg);

        Assert.Equal(HttpStatusCode.OK, reevalRes.StatusCode);
        using var reevalJson = JsonDocument.Parse(await reevalRes.Content.ReadAsStringAsync());
        Assert.Equal(itineraryId, reevalJson.RootElement.GetProperty("itineraryId").GetGuid());
        Assert.True(reevalJson.RootElement.GetProperty("items").EnumerateArray().Any());

        // Re-evaluate with other user token -> 404
        using var otherReevalMsg = new HttpRequestMessage(HttpMethod.Post, $"/api/planner/itineraries/{itineraryId}/re-evaluations")
        {
            Content = JsonContent.Create(new ItineraryReEvaluationRequestDto())
        };
        otherReevalMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(Guid.NewGuid(), "planner.itineraries.manage"));
        using var otherReevalRes = await _client.SendAsync(otherReevalMsg);
        Assert.Equal(HttpStatusCode.NotFound, otherReevalRes.StatusCode);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-API-011")]
    public async Task Cookie_authentication_is_accepted_for_itinerary_endpoints()
    {
        var ownerId = Guid.NewGuid();
        var token = factory.CreateToken(ownerId, "planner.itineraries.manage");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/planner/itineraries");
        request.Headers.Add("Cookie", $"blueverse_access_token={token}");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
