using Blueverse.Auth.Tests.Fixtures;

namespace Blueverse.Auth.Tests.Integration;

public sealed class AuthSessionLifecycleTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuthSessionLifecycleTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _factory.SeedAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("TestId", "AUTH-SESSION-REFRESH-001")]
    public async Task ServerIssuedInstallationRefreshesWithRotationAndSupportsSessionManagement()
    {
        var email = $"session-{Guid.NewGuid():N}@blueverse.local";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "UserPassword-123!",
            fullName = "Session Lifecycle User",
            useCookies = false
        });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var initialToken = registered.GetProperty("token").GetString();
        var deviceId = registered.GetProperty("deviceId").GetString();
        var deviceKey = registered.GetProperty("deviceKey").GetString();
        var initialRefreshToken = registered.GetProperty("refreshToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(initialToken));
        Assert.False(string.IsNullOrWhiteSpace(deviceId));
        Assert.False(string.IsNullOrWhiteSpace(deviceKey));
        Assert.False(string.IsNullOrWhiteSpace(initialRefreshToken));
        Assert.Equal(JsonValueKind.String, registered.GetProperty("sessionExpiresAt").ValueKind);

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = initialRefreshToken,
            deviceId,
            deviceKey,
            useCookies = false
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshedToken = refreshed.GetProperty("token").GetString();
        var replacementRefreshToken = refreshed.GetProperty("refreshToken").GetString();
        Assert.NotEqual(initialToken, refreshedToken);
        Assert.NotEqual(initialRefreshToken, replacementRefreshToken);
        Assert.Equal(deviceId, refreshed.GetProperty("deviceId").GetString());
        Assert.Equal(email, refreshed.GetProperty("user").GetProperty("email").GetString());

        using var sessionsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sessions");
        sessionsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);
        var sessionsResponse = await _client.SendAsync(sessionsRequest);
        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(sessions.EnumerateArray());
        Assert.True(sessions[0].GetProperty("isCurrent").GetBoolean());

        var replayResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = initialRefreshToken,
            deviceId,
            deviceKey,
            useCookies = false
        });
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        using var revokedTokenRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        revokedTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(revokedTokenRequest)).StatusCode);

    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-COOKIE-001")]
    public async Task CookieTransportStoresAccessAndRefreshSecretsWithoutReturningThem()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"cookie-{Guid.NewGuid():N}@blueverse.local",
            password = "UserPassword-123!",
            fullName = "Cookie User",
            useCookies = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("token").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("deviceKey").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("refreshToken").ValueKind);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie").ToList(),
            value => value.StartsWith("blueverse_access_token=", StringComparison.Ordinal));

        var cookieMeResponse = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, cookieMeResponse.StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new { useCookies = true });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, refreshBody.GetProperty("token").ValueKind);

        var refreshedCookieMeResponse = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, refreshedCookieMeResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-EXPIRY-001")]
    public async Task RememberMeSelectsAnAbsoluteThirtyDaySessionLifetime()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"remember-{Guid.NewGuid():N}@blueverse.local",
            password = "UserPassword-123!",
            fullName = "Remembered User",
            rememberMe = true,
            useCookies = false
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var sessionExpiresAt = body.GetProperty("sessionExpiresAt").GetDateTime();
        var remaining = sessionExpiresAt - DateTime.UtcNow;

        Assert.True(body.GetProperty("rememberMe").GetBoolean());
        Assert.InRange(remaining, TimeSpan.FromDays(29), TimeSpan.FromDays(31));
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-REVOKE-001")]
    public async Task AUserCanRevokeOneListedSession()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"revoke-{Guid.NewGuid():N}@blueverse.local",
            password = "UserPassword-123!",
            fullName = "Revoke User",
            useCookies = false
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;

        using var sessionsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sessions");
        sessionsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var sessionsResponse = await _client.SendAsync(sessionsRequest);
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = sessions[0].GetProperty("id").GetGuid();

        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/auth/sessions/{sessionId}");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var revokeResponse = await _client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(meRequest)).StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-CAP-001")]
    public async Task NewAccountSessionEvictsTheOldestActiveSessionAfterFive()
    {
        var email = $"cap-{Guid.NewGuid():N}@blueverse.local";
        var firstDevice = $"cap-device-0-{Guid.NewGuid():N}";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "UserPassword-123!",
            fullName = "Capacity User",
            deviceId = firstDevice
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstToken = registered.GetProperty("token").GetString()!;

        string latestToken = firstToken;
        for (var index = 1; index <= 5; index++)
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "UserPassword-123!",
                deviceId = $"cap-device-{index}-{Guid.NewGuid():N}"
            });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            latestToken = loginBody.GetProperty("token").GetString()!;
        }

        using var sessionsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sessions");
        sessionsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", latestToken);
        var sessionsResponse = await _client.SendAsync(sessionsRequest);
        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, sessions.GetArrayLength());
        Assert.DoesNotContain(
            sessions.EnumerateArray(),
            session => session.GetProperty("deviceId").GetString() == firstDevice);

        using var firstTokenRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        firstTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(firstTokenRequest)).StatusCode);
    }
}
