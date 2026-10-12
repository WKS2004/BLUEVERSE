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
        using var client = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        using var response = await client.GetAsync("/api/experiences/agent/context");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var seam = await response.Content.ReadFromJsonAsync<AgentContextResponseDto>();
        Assert.NotNull(seam);
        Assert.Equal("not_connected", seam.Status);
        Assert.Contains("destination_lookup", seam.PlannedTools);
        Assert.Contains("biodiversity_prediction_lookup", seam.PlannedTools);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AGENT-002")]
    public async Task GetExperienceAgentInfo_Requires_CatalogueRead_And_Returns_AgentMetadata()
    {
        using var anonClient = _factory.CreateAnonymousClient();
        using var anonResponse = await anonClient.GetAsync("/api/experiences/agent/info");
        Assert.Equal(HttpStatusCode.Unauthorized, anonResponse.StatusCode);

        using var forbiddenClient = _factory.CreateAuthenticatedClient(Guid.NewGuid());
        using var forbiddenResponse = await forbiddenClient.GetAsync("/api/experiences/agent/info");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        using var authorizedClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), "experiences.catalogue.read");
        using var okResponse = await authorizedClient.GetAsync("/api/experiences/agent/info");
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

        var info = await okResponse.Content.ReadFromJsonAsync<ExperienceAgentInfoResponseDto>();
        Assert.NotNull(info);
        Assert.Equal("v1.agent.experience-biodiversity", info.AgentId);
        Assert.Equal("/api/experiences/agent", info.EndpointPrefix);
        Assert.Equal("google-ai-studio", info.ModelProvider);
        Assert.Equal("gemini-2.5-flash", info.ModelName);
        Assert.True(info.ReadOnly);
        Assert.Contains("destination_lookup", info.Tools);
        Assert.Contains("biodiversity_prediction_lookup", info.Tools);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AGENT-003")]
    public async Task GenerateExperienceAgentContextReport_Validates_Input_And_Returns_Safe_Fallback()
    {
        using var anonClient = _factory.CreateAnonymousClient();
        using var anonResponse = await anonClient.PostAsJsonAsync(
            "/api/experiences/agent/context-reports",
            new ExperienceAgentContextRequestDto(Guid.NewGuid(), "step-1", "Valid objective", DestinationId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, anonResponse.StatusCode);

        using var forbiddenClient = _factory.CreateAuthenticatedClient(Guid.NewGuid());
        using var forbiddenResponse = await forbiddenClient.PostAsJsonAsync(
            "/api/experiences/agent/context-reports",
            new ExperienceAgentContextRequestDto(Guid.NewGuid(), "step-1", "Valid objective", DestinationId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        using var client = _factory.CreateAuthenticatedClient(Guid.NewGuid(), "experiences.catalogue.read");

        // 1. Missing all selectors -> 400 Bad Request
        using var missingSelectorsResp = await client.PostAsJsonAsync(
            "/api/experiences/agent/context-reports",
            new ExperienceAgentContextRequestDto(Guid.NewGuid(), "step-1", "Assess Mirissa destination"));
        Assert.Equal(HttpStatusCode.BadRequest, missingSelectorsResp.StatusCode);

        // 2. Empty WorkflowId -> 400 Bad Request
        using var emptyWorkflowResp = await client.PostAsJsonAsync(
            "/api/experiences/agent/context-reports",
            new ExperienceAgentContextRequestDto(Guid.Empty, "step-1", "Assess Mirissa destination", DestinationId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, emptyWorkflowResp.StatusCode);

        // 3. Invalid time interval (PeriodEndsAt <= PeriodStartsAt) -> 400 Bad Request
        using var badIntervalResp = await client.PostAsJsonAsync(
            "/api/experiences/agent/context-reports",
            new ExperienceAgentContextRequestDto(
                Guid.NewGuid(),
                "step-1",
                "Assess Mirissa destination",
                OfferingId: Guid.NewGuid(),
                PeriodStartsAt: DateTimeOffset.Parse("2026-10-15T12:00:00Z"),
                PeriodEndsAt: DateTimeOffset.Parse("2026-10-15T10:00:00Z")));
        Assert.Equal(HttpStatusCode.BadRequest, badIntervalResp.StatusCode);

        // 4. Valid request when agent container is offline -> 200 OK with safe not_connected envelope
        var workflowId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        using var validResp = await client.PostAsJsonAsync(
            "/api/experiences/agent/context-reports",
            new ExperienceAgentContextRequestDto(
                workflowId,
                "step-planning-01",
                "Evaluate Mirissa Beach snorkeling and biodiversity context",
                DestinationId: destinationId,
                IncludeBiodiversity: true));
        Assert.Equal(HttpStatusCode.OK, validResp.StatusCode);

        var envelope = await validResp.Content.ReadFromJsonAsync<ExperienceAgentContextReportEnvelopeDto>();
        Assert.NotNull(envelope);
        Assert.Equal("v1.agent.experience-biodiversity", envelope.AgentId);
        Assert.Equal("not_connected", envelope.Status);
        Assert.Equal(workflowId, envelope.WorkflowId);
        Assert.NotNull(envelope.ModelExecution);
        Assert.Equal("google-ai-studio", envelope.ModelExecution.Provider);
        Assert.Equal("gemini-2.5-flash", envelope.ModelExecution.Model);
    }
}
