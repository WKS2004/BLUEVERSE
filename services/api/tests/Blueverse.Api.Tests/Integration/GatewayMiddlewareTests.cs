using System.Net;
using System.Text.Json;
using Blueverse.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Blueverse.Api.Tests.Integration;

public sealed class GatewayMiddlewareTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public GatewayMiddlewareTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "API-EDGE-001")]
    public async Task API_EDGE_001_forwarded_headers_update_the_request_context()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-only/request-context");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.42");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("https", document.RootElement.GetProperty("scheme").GetString());
        Assert.Equal("203.0.113.42", document.RootElement.GetProperty("remoteIp").GetString());
    }

    [Fact]
    [Trait("CaseId", "API-EDGE-002")]
    public void API_EDGE_002_forwarded_header_policy_accepts_the_configured_proxy_headers()
    {
        var options = _factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Empty(options.KnownProxies);
    }

    [Fact]
    [Trait("CaseId", "API-ERROR-001")]
    public async Task API_ERROR_001_unhandled_exception_returns_safe_problem_details()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/test-only/errors");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("https://tools.ietf.org/html/rfc7807", root.GetProperty("type").GetString());
        Assert.Equal("API Gateway Error", root.GetProperty("title").GetString());
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred at the API gateway.", root.GetProperty("detail").GetString());
        Assert.DoesNotContain("synthetic test exception", body, StringComparison.OrdinalIgnoreCase);
    }
}
