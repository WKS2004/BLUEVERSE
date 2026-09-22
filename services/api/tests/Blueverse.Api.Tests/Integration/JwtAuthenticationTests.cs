using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Blueverse.Api.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.Api.Tests.Integration;

public sealed class JwtAuthenticationTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string SigningKey = "api-test-only-signing-key-with-at-least-32-bytes";
    private readonly HttpClient _client;

    public JwtAuthenticationTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("TestId", "API-AUTH-001")]
    public async Task GatewayRejectsMissingAndInvalidTokensAndAcceptsAValidAuthToken()
    {
        var anonymous = await _client.GetAsync("/api/test-only/auth");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test-only/auth");
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("wrong-key-with-at-least-32-bytes-123456"));
        var invalid = await _client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        using var validRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test-only/auth");
        validRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(SigningKey));
        var valid = await _client.SendAsync(validRequest);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var body = await valid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("authenticated").GetBoolean());
    }

    private static string CreateToken(string signingKey)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())]),
            Issuer = "Blueverse.Auth",
            Audience = "Blueverse.Client",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
