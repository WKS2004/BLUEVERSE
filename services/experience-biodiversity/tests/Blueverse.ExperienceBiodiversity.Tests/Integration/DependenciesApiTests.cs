using System.Net;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class DependenciesApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DependenciesApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-DEP-001")]
    public async Task GetDependenciesStatus_Returns_Ok_With_DiagnosticsSummary()
    {
        using var client = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        using var response = await client.GetAsync("/api/experiences/dependencies/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.Equal("Blueverse.ExperienceBiodiversity", root.GetProperty("microservice").GetString());
        Assert.True(root.TryGetProperty("overallStatus", out _));
        Assert.True(root.TryGetProperty("dependencies", out var deps));
        Assert.Equal(JsonValueKind.Array, deps.ValueKind);
        Assert.True(deps.GetArrayLength() >= 3);
        Assert.True(root.TryGetProperty("resilienceNote", out _));
    }
}
