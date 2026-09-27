using System.Net;
using System.Net.Http.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class MicroserviceResilienceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MicroserviceResilienceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-RESIL-API-001")]
    public async Task MarineConditions_Returns_200_With_Safe_Fallback_When_Marine_Service_Absent()
    {
        using var client = _factory.CreateClient();

        // 1. Create a destination
        var createReq = new CreateDestinationRequest("Mirissa Bay", null, "Whale watching center", "Southern Province", 5.94, 80.45);
        using var destRes = await client.PostAsJsonAsync("/api/experiences/destinations", createReq);
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(dest);

        // 2. Query marine conditions: Member 2 is absent in test environment
        using var response = await client.GetAsync($"/api/experiences/destinations/{dest.Id}/marine-conditions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<MarineConditionsContextDto>();
        Assert.NotNull(result);
        Assert.Equal(dest.Id, result.DestinationId);
        Assert.False(result.Responded); // Explicitly mentions remote endpoint did not respond
        Assert.True(result.FallbackUsed);
        Assert.Equal("UNKNOWN", result.SafetyLevel);
        Assert.Equal("UNKNOWN", result.WaterCondition);
        Assert.Contains("did not respond", result.AdvisoryMessage);
        Assert.NotNull(result.TargetEndpoint);
    }

    [Fact]
    [Trait("CaseId", "EXP-RESIL-API-002")]
    public async Task OperationalAdvisories_Returns_200_With_Safe_Fallback_When_Operations_Service_Absent()
    {
        using var client = _factory.CreateClient();

        var createReq = new CreateDestinationRequest("Hikkaduwa Sanctuary", null, "Coral reef sanctuary", "Southern Province", 6.13, 80.10);
        using var destRes = await client.PostAsJsonAsync("/api/experiences/destinations", createReq);
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(dest);

        // Member 4 is absent in test environment
        using var response = await client.GetAsync($"/api/experiences/destinations/{dest.Id}/operational-advisories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OperationalAdvisoriesResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(dest.Id, result.DestinationId);
        Assert.False(result.Responded); // Explicitly mentions remote endpoint did not respond
        Assert.True(result.FallbackUsed);
        Assert.Empty(result.Advisories);
        Assert.Contains("did not respond", result.Message);
    }

    [Fact]
    [Trait("CaseId", "EXP-RESIL-API-003")]
    public async Task DependenciesStatus_Reports_Microservice_Resilience_Without_Crashing()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/dependencies/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<MicroserviceDependenciesStatusDto>();
        Assert.NotNull(result);
        Assert.Equal("Blueverse.ExperienceBiodiversity", result.Microservice);
        Assert.Contains("HEALTHY", result.OverallStatus);
        Assert.NotEmpty(result.Dependencies);

        // Verifies all peer microservice keys are inspected
        Assert.Contains(result.Dependencies, d => d.ServiceKey == "marine-safety");
        Assert.Contains(result.Dependencies, d => d.ServiceKey == "coastal-planner");
        Assert.Contains(result.Dependencies, d => d.ServiceKey == "coastal-operations");
        Assert.Contains(result.Dependencies, d => d.ServiceKey == "map-provider");

        // Confirms each dependency records whether it responded
        foreach (var dep in result.Dependencies)
        {
            Assert.NotNull(dep.TargetEndpoint);
            Assert.NotNull(dep.Status);
            Assert.NotNull(dep.Message);
        }

        Assert.Contains("independent microservice", result.ResilienceNote);
    }
}
