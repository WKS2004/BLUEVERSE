using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Blueverse.Api.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.Api.Tests.Integration;

public sealed class ReverseProxyTests
{
    private const string SigningKey = "api-test-only-signing-key-with-at-least-32-bytes";

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

    [Fact]
    [Trait("CaseId", "API-PROXY-003")]
    public async Task API_PROXY_003_experience_routes_enforce_public_read_and_catalogue_manage_policies()
    {
        await using var destination = await StubDestinationServer.StartAsync();
        var overrides = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:experience-biodiversity:Destinations:experience-biodiversity:Address"] = destination.Address
        };

        using var factory = new ApiWebApplicationFactory(overrides);
        using var client = factory.CreateClient();

        using var publicRead = await client.GetAsync("/api/experiences/destinations");
        Assert.Equal(HttpStatusCode.OK, publicRead.StatusCode);
        var publicReadBody = await publicRead.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(publicReadBody.GetProperty("authorizationHeaderForwarded").GetBoolean());

        using var anonymousMutation = await client.PostAsync(
            "/api/experiences/destinations",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMutation.StatusCode);

        using var readOnlyMutationRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/experiences/destinations")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        readOnlyMutationRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("experiences.catalogue.read"));
        using var readOnlyMutation = await client.SendAsync(readOnlyMutationRequest);
        Assert.Equal(HttpStatusCode.Forbidden, readOnlyMutation.StatusCode);

        using var manageMutationRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/experiences/destinations")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        manageMutationRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("experiences.catalogue.manage"));
        using var manageMutation = await client.SendAsync(manageMutationRequest);
        Assert.Equal(HttpStatusCode.OK, manageMutation.StatusCode);
        var manageMutationBody = await manageMutation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(manageMutationBody.GetProperty("authorizationHeaderForwarded").GetBoolean());
    }

    private static string CreateToken(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityToken(
            issuer: "Blueverse.Auth",
            audience: "Blueverse.Client",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
