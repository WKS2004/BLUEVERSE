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

    private static HttpRequestMessage CookieAuthorized(string cookie)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static string CreateSignedToken(IEnumerable<Claim> claims)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "Blueverse.Auth",
            Audience = "Blueverse.Client",
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MarineSafetyWebApplicationFactory.JwtSigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
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
    public async Task M2_AUTH_005_tokens_without_a_subject_are_rejected_unauthorized_before_permission_resolution()
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

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Resolver.CallCount);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-006")]
    public async Task M2_AUTH_006_selected_account_cookie_takes_precedence_over_a_stale_legacy_cookie()
    {
        using var client = await CreateClientAsync();
        var selectedToken = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);
        var staleLegacyToken = _factory.CreateToken(MarineSafetyTestSeed.UnauthorizedUserId);

        using var request = CookieAuthorized(
            $"blueverse_active_account_id={MarineSafetyTestSeed.ReaderUserId:D}; " +
            $"blueverse_access_token_{MarineSafetyTestSeed.ReaderUserId:N}={selectedToken}; " +
            $"blueverse_access_token={staleLegacyToken}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-007")]
    public async Task M2_AUTH_007_selected_account_accepts_a_matching_legacy_cookie_when_its_account_cookie_is_absent()
    {
        using var client = await CreateClientAsync();
        var selectedToken = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);

        using var request = CookieAuthorized(
            $"blueverse_active_account_id={MarineSafetyTestSeed.ReaderUserId:D}; " +
            $"blueverse_access_token={selectedToken}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-008")]
    public async Task M2_AUTH_008_selected_account_rejects_a_legacy_cookie_for_another_account()
    {
        using var client = await CreateClientAsync();
        var staleLegacyToken = _factory.CreateToken(MarineSafetyTestSeed.UnauthorizedUserId);

        using var request = CookieAuthorized(
            $"blueverse_active_account_id={MarineSafetyTestSeed.ReaderUserId:D}; " +
            $"blueverse_access_token={staleLegacyToken}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-009")]
    public async Task M2_AUTH_009_malformed_account_selection_does_not_fall_back_to_the_legacy_cookie()
    {
        using var client = await CreateClientAsync();
        var legacyToken = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);

        using var request = CookieAuthorized(
            $"blueverse_active_account_id=not-a-guid; blueverse_access_token={legacyToken}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-010")]
    public async Task M2_AUTH_010_legacy_cookie_remains_supported_when_no_account_is_selected()
    {
        using var client = await CreateClientAsync();
        var legacyToken = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);

        using var request = CookieAuthorized($"blueverse_access_token={legacyToken}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-011")]
    public async Task M2_AUTH_011_bearer_header_takes_precedence_over_account_cookies()
    {
        using var client = await CreateClientAsync();
        var selectedCookieToken = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);
        var deniedBearerToken = _factory.CreateToken(MarineSafetyTestSeed.UnauthorizedUserId);
        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            deniedBearerToken);
        request.Headers.TryAddWithoutValidation(
            "Cookie",
            $"blueverse_active_account_id={MarineSafetyTestSeed.ReaderUserId:D}; " +
            $"blueverse_access_token_{MarineSafetyTestSeed.ReaderUserId:N}={selectedCookieToken}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-012")]
    public async Task M2_AUTH_012_invalid_bearer_header_does_not_fall_back_to_a_valid_cookie()
    {
        using var client = await CreateClientAsync();
        var selectedCookieToken = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);
        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            "not-a-valid-token");
        request.Headers.TryAddWithoutValidation(
            "Cookie",
            $"blueverse_active_account_id={MarineSafetyTestSeed.ReaderUserId:D}; " +
            $"blueverse_access_token_{MarineSafetyTestSeed.ReaderUserId:N}={selectedCookieToken}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-013")]
    public async Task M2_AUTH_013_missing_or_malformed_identity_session_claims_are_unauthorized()
    {
        using var client = await CreateClientAsync();
        var userId = MarineSafetyTestSeed.ReaderUserId;
        var sessionId = Guid.NewGuid();
        var complete = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("token_version", "0"),
            new Claim("session_id", sessionId.ToString()),
            new Claim("session_version", "1")
        };
        var cases = new (string Name, Claim[] Claims)[]
        {
            ("missing token_version", complete.Where(claim => claim.Type != "token_version").ToArray()),
            ("malformed token_version", complete.Select(claim => claim.Type == "token_version" ? new Claim(claim.Type, "invalid") : claim).ToArray()),
            ("missing session_id", complete.Where(claim => claim.Type != "session_id").ToArray()),
            ("malformed session_id", complete.Select(claim => claim.Type == "session_id" ? new Claim(claim.Type, "invalid") : claim).ToArray()),
            ("missing session_version", complete.Where(claim => claim.Type != "session_version").ToArray()),
            ("malformed session_version", complete.Select(claim => claim.Type == "session_version" ? new Claim(claim.Type, "invalid") : claim).ToArray())
        };

        foreach (var (name, claims) in cases)
        {
            using var request = Authorized(
                HttpMethod.Get,
                "/api/marine/current?latitude=6.025&longitude=80.216",
                CreateSignedToken(claims));
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        Assert.Equal(0, _factory.Resolver.CallCount);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-014")]
    public async Task M2_AUTH_014_inactive_accounts_are_rejected_before_permission_resolution()
    {
        using var client = await CreateClientAsync();
        var userId = MarineSafetyTestSeed.ReaderUserId;
        var token = _factory.CreateToken(userId);
        _factory.IdentityValidator.SetUserActive(userId, false);

        using var request = Authorized(HttpMethod.Get, "/api/marine/current?latitude=6.025&longitude=80.216", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Resolver.CallCount);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-015")]
    public async Task M2_AUTH_015_tokens_with_a_stale_user_token_version_are_rejected()
    {
        using var client = await CreateClientAsync();
        var userId = MarineSafetyTestSeed.ReaderUserId;
        var token = _factory.CreateToken(userId, tokenVersion: 0);
        _factory.IdentityValidator.SetTokenVersion(userId, 1);

        using var request = Authorized(HttpMethod.Get, "/api/marine/current?latitude=6.025&longitude=80.216", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Resolver.CallCount);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-016")]
    public async Task M2_AUTH_016_revoked_sessions_are_rejected()
    {
        using var client = await CreateClientAsync();
        var sessionId = Guid.NewGuid();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId, sessionId: sessionId);
        _factory.IdentityValidator.RevokeSession(sessionId);

        using var request = Authorized(HttpMethod.Get, "/api/marine/current?latitude=6.025&longitude=80.216", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Resolver.CallCount);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-017")]
    public async Task M2_AUTH_017_tokens_with_a_stale_session_version_are_rejected()
    {
        using var client = await CreateClientAsync();
        var sessionId = Guid.NewGuid();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId, sessionId: sessionId, sessionVersion: 1);
        _factory.IdentityValidator.SetSessionVersion(sessionId, 2);

        using var request = Authorized(HttpMethod.Get, "/api/marine/current?latitude=6.025&longitude=80.216", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Resolver.CallCount);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-018")]
    public async Task M2_AUTH_018_expired_server_sessions_are_rejected_even_when_the_jwt_is_current()
    {
        using var client = await CreateClientAsync();
        var sessionId = Guid.NewGuid();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId, sessionId: sessionId);
        _factory.IdentityValidator.SetSessionExpiry(sessionId, DateTime.UtcNow.AddMinutes(-1));

        using var request = Authorized(HttpMethod.Get, "/api/marine/current?latitude=6.025&longitude=80.216", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Resolver.CallCount);
    }

    [Fact]
    [Trait("CaseId", "M2-AUTH-019")]
    public async Task M2_AUTH_019_a_session_bound_to_another_user_cannot_authenticate_the_token_subject()
    {
        using var client = await CreateClientAsync();
        var sessionId = Guid.NewGuid();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId, sessionId: sessionId);
        _factory.IdentityValidator.SetSessionOwner(sessionId, MarineSafetyTestSeed.ManagerUserId);

        using var request = Authorized(HttpMethod.Get, "/api/marine/current?latitude=6.025&longitude=80.216", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Resolver.CallCount);
    }
}
