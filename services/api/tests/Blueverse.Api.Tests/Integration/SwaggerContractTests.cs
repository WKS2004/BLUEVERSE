using System.Net;
using System.Text.Json;
using Blueverse.Api.Tests.Fixtures;

namespace Blueverse.Api.Tests.Integration;

public sealed class SwaggerContractTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public SwaggerContractTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "API-CONTRACT-001")]
    public async Task API_CONTRACT_001_builtin_openapi_document_describes_the_health_route()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/swagger/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("openapi", out var openApiVersion));
        Assert.False(string.IsNullOrWhiteSpace(openApiVersion.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("info").GetProperty("title").GetString()));
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/health", out _));
    }

    [Fact]
    [Trait("CaseId", "API-CONTRACT-002")]
    public async Task API_CONTRACT_002_swashbuckle_document_contains_public_metadata_and_bearer_scheme()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("BLUEVERSE Public API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", root.GetProperty("info").GetProperty("version").GetString());
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/health", out _));

        var bearer = root
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
    }

    [Fact]
    [Trait("CaseId", "API-CONTRACT-003")]
    public async Task API_CONTRACT_003_swagger_ui_is_published_by_the_public_api()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/swagger");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("BLUEVERSE API Documentation", body, StringComparison.Ordinal);
        Assert.Contains("swagger-ui", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("swagger-ui.css", body, StringComparison.OrdinalIgnoreCase);
    }
}
