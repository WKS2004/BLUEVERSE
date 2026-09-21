using System.Net.Http.Headers;
using System.Text.Json;

namespace Blueverse.Auth.Tests.Integration;

internal static class AuthTestSupport
{
    public static string UniqueEmail(string prefix = "user") =>
        $"{prefix}-{Guid.NewGuid():N}@blueverse.local";

    public static async Task<AuthTestIdentity> RegisterAsync(
        HttpClient client,
        string? email = null,
        string password = "UserPassword-123!",
        string fullName = "Test User",
        string? deviceId = null,
        string? deviceKey = null,
        bool rememberMe = false,
        bool useCookies = false)
    {
        email ??= UniqueEmail();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName,
            deviceId,
            deviceKey,
            rememberMe,
            useCookies
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadIdentityAsync(response, email);
    }

    public static async Task<AuthTestIdentity> LoginAsync(
        HttpClient client,
        string email,
        string password = "UserPassword-123!",
        string? deviceId = null,
        string? deviceKey = null,
        bool rememberMe = false,
        bool useCookies = false)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password,
            deviceId,
            deviceKey,
            rememberMe,
            useCookies
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadIdentityAsync(response, email);
    }

    public static HttpRequestMessage AuthorizedRequest(
        HttpMethod method,
        string uri,
        string token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<AuthTestIdentity> ReadIdentityAsync(
        HttpResponseMessage response,
        string requestedEmail)
    {
        var body = await ReadJsonAsync(response);
        var user = body.GetProperty("user");
        return new AuthTestIdentity(
            Token: body.GetProperty("token").GetString() ?? string.Empty,
            RefreshToken: body.GetProperty("refreshToken").GetString() ?? string.Empty,
            DeviceId: body.GetProperty("deviceId").GetString() ?? string.Empty,
            DeviceKey: body.GetProperty("deviceKey").GetString() ?? string.Empty,
            UserId: user.GetProperty("id").GetGuid(),
            Email: user.GetProperty("email").GetString() ?? requestedEmail,
            SessionExpiresAt: body.GetProperty("sessionExpiresAt").GetDateTime());
    }
}

internal sealed record AuthTestIdentity(
    string Token,
    string RefreshToken,
    string DeviceId,
    string DeviceKey,
    Guid UserId,
    string Email,
    DateTime SessionExpiresAt);
