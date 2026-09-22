using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.Api.Tests.Fixtures;

namespace Blueverse.Api.Tests.Integration;

public sealed class SwaggerAuthBoundaryTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public SwaggerAuthBoundaryTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("TestId", "API-CONTRACT-005")]
    public async Task PublicSwaggerUiIsAvailableAndAuthSwaggerIsRoutedThroughTheGateway()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/swagger");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("swagger-ui", body, StringComparison.OrdinalIgnoreCase);

        await using var destination = await StubDestinationServer.StartAsync();
        var overrides = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:auth:Destinations:auth:Address"] = destination.Address
        };
        using var routedFactory = new ApiWebApplicationFactory(overrides);
        using var routedClient = routedFactory.CreateClient();
        using var authDocumentResponse = await routedClient.GetAsync("/api/auth/swagger/v1/swagger.json");
        var authDocumentBody = await authDocumentResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, authDocumentResponse.StatusCode);
        Assert.Equal("GET", authDocumentBody.GetProperty("method").GetString());
        Assert.Equal("/api/auth/swagger/v1/swagger.json", authDocumentBody.GetProperty("path").GetString());
    }
}
