using Blueverse.Auth.Tests.Fixtures;

namespace Blueverse.Auth.Tests.Integration;

public sealed class PostgresAuthSessionTests : IClassFixture<PostgresAuthWebApplicationFactory>
{
    private readonly PostgresAuthWebApplicationFactory _factory;

    public PostgresAuthSessionTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("TestId", "AUTH-POSTGRES-SESSION-001")]
    public async Task PostgreSqlSerializesCapacityAndRefreshDecisions()
    {
        using var client = _factory.CreateClient();
        var email = $"postgres-session-{Guid.NewGuid():N}@blueverse.local";
        const string password = "PostgresSessionPassword-123!";
        string? latestToken = null;

        try
        {
            var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password,
                fullName = "PostgreSQL Session User",
                useCookies = false
            });
            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
            var registered = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
            var initialRefreshToken = registered.GetProperty("refreshToken").GetString()!;
            var deviceId = registered.GetProperty("deviceId").GetString()!;
            var deviceKey = registered.GetProperty("deviceKey").GetString()!;
            latestToken = registered.GetProperty("token").GetString();

            var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
            {
                refreshToken = initialRefreshToken,
                deviceId,
                deviceKey,
                useCookies = false
            });
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
            var refreshed = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
            latestToken = refreshed.GetProperty("token").GetString();

            var concurrentLogins = Enumerable.Range(1, 6)
                .Select(index => client.PostAsJsonAsync("/api/auth/login", new
                {
                    email,
                    password,
                    deviceId = $"postgres-capacity-{Guid.NewGuid():N}",
                    useCookies = false
                }))
                .ToArray();
            var loginResponses = await Task.WhenAll(concurrentLogins);
            Assert.All(loginResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

            JsonElement sessions = default;
            var foundActiveToken = false;
            foreach (var loginResponse in loginResponses)
            {
                var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
                var candidateToken = loginBody.GetProperty("token").GetString();
                using var sessionsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sessions");
                sessionsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken!);
                var sessionsResponse = await client.SendAsync(sessionsRequest);
                if (sessionsResponse.StatusCode == HttpStatusCode.OK)
                {
                    latestToken = candidateToken;
                    sessions = await sessionsResponse.Content.ReadFromJsonAsync<JsonElement>();
                    foundActiveToken = true;
                    break;
                }
            }

            Assert.True(foundActiveToken, "At least one concurrent login must remain active.");
            Assert.Equal(5, sessions.GetArrayLength());

            var replayResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
            {
                refreshToken = initialRefreshToken,
                deviceId,
                deviceKey,
                useCookies = false
            });
            Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(latestToken))
            {
                using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/me");
                deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", latestToken);
                await client.SendAsync(deleteRequest);
            }
        }
    }
}
