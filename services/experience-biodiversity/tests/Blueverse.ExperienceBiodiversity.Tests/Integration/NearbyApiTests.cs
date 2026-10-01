using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class NearbyApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public NearbyApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-NRB-001")]
    public async Task GetNearby_MissingCoordinates_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();

        // 1. Neither coordinate
        using var res1 = await client.GetAsync("/api/experiences/nearby");
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);

        // 2. Latitude only
        using var res2 = await client.GetAsync("/api/experiences/nearby?latitude=5.9482");
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);

        // 3. Longitude only
        using var res3 = await client.GetAsync("/api/experiences/nearby?longitude=80.4716");
        Assert.Equal(HttpStatusCode.BadRequest, res3.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-NRB-002")]
    public async Task GetNearby_InvalidLatitude_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/nearby?latitude=95.0&longitude=80.0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Latitude", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-NRB-003")]
    public async Task GetNearby_InvalidLongitude_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/nearby?latitude=5.0&longitude=190.0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Longitude", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-NRB-004")]
    public async Task GetNearby_ValidCoordinates_Returns_NearbyDestinations_OrderedByDistance()
    {
        using var client = _factory.CreateClient();

        // Seed a published destination to be certain
        var destReq = new CreateDestinationRequest("Mirissa Coast", $"mirissa-{Guid.NewGuid():N}"[..18], "Whale haven", "Southern Province", 5.9482, 80.4716);
        using var createRes = await client.PostAsJsonAsync("/api/experiences/destinations", destReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var dest = await createRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(dest);

        // Publish it
        await client.PatchAsJsonAsync($"/api/experiences/destinations/{dest.Id}/publication", new UpdatePublicationRequest(PublicationStatus.Published));

        // Query near Mirissa coordinates
        using var nearbyRes = await client.GetAsync("/api/experiences/nearby?latitude=5.9482&longitude=80.4716&radiusMeters=50000");
        Assert.Equal(HttpStatusCode.OK, nearbyRes.StatusCode);

        var content = await nearbyRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("results", out var results));
        Assert.Equal(JsonValueKind.Array, results.ValueKind);
        Assert.True(results.GetArrayLength() > 0);

        // Distance of first item should be very close to 0
        var firstDist = results[0].GetProperty("distanceMeters").GetDouble();
        Assert.True(firstDist >= 0);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-NRB-005")]
    public async Task GetNearby_LocationKeyword_ResolvesLocationAndReturnsNearbyDestinations()
    {
        using var client = _factory.CreateClient();

        // Seed a published destination to be searched
        var destReq = new CreateDestinationRequest("Weligama Bay Haven", $"weligama-{Guid.NewGuid():N}"[..18], "Surf bay", "Southern Province", 5.9723, 80.4287);
        using var createRes = await client.PostAsJsonAsync("/api/experiences/destinations", destReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var dest = await createRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(dest);

        // Publish it
        await client.PatchAsJsonAsync($"/api/experiences/destinations/{dest.Id}/publication", new UpdatePublicationRequest(PublicationStatus.Published));

        // Query by natural text search keyword (e.g. "weligama")
        using var nearbyRes = await client.GetAsync("/api/experiences/nearby?q=weligama&radiusMeters=25000");
        Assert.Equal(HttpStatusCode.OK, nearbyRes.StatusCode);

        var content = await nearbyRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("query", out var query));
        Assert.Equal("weligama", query.GetProperty("location").GetString());
        Assert.True(query.TryGetProperty("resolvedLocation", out var resolved));
        Assert.False(string.IsNullOrEmpty(resolved.GetString()));

        Assert.True(root.TryGetProperty("results", out var results));
        Assert.Equal(JsonValueKind.Array, results.ValueKind);
        Assert.True(results.GetArrayLength() > 0);
    }
}
