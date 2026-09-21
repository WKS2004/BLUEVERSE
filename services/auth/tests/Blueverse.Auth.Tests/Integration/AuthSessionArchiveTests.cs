using Blueverse.Auth.Tests.Fixtures;

namespace Blueverse.Auth.Tests.Integration;

public sealed class AuthSessionArchiveTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;

    public AuthSessionArchiveTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-ARCHIVE-001")]
    public async Task LoggingOutMovesTheSessionAndItsRefreshTokensToTheArchive()
    {
        using var client = _factory.CreateClient();
        var email = $"archive-{Guid.NewGuid():N}@blueverse.local";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "ArchivePassword-123!",
            fullName = "Archive User",
            useCookies = false
        });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = registered.GetProperty("token").GetString()!;
        var deviceId = registered.GetProperty("deviceId").GetString()!;
        var deviceKey = registered.GetProperty("deviceKey").GetString()!;
        var userId = registered.GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            Assert.Single(await db.ActiveSessions.Where(session => session.UserId == userId).ToListAsync());
            Assert.Empty(await db.UserSessionLogs.Where(log => log.UserId == userId).ToListAsync());
        }

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        Guid archivedSessionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            Assert.Empty(await db.ActiveSessions.Where(session => session.UserId == userId).ToListAsync());
            var log = await db.UserSessionLogs.SingleAsync(item => item.UserId == userId);
            archivedSessionId = log.Id;
            Assert.Equal("device-logout", log.EndReason);
            Assert.True(log.EndedAt >= log.CreatedAt);

            var refreshToken = await db.RefreshTokens.SingleAsync(item => item.UserSessionLogId == log.Id);
            Assert.Null(refreshToken.UserSessionId);
            Assert.Equal(log.Id, refreshToken.UserSessionLogId);
            Assert.NotNull(refreshToken.RevokedAt);
            Assert.Equal("device-logout", refreshToken.RevocationReason);
        }

        var secondLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "ArchivePassword-123!",
            deviceId,
            deviceKey,
            useCookies = false
        });
        Assert.Equal(HttpStatusCode.OK, secondLoginResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            Assert.Single(await db.ActiveSessions.Where(session => session.UserId == userId).ToListAsync());
            Assert.True(await db.UserSessionLogs.AnyAsync(log => log.Id == archivedSessionId));
            Assert.Equal(1, await db.UserSessionLogs.CountAsync(log => log.UserId == userId));
        }
    }

    [Fact]
    [Trait("TestId", "AUTH-SESSION-ARCHIVE-002")]
    public async Task ExpiredActiveSessionsAreArchivedWithAnExpiryReason()
    {
        using var client = _factory.CreateClient();
        var email = $"expired-archive-{Guid.NewGuid():N}@blueverse.local";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "ArchivePassword-123!",
            fullName = "Expired Archive User",
            useCookies = false
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registered.GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var session = await db.ActiveSessions.SingleAsync(item => item.UserId == userId);
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            Assert.Equal(1, await authService.ArchiveExpiredSessionsAsync());
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            Assert.Empty(await db.ActiveSessions.Where(item => item.UserId == userId).ToListAsync());
            var log = await db.UserSessionLogs.SingleAsync(item => item.UserId == userId);
            Assert.Equal("session-expired", log.EndReason);
            Assert.True(log.EndedAt >= log.CreatedAt);
        }
    }
}
