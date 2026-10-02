using System.Net;
using System.Net.Http.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class BiodiversityApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public BiodiversityApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-BIO-001")]
    public async Task GetBiodiversity_Returns_Safe_Unavailable_When_Inference_Service_Not_Configured()
    {
        using var client = _factory.CreateAuthenticatedClient(Guid.NewGuid(), "experiences.catalogue.manage");

        // 1. Create a destination
        var createReq = new CreateDestinationRequest("Nilaveli Marine Park", null, "Coral park", "Eastern Province", 8.68, 81.18);
        using var destRes = await client.PostAsJsonAsync("/api/experiences/destinations", createReq);
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(dest);

        // 2. Query biodiversity context (safe degradation test)
        using var response = await client.GetAsync($"/api/experiences/destinations/{dest.Id}/biodiversity");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bioContext = await response.Content.ReadFromJsonAsync<BiodiversityContextResponseDto>();
        Assert.NotNull(bioContext);
        Assert.Equal("unavailable", bioContext.Status);
        Assert.Empty(bioContext.Predictions);
        Assert.Contains("optional context", bioContext.UncertaintyNotes);
        Assert.Contains("informative", bioContext.Disclaimer);
    }
}
