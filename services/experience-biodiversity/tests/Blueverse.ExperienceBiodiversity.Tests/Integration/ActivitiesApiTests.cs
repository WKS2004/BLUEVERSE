using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class ActivitiesApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivitiesApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-ACT-001")]
    public async Task GetAllActivities_Returns_Ok_With_Array()
    {
        using var client = _factory.CreateAnonymousClient();

        using var response = await client.GetAsync("/api/experiences/activities");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(content);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-ACT-002")]
    public async Task GetActivityById_Unknown_Returns_NotFound_ProblemDetails()
    {
        using var client = _factory.CreateAnonymousClient();
        var unknownId = Guid.NewGuid();

        using var response = await client.GetAsync($"/api/experiences/activities/{unknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Activity Not Found", title.GetString());
        Assert.True(doc.RootElement.TryGetProperty("status", out var status));
        Assert.Equal(404, status.GetInt32());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-ACT-003")]
    public async Task CreateActivity_And_UpdatePublication_Lifecycle()
    {
        using var client = _factory.CreateAdminClient();
        var uniqueCode = $"TEST_ACT_{Guid.NewGuid():N}"[..18];

        var createReq = new CreateActivityRequest(
            Code: uniqueCode,
            Name: "Coastal Birdwatching",
            Description: "Guided birdwatching tours along marine mangroves.",
            Category: "Wildlife");

        // 1. Create activity in DRAFT
        using var createRes = await client.PostAsJsonAsync("/api/experiences/activities", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var created = await createRes.Content.ReadFromJsonAsync<ActivityDto>();
        Assert.NotNull(created);
        Assert.Equal(PublicationStatus.Draft, created.Status);
        Assert.Equal(uniqueCode.ToUpperInvariant(), created.Code);

        // 2. Duplicate code check -> Conflict (409)
        using var dupRes = await client.PostAsJsonAsync("/api/experiences/activities", createReq);
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);

        // 3. Evaluate publication
        var pubReq = new UpdatePublicationRequest(PublicationStatus.Published);
        using var evalRes = await client.PostAsJsonAsync($"/api/experiences/activities/{created.Id}/publication-evaluations", pubReq);
        Assert.Equal(HttpStatusCode.OK, evalRes.StatusCode);

        var eval = await evalRes.Content.ReadFromJsonAsync<PublicationEvaluationResponse>();
        Assert.NotNull(eval);
        Assert.True(eval.CanTransition);

        // 4. Update publication -> Published
        using var patchRes = await client.PatchAsJsonAsync($"/api/experiences/activities/{created.Id}/publication", pubReq);
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        var published = await patchRes.Content.ReadFromJsonAsync<ActivityDto>();
        Assert.NotNull(published);
        Assert.Equal(PublicationStatus.Published, published.Status);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-ACT-004")]
    public async Task UpdateActivity_UpdatesProperties_And_Returns_UpdatedDto()
    {
        using var client = _factory.CreateAdminClient();

        var createReq = new CreateActivityRequest(
            Code: $"ACT_{Guid.NewGuid():N}"[..15],
            Name: "Tidal Pool Walk",
            Description: "Walk along low-tide pools",
            Category: "Nature");

        using var createRes = await client.PostAsJsonAsync("/api/experiences/activities", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<ActivityDto>();
        Assert.NotNull(created);

        var updateReq = new UpdateActivityRequest("Guided Tidal Pool Exploration", "Extended 3-hour marine biologist walk", "Education");
        using var updateRes = await client.PutAsJsonAsync($"/api/experiences/activities/{created.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<ActivityDto>();
        Assert.NotNull(updated);
        Assert.Equal("Guided Tidal Pool Exploration", updated.Name);
        Assert.Equal("Extended 3-hour marine biologist walk", updated.Description);
        Assert.Equal("Education", updated.Category);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-ACT-005")]
    public async Task UpdateActivity_UnknownId_Returns_NotFound_ProblemDetails()
    {
        using var client = _factory.CreateAdminClient();
        var unknownId = Guid.NewGuid();

        var updateReq = new UpdateActivityRequest("Missing", "Missing", "None");
        using var response = await client.PutAsJsonAsync($"/api/experiences/activities/{unknownId}", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Activity Not Found", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-ACT-006")]
    public async Task GetAllActivities_WithCategoryFilter_ReturnsMatchingSubset()
    {
        using var client = _factory.CreateAdminClient();

        // 1. Create a uniquely-categorized activity and publish it
        var categoryName = $"CustomCat_{Guid.NewGuid():N}"[..14];
        var createReq = new CreateActivityRequest(
            Code: $"CAT_TEST_{Guid.NewGuid():N}"[..16],
            Name: "Custom Category Test Activity",
            Description: "Category filter test",
            Category: categoryName);

        using var createRes = await client.PostAsJsonAsync("/api/experiences/activities", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<ActivityDto>();
        Assert.NotNull(created);

        // Transition to published
        var pubReq = new UpdatePublicationRequest(PublicationStatus.Published);
        await client.PatchAsJsonAsync($"/api/experiences/activities/{created.Id}/publication", pubReq);

        // 2. Query with category filter
        using var filterRes = await client.GetAsync($"/api/experiences/activities?category={categoryName}");
        Assert.Equal(HttpStatusCode.OK, filterRes.StatusCode);

        var items = await filterRes.Content.ReadFromJsonAsync<List<ActivityDto>>();
        Assert.NotNull(items);
        Assert.Contains(items, a => a.Id == created.Id);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-ACT-007")]
    public async Task DeleteActivity_Existing_Returns_NoContent_And_Deletes()
    {
        using var client = _factory.CreateAdminClient();

        var createReq = new CreateActivityRequest(
            Code: $"ACT_DEL_{Guid.NewGuid():N}"[..18],
            Name: "Delete Target Activity",
            Description: "Activity to be deleted",
            Category: "DeleteTest");

        using var createRes = await client.PostAsJsonAsync("/api/experiences/activities", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ActivityDto>();
        Assert.NotNull(created);

        // Delete activity
        using var delRes = await client.DeleteAsync($"/api/experiences/activities/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // Subsequent lookup returns 404
        using var getRes = await client.GetAsync($"/api/experiences/activities/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-ACT-008")]
    public async Task DeleteActivity_UnknownId_Returns_NotFound()
    {
        using var client = _factory.CreateAdminClient();
        var unknownId = Guid.NewGuid();

        using var delRes = await client.DeleteAsync($"/api/experiences/activities/{unknownId}");
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }
}
