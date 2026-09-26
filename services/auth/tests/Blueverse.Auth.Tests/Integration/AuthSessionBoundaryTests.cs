using Blueverse.Auth.Tests.Fixtures;

namespace Blueverse.Auth.Tests.Integration;

public sealed class AuthSessionBoundaryTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuthSessionBoundaryTests(AuthWebApplicationFactory factory)
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
    [Trait("TestId", "AUTH-SESSION-BOUNDARY-001")]
    public async Task ReLoginOnTheSameInstallationKeepsOneActiveSessionAndRevokesThePreviousRefreshToken()
    {
        var first = await AuthTestSupport.RegisterAsync(_client, fullName: "Re-login User");
        var second = await AuthTestSupport.LoginAsync(
            _client,
            first.Email,
            deviceId: first.DeviceId,
            deviceKey: first.DeviceKey);

        Assert.Equal(first.DeviceId, second.DeviceId);
        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.False(second.SessionExpiresAt <= DateTime.UtcNow);
        Assert.InRange(
            second.SessionExpiresAt - DateTime.UtcNow,
            TimeSpan.FromHours(23),
            TimeSpan.FromHours(25));

        using var oldAccessRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", first.Token);
        using var oldAccessResponse = await _client.SendAsync(oldAccessRequest);
        Assert.Equal(HttpStatusCode.OK, oldAccessResponse.StatusCode);

        using var oldRefreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = first.RefreshToken,
            deviceId = first.DeviceId,
            deviceKey = first.DeviceKey,
            useCookies = false
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefreshResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var session = await db.ActiveSessions.SingleAsync(item => item.UserId == first.UserId);
        Assert.Equal(0, session.SessionVersion);
        Assert.Equal(1, await db.ActiveSessions.CountAsync(item => item.UserId == first.UserId));
        var refreshTokens = await db.RefreshTokens
            .Where(item => item.UserSessionId == session.Id)
            .OrderBy(item => item.IssuedAt)
            .ToListAsync();
        Assert.Equal(2, refreshTokens.Count);

        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var oldRefreshToken = Assert.Single(
            refreshTokens,
            token => token.TokenHash == refreshTokenService.Hash(first.RefreshToken));
        var currentRefreshToken = Assert.Single(
            refreshTokens,
            token => token.TokenHash == refreshTokenService.Hash(second.RefreshToken));

        Assert.NotNull(oldRefreshToken.RevokedAt);
        Assert.Equal("new-login", oldRefreshToken.RevocationReason);
        Assert.Null(oldRefreshToken.ConsumedAt);
        Assert.Null(currentRefreshToken.RevokedAt);
        Assert.Null(currentRefreshToken.ConsumedAt);
        Assert.Null(currentRefreshToken.RevocationReason);
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-BOUNDARY-002")]
    public async Task SessionListingContainsOnlySafeMetadataAndMarksExactlyOneCurrentSession()
    {
        var user = await AuthTestSupport.RegisterAsync(_client, fullName: "Session Listing User");
        var second = await AuthTestSupport.LoginAsync(
            _client,
            user.Email,
            deviceId: $"listing-device-{Guid.NewGuid():N}");

        using var sessionsRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            "/api/auth/sessions",
            second.Token);
        using var sessionsResponse = await _client.SendAsync(sessionsRequest);
        var sessions = await AuthTestSupport.ReadJsonAsync(sessionsResponse);

        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        Assert.Equal(2, sessions.GetArrayLength());
        Assert.Single(sessions.EnumerateArray(), item => item.GetProperty("isCurrent").GetBoolean());

        var allowedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "deviceId", "createdAt", "lastSeenAt", "expiresAt", "rememberMe", "isCurrent"
        };
        foreach (var session in sessions.EnumerateArray())
        {
            var actualFields = session
                .EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);
            Assert.Equal(allowedFields.Count, actualFields.Count);
            Assert.All(allowedFields, field => Assert.Contains(field, actualFields));
            Assert.False(session.TryGetProperty("refreshToken", out _));
            Assert.False(session.TryGetProperty("deviceKey", out _));
            Assert.False(session.TryGetProperty("token", out _));
        }
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-BOUNDARY-003")]
    public async Task AccountSpecificLogoutRequiresTheTargetAccountAndPreservesOtherAccounts()
    {
        var deviceId = $"account-logout-{Guid.NewGuid():N}";
        var first = await AuthTestSupport.RegisterAsync(_client, fullName: "Account Logout First", deviceId: deviceId);
        var second = await AuthTestSupport.RegisterAsync(_client, fullName: "Account Logout Second", deviceId: deviceId);

        using var logoutSecondRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/logout/{second.UserId}",
            first.Token);
        using var logoutSecondResponse = await _client.SendAsync(logoutSecondRequest);
        Assert.Equal(HttpStatusCode.Forbidden, logoutSecondResponse.StatusCode);

        using var secondMeRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", second.Token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(secondMeRequest)).StatusCode);

        using var missingAccountRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/logout/{Guid.NewGuid()}",
            first.Token);
        using var missingAccountResponse = await _client.SendAsync(missingAccountRequest);
        Assert.Equal(HttpStatusCode.Forbidden, missingAccountResponse.StatusCode);

        using var firstMeRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", first.Token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(firstMeRequest)).StatusCode);

        using var selfLogoutRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Post,
            $"/api/auth/logout/{first.UserId}",
            first.Token);
        using var selfLogoutResponse = await _client.SendAsync(selfLogoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, selfLogoutResponse.StatusCode);

        using var selfMeRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", first.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(selfMeRequest)).StatusCode);

        using var secondAfterLogoutRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            "/api/auth/me",
            second.Token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(secondAfterLogoutRequest)).StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-BOUNDARY-004")]
    public async Task CookieLogoutExpiresEveryAuthenticationCookie()
    {
        using var client = _factory.CreateClient();
        var email = AuthTestSupport.UniqueEmail("cookie-logout");
        using var loginResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "UserPassword-123!",
            fullName = "Cookie Logout User",
            useCookies = true
        });
        Assert.Equal(HttpStatusCode.Created, loginResponse.StatusCode);

        using var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        var logoutCookies = logoutResponse.Headers.GetValues("Set-Cookie").ToList();

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        foreach (var cookieName in new[]
        {
            "blueverse_device_id=",
            "blueverse_device_key=",
            "blueverse_access_token=",
            "blueverse_refresh_token="
        })
        {
            var cookie = Assert.Single(logoutCookies, value => value.StartsWith(cookieName, StringComparison.Ordinal));
            Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-COOKIE-002")]
    public async Task RevokingTheCurrentCookieSessionClearsCookiesAndEndsCookieAuthentication()
    {
        using var client = _factory.CreateClient();
        using var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = AuthTestSupport.UniqueEmail("cookie-session-revoke"),
            password = "UserPassword-123!",
            fullName = "Cookie Session Revoke User",
            useCookies = true
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        using var sessionsResponse = await client.GetAsync("/api/auth/sessions");
        var sessions = await AuthTestSupport.ReadJsonAsync(sessionsResponse);
        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        var currentSessionId = Assert.Single(
            sessions.EnumerateArray(),
            item => item.GetProperty("isCurrent").GetBoolean())
            .GetProperty("id")
            .GetGuid();

        using var revokeResponse = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/auth/sessions/{currentSessionId}")
        {
            Content = JsonContent.Create(new { currentPassword = string.Empty })
        });
        var revokeCookies = revokeResponse.Headers.GetValues("Set-Cookie").ToList();
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        Assert.Empty(await revokeResponse.Content.ReadAsStringAsync());

        foreach (var cookieName in new[]
        {
            "blueverse_device_id=",
            "blueverse_device_key=",
            "blueverse_access_token=",
            "blueverse_refresh_token="
        })
        {
            var cookie = Assert.Single(revokeCookies, value => value.StartsWith(cookieName, StringComparison.Ordinal));
            Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        }

        using var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-COOKIE-003")]
    public async Task LoggingOutEverywhereFromCookiesClearsCookiesAndRevokesOtherDevices()
    {
        var email = AuthTestSupport.UniqueEmail("cookie-logout-all");
        using var cookieClient = _factory.CreateClient();
        using var registerResponse = await cookieClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "UserPassword-123!",
            fullName = "Cookie Logout All User",
            useCookies = true
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        using var otherDeviceClient = _factory.CreateClient();
        var otherDevice = await AuthTestSupport.LoginAsync(
            otherDeviceClient,
            email,
            deviceId: $"cookie-logout-all-{Guid.NewGuid():N}");

        using var logoutResponse = await cookieClient.PostAsJsonAsync(
            "/api/auth/logout-all-devices",
            new { currentPassword = "UserPassword-123!" });
        var logoutCookies = logoutResponse.Headers.GetValues("Set-Cookie").ToList();
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Empty(await logoutResponse.Content.ReadAsStringAsync());

        foreach (var cookieName in new[]
        {
            "blueverse_device_id=",
            "blueverse_device_key=",
            "blueverse_access_token=",
            "blueverse_refresh_token="
        })
        {
            var cookie = Assert.Single(logoutCookies, value => value.StartsWith(cookieName, StringComparison.Ordinal));
            Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        }

        using var cookieMeResponse = await cookieClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, cookieMeResponse.StatusCode);

        using var otherDeviceMeRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            "/api/auth/me",
            otherDevice.Token);
        using var otherDeviceMeResponse = await otherDeviceClient.SendAsync(otherDeviceMeRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, otherDeviceMeResponse.StatusCode);
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-BOUNDARY-005")]
    public async Task ExpiredRefreshTokensAreRejectedAndCanBeArchivedByTheCleanupService()
    {
        var user = await AuthTestSupport.RegisterAsync(_client, fullName: "Expired Refresh User");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var session = await db.ActiveSessions.SingleAsync(item => item.UserId == user.UserId);
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            var refreshToken = await db.RefreshTokens.SingleAsync(item => item.UserSessionId == session.Id && item.RevokedAt == null);
            refreshToken.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = user.RefreshToken,
            deviceId = user.DeviceId,
            deviceKey = user.DeviceKey,
            useCookies = false
        });
        var refreshBody = await AuthTestSupport.ReadJsonAsync(refreshResponse);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        Assert.Equal("Refresh Failed", refreshBody.GetProperty("title").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAuthService>();
            Assert.Equal(1, await service.ArchiveExpiredSessionsAsync());
        }

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Empty(await verificationDb.ActiveSessions.Where(item => item.UserId == user.UserId).ToListAsync());
        Assert.Contains(
            await verificationDb.UserSessionLogs.Where(item => item.UserId == user.UserId).ToListAsync(),
            item => item.EndReason == "session-expired");
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-BOUNDARY-006")]
    public async Task TheSixthAccountSessionArchivesTheOldestSessionInsteadOfGrowingTheActiveTable()
    {
        var user = await AuthTestSupport.RegisterAsync(_client, fullName: "Capacity Archive User");
        var firstDevice = user.DeviceId;

        for (var index = 1; index <= 5; index++)
        {
            await AuthTestSupport.LoginAsync(
                _client,
                user.Email,
                deviceId: $"capacity-archive-{index}-{Guid.NewGuid():N}");
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Equal(5, await db.ActiveSessions.CountAsync(item => item.UserId == user.UserId));
        Assert.False(await db.ActiveSessions.AnyAsync(item => item.UserId == user.UserId && item.DeviceId == firstDevice));
        Assert.Contains(
            await db.UserSessionLogs.Where(item => item.UserId == user.UserId).ToListAsync(),
            item => item.EndReason == "capacity-eviction" && item.DeviceId == firstDevice);
    }

    [Fact]
    [Trait("TestId", "AUTH-DEVICE-002")]
    public async Task LegacyDeviceIdentifiersAreNormalizedAndReuseTheSameInstallation()
    {
        var requestedDeviceId = $"  Legacy-Device-{Guid.NewGuid():N}  ";
        var first = await AuthTestSupport.RegisterAsync(
            _client,
            fullName: "Legacy Device User",
            deviceId: requestedDeviceId);
        var second = await AuthTestSupport.LoginAsync(
            _client,
            first.Email,
            deviceId: requestedDeviceId.ToUpperInvariant());

        Assert.Equal(requestedDeviceId.Trim().ToLowerInvariant(), first.DeviceId);
        Assert.Equal(first.DeviceId, second.DeviceId);
        Assert.Equal(first.UserId, second.UserId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Single(await db.DeviceInstallations.Where(item => item.DeviceId == first.DeviceId).ToListAsync());
        Assert.Single(await db.ActiveSessions.Where(item => item.UserId == first.UserId).ToListAsync());
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-BOUNDARY-007")]
    public async Task SessionRevocationCannotTargetAnotherUserOrAnUnknownSession()
    {
        var first = await AuthTestSupport.RegisterAsync(_client, fullName: "Session Owner");
        var second = await AuthTestSupport.RegisterAsync(_client, fullName: "Other Session Owner");

        using var otherSessionsRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            "/api/auth/sessions",
            second.Token);
        using var otherSessionsResponse = await _client.SendAsync(otherSessionsRequest);
        var otherSessions = await AuthTestSupport.ReadJsonAsync(otherSessionsResponse);
        var otherSessionId = otherSessions[0].GetProperty("id").GetGuid();

        using var crossUserRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/sessions/{otherSessionId}",
            first.Token,
            new { currentPassword = "UserPassword-123!" });
        using var crossUserResponse = await _client.SendAsync(crossUserRequest);
        Assert.Equal(HttpStatusCode.NotFound, crossUserResponse.StatusCode);

        using var unknownRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/sessions/{Guid.NewGuid()}",
            first.Token,
            new { currentPassword = "UserPassword-123!" });
        using var unknownResponse = await _client.SendAsync(unknownRequest);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);

        using var ownSessionsRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Get,
            "/api/auth/sessions",
            first.Token);
        using var ownSessionsResponse = await _client.SendAsync(ownSessionsRequest);
        var ownSessions = await AuthTestSupport.ReadJsonAsync(ownSessionsResponse);
        var ownSessionId = ownSessions[0].GetProperty("id").GetGuid();

        using var revokeOwnRequest = AuthTestSupport.AuthorizedRequest(
            HttpMethod.Delete,
            $"/api/auth/sessions/{ownSessionId}",
            first.Token,
            new { currentPassword = string.Empty });
        using var revokeOwnResponse = await _client.SendAsync(revokeOwnRequest);
        Assert.Equal(HttpStatusCode.NoContent, revokeOwnResponse.StatusCode);

        using var revokedMeRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", first.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(revokedMeRequest)).StatusCode);
        using var otherMeRequest = AuthTestSupport.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", second.Token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(otherMeRequest)).StatusCode);
    }
}
