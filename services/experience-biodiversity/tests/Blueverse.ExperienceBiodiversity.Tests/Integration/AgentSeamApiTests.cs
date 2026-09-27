using System.Net;
using System.Net.Http.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class AgentSeamApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AgentSeamApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AGENT-001")]
    public async Task GetAgentContext_Returns_NotConnected_Status_Pre_G07()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/agent/context");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var seam = await response.Content.ReadFromJsonAsync<AgentContextResponseDto>();
        Assert.NotNull(seam);
        Assert.Equal("not_connected", seam.Status);
        Assert.Contains("destination_lookup", seam.PlannedTools);
        Assert.Contains("biodiversity_prediction_lookup", seam.PlannedTools);
    }
}
