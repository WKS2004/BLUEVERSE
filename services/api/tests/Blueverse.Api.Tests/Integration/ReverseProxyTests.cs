using System.Net;
using System.Text.Json;
using Blueverse.Api.Tests.Fixtures;

namespace Blueverse.Api.Tests.Integration;

public sealed class ReverseProxyTests
{
    [Fact]
    [Trait("CaseId", "API-PROXY-001")]
    public async Task API_PROXY_001_auth_requests_are_forwarded_with_method_path_query_body_and_correlation()
    {
        await using var destination = await StubDestinationServer.StartAsync();
        var overrides = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:auth:Destinations:auth:Address"] = destination.Address
        };

        using var factory = new ApiWebApplicationFactory(overrides);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login?source=synthetic")
        {
            Content = new StringContent("{\"email\":\"synthetic@example.test\"}")
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "correlation-123");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("POST", root.GetProperty("method").GetString());
        Assert.Equal("/api/auth/login", root.GetProperty("path").GetString());
        Assert.Equal("?source=synthetic", root.GetProperty("query").GetString());
        Assert.Equal("{\"email\":\"synthetic@example.test\"}", root.GetProperty("body").GetString());
        Assert.Equal("correlation-123", root.GetProperty("correlationId").GetString());
    }

    [Fact]
    [Trait("CaseId", "API-PROXY-002")]
    public async Task API_PROXY_002_unavailable_auth_destination_returns_bad_gateway_without_destination_details()
    {
        await using var destination = await StubDestinationServer.StartAsync();
        var unavailableAddress = destination.Address;
        await destination.DisposeAsync();

        var overrides = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:auth:Destinations:auth:Address"] = unavailableAddress
        };

        using var factory = new ApiWebApplicationFactory(overrides);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.DoesNotContain(unavailableAddress, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", body, StringComparison.OrdinalIgnoreCase);
    }
}
