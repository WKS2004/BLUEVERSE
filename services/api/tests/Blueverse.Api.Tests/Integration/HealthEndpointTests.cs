using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.Api.Tests.Fixtures;

namespace Blueverse.Api.Tests.Integration;

public sealed class HealthEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public HealthEndpointTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "API-HEALTH-001")]
    public async Task API_HEALTH_001_get_returns_the_complete_liveness_response()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal("api", root.GetProperty("service").GetString());
        Assert.Equal("healthy", root.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("CaseId", "API-HEALTH-002")]
    public async Task API_HEALTH_002_unsupported_method_is_rejected_without_invoking_liveness()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/health")
        {
            Content = JsonContent.Create(new { synthetic = true })
        };

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.DoesNotContain("healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("CaseId", "API-CONTRACT-004")]
    public async Task API_CONTRACT_004_versioned_health_path_is_not_exposed()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "API-HEALTH-003")]
    public async Task API_HEALTH_003_liveness_remains_available_when_auth_destination_is_down()
    {
        var overrides = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:auth:Destinations:auth:Address"] = "http://127.0.0.1:1/"
        };

        using var factory = new ApiWebApplicationFactory(overrides);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"service\":\"api\"", body, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"healthy\"", body, StringComparison.Ordinal);
    }
}
