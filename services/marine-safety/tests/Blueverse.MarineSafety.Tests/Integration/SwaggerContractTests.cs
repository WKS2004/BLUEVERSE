using System.Net.Http.Json;
using System.Text.Json;

namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Contract-surface tests (M2-SWAG-*): the OpenAPI document must enumerate
/// every operation the endpoint catalog registers for the marine service, with
/// the bearer security scheme applied — this is the contract both clients and
/// the gateway aggregation bind to.
/// </summary>
public sealed class SwaggerContractTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private static readonly (string Method, string Path)[] DocumentedOperations =
    [
        ("get", "/api/marine/current"),
        ("get", "/api/marine/snapshots/{id}"),
        ("get", "/api/marine/history"),
        ("post", "/api/marine/evaluate"),
        ("get", "/api/marine/assessments"),
        ("get", "/api/marine/assessments/{id}"),
        ("get", "/api/marine/activities"),
        ("post", "/api/marine/activities"),
        ("get", "/api/marine/activities/{id}"),
        ("put", "/api/marine/activities/{id}"),
        ("delete", "/api/marine/activities/{id}"),
        ("get", "/api/marine/safety-profiles"),
        ("post", "/api/marine/safety-profiles"),
        ("get", "/api/marine/safety-profiles/{id}"),
        ("put", "/api/marine/safety-profiles/{id}"),
        ("delete", "/api/marine/safety-profiles/{id}"),
        ("get", "/api/marine/safety-profiles/by-activity/{activityId}"),
        ("get", "/api/marine/health")
    ];

    private readonly MarineSafetyWebApplicationFactory _factory;

    public SwaggerContractTests(MarineSafetyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "M2-SWAG-001")]
    public async Task M2_SWAG_001_openapi_document_enumerates_the_whole_contract()
    {
        _factory.ResetDatabase();
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/marine/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("BLUEVERSE Marine Conditions & Safety API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.True(
            root.GetProperty("openapi").GetString()?.StartsWith("3.0", StringComparison.Ordinal) == true,
            "The document must be OpenAPI 3.0.x for the gateway aggregation and client generators.");

        var paths = root.GetProperty("paths");
        foreach (var (method, path) in DocumentedOperations)
        {
            Assert.True(
                paths.TryGetProperty(path, out var pathItem) &&
                pathItem.TryGetProperty(method, out _),
                $"OpenAPI document is missing operation {method.ToUpperInvariant()} {path}");
        }

        // Every documented operation (except anonymous health and the swagger
        // surface itself) requires the bearer scheme; the scheme is defined.
        Assert.True(root.TryGetProperty("components", out var components) &&
                    components.TryGetProperty("securitySchemes", out var schemes) &&
                    schemes.TryGetProperty("Bearer", out _),
            "The bearer security scheme must be defined for clients to bind to.");

        var healthOperation = paths.GetProperty("/api/marine/health").GetProperty("get");
        Assert.False(
            healthOperation.TryGetProperty("security", out _) &&
            healthOperation.GetProperty("security").GetArrayLength() > 0,
            "Health must remain anonymous (no security requirement).");
    }
}
