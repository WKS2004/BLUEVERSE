namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Requirement-based safety-profile endpoint tests (M2-PROF-*): the CRUD path,
/// validation and authorization outcomes are asserted from the component
/// contract, including exact status codes, response bodies and persistence.
/// </summary>
public sealed class SafetyProfileEndpointTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;

    public SafetyProfileEndpointTests(MarineSafetyWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Provider = new StubOpenMeteoClient();
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        _factory.ResetDatabase();
        var client = _factory.CreateClient();
        await _factory.SeedAsync(db => MarineSafetyTestSeed.SeedIdentitiesAsync(db, _factory.Resolver));
        await _factory.SeedAsync(MarineSafetyTestSeed.SeedConfigurationAsync);
        return client;
    }

    private static HttpRequestMessage Authorized(
        HttpMethod method,
        string uri,
        string token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-001")]
    public async Task M2_PROF_001_manager_creates_a_valid_profile_and_it_is_persisted()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);
        var newActivityId = Guid.NewGuid();

        await _factory.SeedAsync(db =>
        {
            db.MarineActivities.Add(new MarineActivity
            {
                Id = newActivityId,
                Name = "Kayaking " + Guid.NewGuid().ToString("N"),
                ActivityType = "BoatTour",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            return db.SaveChangesAsync();
        });

        using var request = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token, new
        {
            activityId = newActivityId,
            maxWindSpeed = 30m,
            maxWaveHeight = 2.0m,
            maxSwellHeight = 1.5m
        });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(newActivityId, root.GetProperty("activityId").GetGuid());
        Assert.Equal(30m, root.GetProperty("maxWindSpeed").GetDecimal());
        Assert.Equal(2.0m, root.GetProperty("maxWaveHeight").GetDecimal());
        Assert.Equal(1.5m, root.GetProperty("maxSwellHeight").GetDecimal());
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, root.GetProperty("version").GetInt32());

        // Persistence: the profile is retrievable through the read path.
        var profileId = root.GetProperty("id").GetGuid();
        using var verify = Authorized(HttpMethod.Get, $"/api/marine/safety-profiles/{profileId}", token);
        using var verifyResponse = await client.SendAsync(verify);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-002")]
    public async Task M2_PROF_002_create_with_nonexistent_activity_is_conflict()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        using var request = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token, new
        {
            activityId = Guid.NewGuid(),
            maxWindSpeed = 30m,
            maxWaveHeight = 2.0m,
            maxSwellHeight = 1.5m
        });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Profile Rejected", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-003")]
    public async Task M2_PROF_003_create_with_invalid_numeric_limits_is_rejected()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        foreach (var invalidLimits in new[]
                 {
                     new { activityId = MarineSafetyTestSeed.ActivityId, maxWindSpeed = -5m, maxWaveHeight = 1.5m, maxSwellHeight = 1m },
                     new { activityId = MarineSafetyTestSeed.ActivityId, maxWindSpeed = 25m, maxWaveHeight = 0m, maxSwellHeight = 1m },
                     new { activityId = MarineSafetyTestSeed.ActivityId, maxWindSpeed = 25m, maxWaveHeight = 1.5m, maxSwellHeight = -1m }
                 })
        {
            using var request = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token, invalidLimits);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-004")]
    public async Task M2_PROF_004_create_with_caution_band_at_or_above_maximum_is_rejected()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        using var request = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token, new
        {
            activityId = MarineSafetyTestSeed.ActivityId,
            maxWindSpeed = 25m,
            maxWaveHeight = 1.5m,
            maxSwellHeight = 1.2m,
            cautionWaveHeight = 1.5m
        });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Caution wave height", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-005")]
    public async Task M2_PROF_005_profiles_are_listed_and_filtered_by_activity()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        using var list = Authorized(HttpMethod.Get, "/api/marine/safety-profiles", token);
        using var listResponse = await client.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        using var listDocument = JsonDocument.Parse(listBody);
        Assert.True(listDocument.RootElement.ValueKind == JsonValueKind.Array);
        Assert.True(listDocument.RootElement.GetArrayLength() >= 1);

        using var byActivity = Authorized(
            HttpMethod.Get,
            $"/api/marine/safety-profiles/by-activity/{MarineSafetyTestSeed.ActivityId}",
            token);
        using var byActivityResponse = await client.SendAsync(byActivity);
        Assert.Equal(HttpStatusCode.OK, byActivityResponse.StatusCode);
        var byActivityBody = await byActivityResponse.Content.ReadAsStringAsync();
        using var byActivityDocument = JsonDocument.Parse(byActivityBody);
        Assert.Equal(
            MarineSafetyTestSeed.ActivityId,
            byActivityDocument.RootElement.GetProperty("activityId").GetGuid());
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-006")]
    public async Task M2_PROF_006_get_by_unknown_id_returns_not_found()
    {
        using var client = await CreateClientAsync();
        await _factory.SeedAsync(db => MarineSafetyTestSeed.SeedIdentitiesAsync(db, _factory.Resolver));
        var token = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);

        using var request = Authorized(
            HttpMethod.Get,
            $"/api/marine/safety-profiles/{Guid.NewGuid()}",
            token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-007")]
    public async Task M2_PROF_007_manager_updates_limits_and_version_increments()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        using var current = Authorized(
            HttpMethod.Get,
            $"/api/marine/safety-profiles/by-activity/{MarineSafetyTestSeed.ActivityId}",
            token);
        using var currentResponse = await client.SendAsync(current);
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        var currentBody = await currentResponse.Content.ReadAsStringAsync();
        using var currentDocument = JsonDocument.Parse(currentBody);
        var profileId = currentDocument.RootElement.GetProperty("id").GetGuid();
        var versionBefore = currentDocument.RootElement.GetProperty("version").GetInt32();

        using var request = Authorized(HttpMethod.Put, $"/api/marine/safety-profiles/{profileId}", token, new
        {
            maxWindSpeed = 22m,
            maxWaveHeight = 1.2m,
            maxSwellHeight = 1.0m,
            isActive = true
        });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(22m, document.RootElement.GetProperty("maxWindSpeed").GetDecimal());
        Assert.Equal(1.2m, document.RootElement.GetProperty("maxWaveHeight").GetDecimal());
        Assert.Equal(versionBefore + 1, document.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-008")]
    public async Task M2_PROF_008_delete_deactivates_instead_of_deleting()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        using var current = Authorized(
            HttpMethod.Get,
            $"/api/marine/safety-profiles/by-activity/{MarineSafetyTestSeed.ActivityId}",
            token);
        using var currentResponse = await client.SendAsync(current);
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        var currentBody = await currentResponse.Content.ReadAsStringAsync();
        using var currentDocument = JsonDocument.Parse(currentBody);
        var profileId = currentDocument.RootElement.GetProperty("id").GetGuid();

        using var delete = Authorized(HttpMethod.Delete, $"/api/marine/safety-profiles/{profileId}", token);
        using var response = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The row remains retrievable (deactivation, not deletion), flagged inactive.
        using var verify = Authorized(HttpMethod.Get, $"/api/marine/safety-profiles/{profileId}", token);
        using var verifyResponse = await client.SendAsync(verify);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        using var verifyDocument = JsonDocument.Parse(verifyBody);
        Assert.False(verifyDocument.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-009")]
    public async Task M2_PROF_009_manage_operations_require_the_manage_grant()
    {
        using var client = await CreateClientAsync();

        // Reader: authenticated but without the manage grant.
        var readerToken = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);
        using var forbidden = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", readerToken, new
        {
            activityId = MarineSafetyTestSeed.ActivityId,
            maxWindSpeed = 25m,
            maxWaveHeight = 1.5m,
            maxSwellHeight = 1.2m
        });
        using var forbiddenResponse = await client.SendAsync(forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        // Unauthenticated callers are challenged.
        using var anonymous = await client.PostAsJsonAsync("/api/marine/safety-profiles", new
        {
            activityId = MarineSafetyTestSeed.ActivityId,
            maxWindSpeed = 25m,
            maxWaveHeight = 1.5m,
            maxSwellHeight = 1.2m
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
