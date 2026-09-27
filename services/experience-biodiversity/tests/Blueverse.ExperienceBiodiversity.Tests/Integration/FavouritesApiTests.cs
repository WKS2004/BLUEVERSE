using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class FavouritesApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public FavouritesApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-FAV-001")]
    public async Task GetFavourites_Without_Auth_Returns_Unauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/favourites");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-FAV-002")]
    public async Task Add_And_Remove_Favourite_With_User_Context()
    {
        using var client = _factory.CreateClient();

        var userId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());

        // 1. Create a destination to favourite
        var createReq = new CreateDestinationRequest("Mirissa Secret Bay", null, "Scenic bay", "Southern Province", 5.94, 80.46);
        using var destRes = await client.PostAsJsonAsync("/api/experiences/destinations", createReq);
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(dest);

        // 2. Add favourite
        using var addRes = await client.PutAsync($"/api/experiences/favourites/Destination/{dest.Id}", null);
        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);

        // 3. List favourites
        using var listRes = await client.GetAsync("/api/experiences/favourites");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var favs = await listRes.Content.ReadFromJsonAsync<List<FavouriteDto>>();
        Assert.NotNull(favs);
        Assert.Contains(favs, f => f.TargetId == dest.Id);

        // 4. Delete favourite
        using var delRes = await client.DeleteAsync($"/api/experiences/favourites/Destination/{dest.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-FAV-003")]
    public async Task AddFavourite_InvalidTargetType_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());

        using var response = await client.PutAsync($"/api/experiences/favourites/InvalidType/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Target Type", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-FAV-004")]
    public async Task AddFavourite_NonExistentTargetId_Returns_NotFound()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());

        using var response = await client.PutAsync($"/api/experiences/favourites/Destination/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Target Item Not Found", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-FAV-005")]
    public async Task RemoveFavourite_NonExistentTarget_Returns_NoContent()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());

        using var response = await client.DeleteAsync($"/api/experiences/favourites/Destination/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-FAV-006")]
    public async Task RemoveFavourite_InvalidTargetType_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());

        using var response = await client.DeleteAsync($"/api/experiences/favourites/UnknownKind/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Target Type", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-FAV-007")]
    public async Task AddFavourite_ForActivityAndOffering_Succeeds()
    {
        using var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());

        // 1. Create activity
        var actRes = await client.PostAsJsonAsync("/api/experiences/activities",
            new CreateActivityRequest($"FAV_A_{Guid.NewGuid():N}"[..15], "Fav Activity", "Desc", "Sport"));
        var act = await actRes.Content.ReadFromJsonAsync<ActivityDto>();

        // 2. Create destination and offering
        var destRes = await client.PostAsJsonAsync("/api/experiences/destinations",
            new CreateDestinationRequest("Fav Bay", null, "Desc", "South", 5.9, 80.4));
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();

        var offRes = await client.PostAsJsonAsync("/api/experiences/offerings",
            new CreateOfferingRequest(dest!.Id, act!.Id, "Fav Offering", "Desc", 50m, "USD", 60, 4));
        var off = await offRes.Content.ReadFromJsonAsync<OfferingDto>();

        // 3. Favourite both
        using var favActRes = await client.PutAsync($"/api/experiences/favourites/Activity/{act.Id}", null);
        Assert.Equal(HttpStatusCode.OK, favActRes.StatusCode);

        using var favOffRes = await client.PutAsync($"/api/experiences/favourites/Offering/{off!.Id}", null);
        Assert.Equal(HttpStatusCode.OK, favOffRes.StatusCode);

        // 4. Retrieve list
        using var listRes = await client.GetAsync("/api/experiences/favourites");
        var favs = await listRes.Content.ReadFromJsonAsync<List<FavouriteDto>>();
        Assert.NotNull(favs);
        Assert.Contains(favs, f => f.TargetId == act.Id && f.TargetType == "Activity");
        Assert.Contains(favs, f => f.TargetId == off.Id && f.TargetType == "Offering");
    }
}
