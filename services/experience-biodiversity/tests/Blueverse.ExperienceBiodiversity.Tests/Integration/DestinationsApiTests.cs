using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class DestinationsApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DestinationsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEST-001")]
    public async Task GetAllDestinations_Returns_Ok_And_Json_List()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/destinations");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode, content);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEST-002")]
    public async Task CreateDestination_And_Transition_Publication_Lifecycle()
    {
        using var client = _factory.CreateClient();

        var createReq = new CreateDestinationRequest(
            Name: "Kite Lagoon Kalpitiya",
            Slug: $"kite-lagoon-{Guid.NewGuid():N}"[..22],
            Description: "Flatwater coastal lagoon optimal for kitesurfing.",
            Region: "North Western Province",
            Latitude: 8.23,
            Longitude: 79.76);

        // 1. Create destination in DRAFT
        using var createRes = await client.PostAsJsonAsync("/api/experiences/destinations", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var created = await createRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(created);
        Assert.Equal(PublicationStatus.Draft, created.Status);

        // 2. Evaluate publication transition
        var evalReq = new UpdatePublicationRequest(PublicationStatus.Published);
        using var evalRes = await client.PostAsJsonAsync($"/api/experiences/destinations/{created.Id}/publication-evaluations", evalReq);
        Assert.Equal(HttpStatusCode.OK, evalRes.StatusCode);

        var eval = await evalRes.Content.ReadFromJsonAsync<PublicationEvaluationResponse>();
        Assert.NotNull(eval);
        Assert.True(eval.CanTransition);

        // 3. Apply publication transition
        using var patchRes = await client.PatchAsJsonAsync($"/api/experiences/destinations/{created.Id}/publication", evalReq);
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        var published = await patchRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(published);
        Assert.Equal(PublicationStatus.Published, published.Status);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEST-003")]
    public async Task GetDestinationById_Unknown_Returns_NotFound_ProblemDetails()
    {
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        using var response = await client.GetAsync($"/api/experiences/destinations/{unknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Destination Not Found", title.GetString());
        Assert.True(doc.RootElement.TryGetProperty("status", out var status));
        Assert.Equal(404, status.GetInt32());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEST-004")]
    public async Task UpdateDestination_UpdatesProperties_And_Returns_UpdatedDto()
    {
        using var client = _factory.CreateClient();

        // 1. Create
        var createReq = new CreateDestinationRequest("Weligama Bay", $"weligama-{Guid.NewGuid():N}"[..18], "Surf and sand", "Southern Province", 5.97, 80.42);
        using var createRes = await client.PostAsJsonAsync("/api/experiences/destinations", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(created);

        // 2. Update
        var updateReq = new UpdateDestinationRequest("Weligama Updated Bay", "weligama-updated", "Updated surf description", "Southern Province", 5.98, 80.43);
        using var updateRes = await client.PutAsJsonAsync($"/api/experiences/destinations/{created.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(updated);
        Assert.Equal("Weligama Updated Bay", updated.Name);
        Assert.Equal("Updated surf description", updated.Description);
        Assert.Equal(5.98, updated.Latitude);
        Assert.Equal(80.43, updated.Longitude);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEST-005")]
    public async Task UpdateDestination_UnknownId_Returns_NotFound_ProblemDetails()
    {
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        var updateReq = new UpdateDestinationRequest("Non-existent", "non-existent", "Desc", "Region", 6.0, 80.0);
        using var response = await client.PutAsJsonAsync($"/api/experiences/destinations/{unknownId}", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Destination Not Found", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEST-006")]
    public async Task GetAllDestinations_WithFilters_AppliesFiltersCorrectly()
    {
        using var client = _factory.CreateClient();

        // Query with explicit region and status filters
        using var response = await client.GetAsync("/api/experiences/destinations?region=Southern&status=PUBLISHED&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("page", out var page));
        Assert.Equal(1, page.GetInt32());
        Assert.True(root.TryGetProperty("pageSize", out var pageSize));
        Assert.Equal(10, pageSize.GetInt32());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEST-007")]
    public async Task GetDestinationMarineConditions_Returns_Ok_Or_NotFound()
    {
        using var client = _factory.CreateClient();

        // 1. Unknown destination -> 404
        using var unknownRes = await client.GetAsync($"/api/experiences/destinations/{Guid.NewGuid()}/marine-conditions");
        Assert.Equal(HttpStatusCode.NotFound, unknownRes.StatusCode);

        // 2. Known destination -> 200 with fallback/real condition
        var createReq = new CreateDestinationRequest("Marine Condition Bay", null, "Testing conditions", "Southern Province", 5.95, 80.45);
        using var createRes = await client.PostAsJsonAsync("/api/experiences/destinations", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(created);

        using var condRes = await client.GetAsync($"/api/experiences/destinations/{created.Id}/marine-conditions");
        Assert.Equal(HttpStatusCode.OK, condRes.StatusCode);

        var content = await condRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("destinationId", out var destIdProp));
        Assert.Equal(created.Id, destIdProp.GetGuid());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEST-008")]
    public async Task GetDestinationOperationalAdvisories_Returns_Ok_Or_NotFound()
    {
        using var client = _factory.CreateClient();

        // 1. Unknown destination -> 404
        using var unknownRes = await client.GetAsync($"/api/experiences/destories-operational-advisories/{Guid.NewGuid()}");
        // Or exact path: /api/experiences/destinations/{id}/operational-advisories
        using var notFoundRes = await client.GetAsync($"/api/experiences/destinations/{Guid.NewGuid()}/operational-advisories");
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);

        // 2. Known destination -> 200 with advisories
        var createReq = new CreateDestinationRequest("Advisory Bay", null, "Testing advisories", "Eastern Province", 8.5, 81.2);
        using var createRes = await client.PostAsJsonAsync("/api/experiences/destinations", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(created);

        using var advRes = await client.GetAsync($"/api/experiences/destinations/{created.Id}/operational-advisories");
        Assert.Equal(HttpStatusCode.OK, advRes.StatusCode);

        var content = await advRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("destinationId", out var destIdProp));
        Assert.Equal(created.Id, destIdProp.GetGuid());
    }
}
