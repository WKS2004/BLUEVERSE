using System.Net;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class MapApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MapApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-MAP-001")]
    public async Task GetMapConfiguration_Returns_OpenFreeMap_Styles_And_Attribution()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/map/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.Equal("OpenFreeMap", root.GetProperty("provider").GetString());
        Assert.Equal("VectorTiles", root.GetProperty("tileServiceType").GetString());
        Assert.Equal("liberty", root.GetProperty("defaultStyle").GetString());
        Assert.Contains("OpenFreeMap", root.GetProperty("attribution").GetString());
        Assert.True(root.GetProperty("availableStyles").EnumerateObject().Any());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-MAP-002")]
    public async Task SearchPlaces_MissingQuery_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/map/search");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Query", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-MAP-003")]
    public async Task SearchPlaces_QueryTooShort_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/map/search?q=a");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("detail", out var detail));
        Assert.Contains("at least 2 characters", detail.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-MAP-004")]
    public async Task SearchPlaces_ValidQuery_Returns_Ok_With_Results()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/map/search?q=Mirissa");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.Equal("Mirissa", root.GetProperty("query").GetString());
        Assert.True(root.TryGetProperty("results", out var results));
        Assert.Equal(JsonValueKind.Array, results.ValueKind);
        Assert.True(root.TryGetProperty("source", out _));
    }
}
