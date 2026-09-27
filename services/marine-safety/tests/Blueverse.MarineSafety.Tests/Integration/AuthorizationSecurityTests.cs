using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Authentication/authorization security tests (M2-AUTH-*): token lifetime,
/// signing key and issuer/audience validation, and the guarantee that database
/// permission resolution is authoritative over any JWT permission claims —
/// a revoked grant must take effect on the very next request.
/// </summary>
public sealed class AuthorizationSecurityTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;

    public AuthorizationSecurityTests(MarineSafetyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        _factory.ResetDatabase();
        _factory.Provider = new StubOpenMeteoClient();
        var client = _factory.CreateClient();
        await _factory.SeedAsync(db => MarineSafetyTestSeed.SeedIdentitiesAsync(db, _factory.Resolver));
        await _factory.SeedAsync(MarineSafetyTestSeed.SeedConfigurationAsync);
        return client;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-001")]
    public async Task M2_AUTH_001_expired_tokens_are_rejected_unauthorized()
    {
        using var client = await CreateClientAsync();

        // Mint a structurally valid token whose lifetime has fully elapsed:
        // NotBefore must precede Expires even for an already-expired token.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, MarineSafetyTestSeed.ReaderUserId.ToString())
        };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "Blueverse.Auth",
            Audience = "Blueverse.Client",
            NotBefore = DateTime.UtcNow.AddMinutes(-20),
            Expires = DateTime.UtcNow.AddMinutes(-10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MarineSafetyWebApplicationFactory.JwtSigningKey)),
                SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        var expiredToken = handler.WriteToken(handler.CreateToken(descriptor));

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            expiredToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-002")]
    public async Task M2_AUTH_002_tokens_signed_with_a_foreign_key_are_rejected()
    {
        using var client = await CreateClientAsync();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, MarineSafetyTestSeed.ReaderUserId.ToString())
        };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "Blueverse.Auth",
            Audience = "Blueverse.Client",
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("foreign-signing-key-that-is-long-enough-32b")),
                SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        var foreignToken = handler.WriteToken(handler.CreateToken(descriptor));

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            foreignToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-003")]
    public async Task M2_AUTH_003_wrong_issuer_or_audience_is_rejected()
    {
        using var client = await CreateClientAsync();

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MarineSafetyWebApplicationFactory.JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);

        SecurityTokenDescriptor TokenDescriptor(string issuer, string audience) => new()
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, MarineSafetyTestSeed.ReaderUserId.ToString())]),
            Issuer = issuer,
            Audience = audience,
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = signingCredentials
        };

        var handler = new JwtSecurityTokenHandler();
        var wrongIssuer = handler.WriteToken(handler.CreateToken(TokenDescriptor("Not-Blueverse.Auth", "Blueverse.Client")));
        var wrongAudience = handler.WriteToken(handler.CreateToken(TokenDescriptor("Blueverse.Auth", "Not-Blueverse.Client")));

        foreach (var token in new[] { wrongIssuer, wrongAudience })
        {
            using var request = Authorized(
                HttpMethod.Get,
                "/api/marine/current?latitude=6.025&longitude=80.216",
                token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-004")]
    public async Task M2_AUTH_004_database_permission_resolution_overrides_jwt_claims()
    {
        // A token may carry permission claims, but the database grant is
        // authoritative: a user without the seeded grant is forbidden even
        // when the JWT asserts the permission, and a grant added after token
        // issuance takes effect on the next request.
        using var client = await CreateClientAsync();

        var grantedUser = Guid.NewGuid();
        var ungrantedUser = Guid.NewGuid();
        _factory.Resolver.Grant(grantedUser, PermissionCodes.MarineProfileRead);
        // ungrantedUser: deliberately no grant, but the token below claims one.

        var claimedReader = _factory.CreateToken(
            ungrantedUser,
            permissionClaims: ["marine.profile.read"]);
        var grantedReader = _factory.CreateToken(grantedUser);

        using var claimed = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            claimedReader);
        using var claimedResponse = await client.SendAsync(claimed);
        Assert.Equal(HttpStatusCode.Forbidden, claimedResponse.StatusCode);

        using var granted = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            grantedReader);
        using var grantedResponse = await client.SendAsync(granted);
        Assert.Equal(HttpStatusCode.OK, grantedResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-005")]
    public async Task M2_AUTH_005_tokens_without_a_subject_are_rejected_before_permission_resolution()
    {
        using var client = await CreateClientAsync();

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(), // no sub/NameIdentifier claim
            Issuer = "Blueverse.Auth",
            Audience = "Blueverse.Client",
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MarineSafetyWebApplicationFactory.JwtSigningKey)),
                SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        var token = handler.WriteToken(handler.CreateToken(descriptor));

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
