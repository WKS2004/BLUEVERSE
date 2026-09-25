using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.Auth.Tests.Fixtures;
using Blueverse.Auth.Security;

namespace Blueverse.Auth.Tests.Integration;

public sealed class CookieMultiAccountTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CookieMultiAccountTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        await _factory.SeedAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("TestId", "AUTH-MULTI-COOKIE-001")]
    public async Task BrowserAccountsCanSwitchAndMustSignOutOneAccountAtATime()
    {
        var firstAccount = await RegisterCookieAccountAsync(_client, "First browser account");

        // Recreate a browser that still has only the original shared cookies
        // from before account-scoped cookies were introduced.
        var legacyCookieJar = new CookieContainer();
        var legacyCookieNames = new HashSet<string>(StringComparer.Ordinal)
        {
            AuthCookieNames.DeviceId,
            AuthCookieNames.DeviceKey,
            AuthCookieNames.LegacyAccessToken,
            AuthCookieNames.LegacyRefreshToken
        };
        foreach (var header in firstAccount.SetCookieHeaders)
        {
            var cookieName = header.Split('=', 2)[0];
            if (legacyCookieNames.Contains(cookieName))
            {
                legacyCookieJar.SetCookies(new Uri("http://localhost"), header);
            }
        }

        using var legacyClient = CreateCookieClient(_factory, legacyCookieJar);
        using var migrateLegacyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { useCookies = true, accountId = firstAccount.UserId })
        };
        var migrateLegacyResponse = await legacyClient.SendAsync(migrateLegacyRequest);
        Assert.Equal(HttpStatusCode.OK, migrateLegacyResponse.StatusCode);
        Assert.Contains(
            AuthCookieNames.AccessTokenFor(firstAccount.UserId),
            legacyCookieJar.GetCookies(new Uri("http://localhost")).Cast<Cookie>().Select(cookie => cookie.Name));

        var secondAccount = await RegisterCookieAccountAsync(legacyClient, "Second browser account");

        Assert.Equal(secondAccount.UserId, await GetCurrentUserIdAsync(legacyClient));

        await SelectCookieAccountAsync(legacyClient, firstAccount.UserId);
        Assert.Equal(firstAccount.UserId, await GetCurrentUserIdAsync(legacyClient));

        await SelectCookieAccountAsync(legacyClient, secondAccount.UserId);
        Assert.Equal(secondAccount.UserId, await GetCurrentUserIdAsync(legacyClient));

        var crossAccountLogoutResponse = await legacyClient.PostAsJsonAsync(
            "/api/auth/logout-account",
            new { userId = firstAccount.UserId });
        Assert.Equal(HttpStatusCode.Forbidden, crossAccountLogoutResponse.StatusCode);
        Assert.Equal(secondAccount.UserId, await GetCurrentUserIdAsync(legacyClient));

        await SelectCookieAccountAsync(legacyClient, firstAccount.UserId);
        var removeResponse = await legacyClient.PostAsJsonAsync(
            "/api/auth/logout-account",
            new { userId = firstAccount.UserId });
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        await SelectCookieAccountAsync(legacyClient, secondAccount.UserId);
        Assert.Equal(secondAccount.UserId, await GetCurrentUserIdAsync(legacyClient));

        using var removedAccountRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { useCookies = true, accountId = firstAccount.UserId })
        };
        var removedAccountResponse = await legacyClient.SendAsync(removedAccountRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, removedAccountResponse.StatusCode);
    }

    private async Task<(Guid UserId, string Email, string[] SetCookieHeaders)> RegisterCookieAccountAsync(
        HttpClient client,
        string fullName)
    {
        var email = $"cookie-{Guid.NewGuid():N}@example.test";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "UserPassword-123!",
            fullName,
            rememberMe = true,
            useCookies = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("user").GetProperty("email").GetString());
        Assert.True(body.GetProperty("token").ValueKind is JsonValueKind.Null);
        return (
            body.GetProperty("user").GetProperty("id").GetGuid(),
            email,
            response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders)
                ? setCookieHeaders.ToArray()
                : []);
    }

    private async Task SelectCookieAccountAsync(HttpClient client, Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { useCookies = true, accountId = userId })
        };
        var response = await client.SendAsync(request);
        Assert.True(
            response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK,
            $"Selecting an available browser account returned {(int)response.StatusCode}.");
    }

    private static async Task<Guid> GetCurrentUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static HttpClient CreateCookieClient(AuthWebApplicationFactory factory, CookieContainer cookies)
    {
        var handler = new CookieJarHandler(cookies)
        {
            InnerHandler = factory.Server.CreateHandler()
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    private sealed class CookieJarHandler(CookieContainer cookies) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri ?? throw new InvalidOperationException("A request URI is required.");
            var cookieHeader = cookies.GetCookieHeader(requestUri);
            if (!string.IsNullOrWhiteSpace(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            {
                foreach (var setCookieHeader in setCookieHeaders)
                {
                    cookies.SetCookies(requestUri, setCookieHeader);
                }
            }

            return response;
        }
    }
}
