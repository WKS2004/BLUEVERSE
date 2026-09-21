using Blueverse.Auth.Tests.Fixtures;

namespace Blueverse.Auth.Tests.Integration;

public sealed class AuthContractBoundaryTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuthContractBoundaryTests(AuthWebApplicationFactory factory)
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
    [Trait("TestId", "AUTH-HEALTH-001")]
    public async Task HealthReportsTheAuthServiceAndDatabaseAsHealthy()
    {
        using var response = await _client.GetAsync("/api/auth/health");
        var body = await AuthTestSupport.ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("auth", body.GetProperty("service").GetString());
        Assert.Equal("healthy", body.GetProperty("status").GetString());
        Assert.Equal("connected", body.GetProperty("database").GetString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Trait("TestId", "AUTH-CONTRACT-001")]
    public async Task AuthOpenApiDocumentListsTheCompletePublicAuthSurface()
    {
        using var response = await _client.GetAsync("/api/auth/swagger/v1/swagger.json");
        var body = await AuthTestSupport.ReadJsonAsync(response);
        var paths = body.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BLUEVERSE Auth API", body.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", body.GetProperty("info").GetProperty("version").GetString());

        var expectedPaths = new[]
        {
            "/api/auth/register",
            "/api/auth/login",
            "/api/auth/refresh",
            "/api/auth/logout",
            "/api/auth/logout/{id}",
            "/api/auth/logout-all-devices",
            "/api/auth/sessions",
            "/api/auth/sessions/{sessionId}",
            "/api/auth/change-password",
            "/api/auth/me",
            "/api/auth/users",
            "/api/auth/users/{id}",
            "/api/auth/users/{id}/roles",
            "/api/auth/roles",
            "/api/auth/roles/{id}",
            "/api/auth/roles/{id}/permissions",
            "/api/auth/permissions",
            "/api/auth/permissions/{id}",
            "/api/auth/health"
        };

        foreach (var path in expectedPaths)
        {
            Assert.True(paths.TryGetProperty(path, out _), $"Auth OpenAPI is missing {path}.");
        }

        var bearer = body
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());

        var profileProperties = body
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("UpdateProfileDto")
            .GetProperty("properties");
        Assert.True(profileProperties.TryGetProperty("fullName", out _));
        Assert.False(profileProperties.TryGetProperty("password", out _));
        Assert.False(profileProperties.TryGetProperty("currentPassword", out _));
        Assert.False(profileProperties.TryGetProperty("newPassword", out _));

        var changePasswordRequest = paths
            .GetProperty("/api/auth/change-password")
            .GetProperty("post")
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.EndsWith("/ChangePasswordDto", changePasswordRequest, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "AUTH-CONTRACT-002")]
    public async Task RegistrationNormalizesIdentityAndUsesTheDefaultOneDaySessionLifetime()
    {
        var requestedEmail = $"  MixedCase-{Guid.NewGuid():N}@BlueVerse.Local  ";
        var requestedName = "  Contract User  ";
        using var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = requestedEmail,
            password = "UserPassword-123!",
            fullName = requestedName,
            useCookies = false
        });
        var body = await AuthTestSupport.ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(requestedEmail.Trim().ToLowerInvariant(), body.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal(requestedName.Trim(), body.GetProperty("user").GetProperty("fullName").GetString());
        Assert.False(body.GetProperty("rememberMe").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("refreshToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("deviceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("deviceKey").GetString()));
        Assert.DoesNotContain("password", body.ToString(), StringComparison.OrdinalIgnoreCase);

        var sessionLifetime = body.GetProperty("sessionExpiresAt").GetDateTime() - DateTime.UtcNow;
        Assert.InRange(sessionLifetime, TimeSpan.FromHours(23), TimeSpan.FromHours(25));
        Assert.True(body.GetProperty("expiresAt").GetDateTime() < body.GetProperty("sessionExpiresAt").GetDateTime());
    }

    [Fact]
    [Trait("TestId", "AUTH-VALIDATION-002")]
    public async Task RegistrationRejectsInvalidMissingAndOversizedInputWithValidationDetails()
    {
        var invalidRequests = new (object Payload, string ExpectedField)[]
        {
            (new Dictionary<string, object?>
            {
                ["email"] = "not-an-email",
                ["password"] = "UserPassword-123!",
                ["fullName"] = "Validation User"
            }, "email"),
            (new Dictionary<string, object?>
            {
                ["email"] = AuthTestSupport.UniqueEmail("missing-password"),
                ["fullName"] = "Validation User"
            }, "password"),
            (new Dictionary<string, object?>
            {
                ["email"] = AuthTestSupport.UniqueEmail("short-password"),
                ["password"] = "short",
                ["fullName"] = "Validation User"
            }, "password"),
            (new Dictionary<string, object?>
            {
                ["email"] = AuthTestSupport.UniqueEmail("missing-name"),
                ["password"] = "UserPassword-123!"
            }, "fullName"),
            (new Dictionary<string, object?>
            {
                ["email"] = AuthTestSupport.UniqueEmail("long-device"),
                ["password"] = "UserPassword-123!",
                ["fullName"] = "Validation User",
                ["deviceId"] = new string('x', 129)
            }, "deviceId")
        };

        foreach (var (payload, expectedField) in invalidRequests)
        {
            using var response = await _client.PostAsJsonAsync("/api/auth/register", payload);
            var body = await AuthTestSupport.ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(400, body.GetProperty("status").GetInt32());
            Assert.True(body.TryGetProperty("errors", out var errors));
            Assert.NotEqual(JsonValueKind.Null, errors.ValueKind);
            Assert.Contains(
                errors.EnumerateObject(),
                property => string.Equals(property.Name, expectedField, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    [Trait("TestId", "AUTH-VALIDATION-005")]
    public async Task MalformedJsonReturnsAValidationProblemInsteadOfAnUnhandledError()
    {
        using var content = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/auth/register", content);
        var body = await AuthTestSupport.ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.Contains(
            errors.EnumerateObject(),
            property => string.Equals(property.Name, "$", StringComparison.Ordinal));
        Assert.Contains(
            errors.EnumerateObject(),
            property => string.Equals(property.Name, "dto", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("TestId", "AUTH-VALIDATION-003")]
    public async Task RegistrationTreatsEmailUniquenessAsCaseInsensitiveAndTrimmed()
    {
        var email = $"Duplicate-{Guid.NewGuid():N}@BlueVerse.Local";
        var first = await AuthTestSupport.RegisterAsync(
            _client,
            email: $"  {email}  ",
            fullName: "First Registration");

        using var duplicateResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = email.ToUpperInvariant(),
            password = "AnotherPassword-123!",
            fullName = "Duplicate Registration"
        });
        var duplicateBody = await AuthTestSupport.ReadJsonAsync(duplicateResponse);

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
        Assert.Equal("Registration Failed", duplicateBody.GetProperty("title").GetString());
        Assert.Contains("already exists", duplicateBody.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(user => user.Email == first.Email));
    }

    [Fact]
    [Trait("TestId", "AUTH-TRANSPORT-001")]
    public async Task CookieLoginReturnsNoSecretsAndSetsProtectedCookieAttributes()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = AuthTestSupport.UniqueEmail("cookie-contract"),
            password = "UserPassword-123!",
            fullName = "Cookie Contract User",
            useCookies = true
        });
        var body = await AuthTestSupport.ReadJsonAsync(response);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("token").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("deviceKey").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("refreshToken").ValueKind);

        foreach (var cookieName in new[]
        {
            "blueverse_device_id=",
            "blueverse_device_key=",
            "blueverse_access_token=",
            "blueverse_refresh_token="
        })
        {
            var cookie = Assert.Single(setCookies, value => value.StartsWith(cookieName, StringComparison.Ordinal));
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("TestId", "AUTH-VALIDATION-004")]
    public async Task ChangePasswordRejectsMissingAndShortPasswordsBeforeServiceExecution()
    {
        var user = await AuthTestSupport.RegisterAsync(_client, fullName: "Password Validation User");
        var invalidRequests = new object[]
        {
            new { currentPassword = "UserPassword-123!" },
            new { newPassword = "NewPassword-456!" },
            new { currentPassword = "UserPassword-123!", newPassword = "short" }
        };

        foreach (var payload in invalidRequests)
        {
            using var request = AuthTestSupport.AuthorizedRequest(
                HttpMethod.Post,
                "/api/auth/change-password",
                user.Token,
                payload);
            using var response = await _client.SendAsync(request);
            var body = await AuthTestSupport.ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(400, body.GetProperty("status").GetInt32());
        }
    }

    [Fact]
    [Trait("TestId", "AUTH-DEVICE-001")]
    public async Task AServerIssuedDeviceWithAnInvalidProofFallsBackToANewInstallationWithoutPersistingSecrets()
    {
        var original = await AuthTestSupport.RegisterAsync(_client, fullName: "Device Proof User");
        var replacement = await AuthTestSupport.LoginAsync(
            _client,
            original.Email,
            deviceId: original.DeviceId,
            deviceKey: "wrong-device-key");

        Assert.NotEqual(original.DeviceId, replacement.DeviceId);
        Assert.NotEqual(original.DeviceKey, replacement.DeviceKey);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var installations = await db.DeviceInstallations
            .Where(item => item.DeviceId == original.DeviceId || item.DeviceId == replacement.DeviceId)
            .ToListAsync();
        Assert.Equal(2, installations.Count);
        Assert.All(installations, installation =>
        {
            Assert.DoesNotContain(original.DeviceKey, installation.DeviceKeyHash ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(replacement.DeviceKey, installation.DeviceKeyHash ?? string.Empty, StringComparison.Ordinal);
        });

        var activeSessionIds = await db.ActiveSessions
            .Where(item => item.UserId == original.UserId)
            .Select(item => item.Id)
            .ToListAsync();
        var refreshTokens = await db.RefreshTokens
            .Where(token => token.UserSessionId.HasValue && activeSessionIds.Contains(token.UserSessionId.Value))
            .ToListAsync();
        Assert.Equal(2, refreshTokens.Count);
        Assert.DoesNotContain(original.RefreshToken, refreshTokens.Select(token => token.TokenHash));
        Assert.DoesNotContain(replacement.RefreshToken, refreshTokens.Select(token => token.TokenHash));
    }
}
