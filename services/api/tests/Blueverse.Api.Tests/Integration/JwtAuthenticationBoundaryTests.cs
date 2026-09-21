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

public sealed class JwtAuthenticationBoundaryTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string SigningKey = "api-test-only-signing-key-with-at-least-32-bytes";
    private readonly HttpClient _client;

    public JwtAuthenticationBoundaryTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("TestId", "API-AUTH-BOUNDARY-001")]
    public async Task GatewayRejectsTokensWithInvalidIssuerAudienceLifetimeOrAlgorithm()
    {
        var invalidTokens = new[]
        {
            CreateToken(SigningKey, issuer: "Other.Issuer", audience: "Blueverse.Client"),
            CreateToken(SigningKey, issuer: "Blueverse.Auth", audience: "Other.Audience"),
            CreateToken(SigningKey, expiresAt: DateTime.UtcNow.AddMinutes(-5)),
            CreateToken(new string('x', 64), algorithm: SecurityAlgorithms.HmacSha512)
        };

        foreach (var token in invalidTokens)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-only/auth");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain("Other.Issuer", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("TestId", "API-AUTH-BOUNDARY-002")]
    public async Task GatewayAcceptsAnAccessTokenFromTheProtectedCookieWhenBearerIsAbsent()
    {
        var token = CreateToken(SigningKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-only/auth");
        request.Headers.TryAddWithoutValidation("Cookie", $"blueverse_access_token={token}");

        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("authenticated").GetBoolean());
    }

    [Fact]
    [Trait("TestId", "API-AUTH-BOUNDARY-003")]
    public async Task GatewayRejectsMalformedBearerAndCookieTokensWithoutLeakingValidationDetails()
    {
        foreach (var header in new[]
        {
            (Name: "Authorization", Value: "Bearer not-a-jwt"),
            (Name: "Cookie", Value: "blueverse_access_token=not-a-jwt")
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-only/auth");
            request.Headers.TryAddWithoutValidation(header.Name, header.Value);
            using var response = await _client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain("signing key", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("issuer", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string CreateToken(
        string signingKey,
        string issuer = "Blueverse.Auth",
        string audience = "Blueverse.Client",
        DateTime? expiresAt = null,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var expires = expiresAt ?? DateTime.UtcNow.AddMinutes(5);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())]),
            Issuer = issuer,
            Audience = audience,
            NotBefore = expires.AddMinutes(-10),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                algorithm)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
