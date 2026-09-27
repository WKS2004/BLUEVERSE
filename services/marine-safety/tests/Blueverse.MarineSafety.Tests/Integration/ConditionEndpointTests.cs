namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Requirement-based condition endpoint tests (M2-COND-*): acquisition,
/// snapshot reads, history filters and provider failure behavior, asserted
/// against the component contract's source/freshness/missing-data semantics.
/// </summary>
public sealed class ConditionEndpointTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;

    public ConditionEndpointTests(MarineSafetyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        _factory.ResetDatabase();
        var client = _factory.CreateClient();
        await _factory.SeedAsync(db => MarineSafetyTestSeed.SeedIdentitiesAsync(db, _factory.Resolver));
        await _factory.SeedAsync(MarineSafetyTestSeed.SeedConfigurationAsync);
        return client;
    }

    private string ReaderToken() =>
        _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);

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
    [Trait("CaseId", "M2-COND-001")]
    public async Task M2_COND_001_current_conditions_return_valid_data_with_provenance()
    {
        _factory.Provider = new StubOpenMeteoClient();
        using var client = await CreateClientAsync();

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            ReaderToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("Open-Meteo", root.GetProperty("source").GetString());
        Assert.Equal("FRESH", root.GetProperty("freshnessStatus").GetString());
        Assert.Equal(15m, root.GetProperty("windSpeed").GetDecimal());
        Assert.Equal(0.8m, root.GetProperty("waveHeight").GetDecimal());
        Assert.Equal(0.7m, root.GetProperty("swellHeight").GetDecimal());
        Assert.Empty(root.GetProperty("missingFields").EnumerateArray());

        // Persistence: the snapshot is stored and retrievable by ID.
        var snapshotId = root.GetProperty("id").GetGuid();
        using var verify = Authorized(
            HttpMethod.Get,
            $"/api/marine/snapshots/{snapshotId}",
            ReaderToken());
        using var verifyResponse = await client.SendAsync(verify);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-COND-002")]
    public async Task M2_COND_002_invalid_coordinates_are_rejected()
    {
        using var client = await CreateClientAsync();

        foreach (var query in new[]
                 {
                     "latitude=91&longitude=80",
                     "latitude=6&longitude=181",
                     "latitude=-95&longitude=80"
                 })
        {
            using var request = Authorized(
                HttpMethod.Get,
                $"/api/marine/current?{query}",
                ReaderToken());
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    [Trait("CaseId", "M2-COND-003")]
    public async Task M2_COND_003_provider_outage_returns_service_unavailable_without_fabricated_data()
    {
        _factory.Provider = StubOpenMeteoClient.Unavailable("provider is unreachable");
        using var client = await CreateClientAsync();

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            ReaderToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Marine Conditions Unavailable", document.RootElement.GetProperty("title").GetString());
        Assert.True(response.Headers.Contains("Retry-After"));
    }

    [Fact]
    [Trait("CaseId", "M2-COND-004")]
    public async Task M2_COND_004_provider_rate_limit_is_handled_as_unavailable()
    {
        _factory.Provider = StubOpenMeteoClient.RateLimited();
        using var client = await CreateClientAsync();

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            ReaderToken());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-COND-005")]
    public async Task M2_COND_005_missing_provider_fields_are_reported_never_zeroed()
    {
        _factory.Provider = new StubOpenMeteoClient(() => new MarineConditionsResult(
            DateTime.UtcNow,
            DateTime.UtcNow,
            WindSpeed: 15m,
            WaveHeight: null,
            SwellHeight: null,
            Rain: 0m,
            WeatherCode: 0,
            Source: ConditionSources.OpenMeteo,
            MissingFields: ["waveHeight", "swellHeight"]));
        using var client = await CreateClientAsync();

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            ReaderToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("waveHeight").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("swellHeight").ValueKind);
        var missing = root.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.Contains("waveHeight", missing);
        Assert.Contains("swellHeight", missing);
    }

    [Fact]
    [Trait("CaseId", "M2-COND-006")]
    public async Task M2_COND_006_history_filters_by_location_and_time_window()
    {
        _factory.Provider = new StubOpenMeteoClient();
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        // Two acquisitions at different locations produce distinct snapshots.
        foreach (var location in new[] { "latitude=6.025&longitude=80.216", "latitude=7.100&longitude=81.200" })
        {
            using var request = Authorized(
                HttpMethod.Get,
                $"/api/marine/current?{location}",
                token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var from = DateTime.UtcNow.AddMinutes(-30).ToString("O");
        var to = DateTime.UtcNow.AddMinutes(30).ToString("O");
        using var filtered = Authorized(
            HttpMethod.Get,
            $"/api/marine/history?latitude=6.025&longitude=80.216&from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}",
            token);
        using var filteredResponse = await client.SendAsync(filtered);
        var filteredBody = await filteredResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        using var document = JsonDocument.Parse(filteredBody);
        var rows = document.RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.Equal(6.025m, row.GetProperty("latitude").GetDecimal());
            Assert.Equal(80.216m, row.GetProperty("longitude").GetDecimal());
        });
    }

    [Fact]
    [Trait("CaseId", "M2-COND-007")]
    public async Task M2_COND_007_condition_data_cannot_be_modified_by_clients()
    {
        using var client = await CreateClientAsync();

        // No POST/PUT/DELETE exists for snapshots; unsafe verbs are rejected
        // by routing rather than mutating provider-derived data.
        using var post = await client.PostAsJsonAsync("/api/marine/current", new { windSpeed = 0 });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);

        using var put = await client.PutAsJsonAsync("/api/marine/current", new { windSpeed = 0 });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);

        using var delete = await client.DeleteAsync("/api/marine/current");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-COND-008")]
    public async Task M2_COND_008_condition_reads_require_authentication_and_permission()
    {
        using var client = await CreateClientAsync();

        using var anonymous = await client.GetAsync("/api/marine/current?latitude=6&longitude=80");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var unauthorizedToken = _factory.CreateToken(MarineSafetyTestSeed.UnauthorizedUserId);
        using var denied = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6&longitude=80",
            unauthorizedToken);
        using var deniedResponse = await client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-COND-009")]
    public async Task M2_COND_009_marine_outage_degrades_marine_fields_and_keeps_weather_evidence()
    {
        // G00 provider-seam rule: weather and marine are independent sources;
        // a marine outage must degrade only marine fields to missing (and
        // therefore UNKNOWN suitability) rather than discarding the weather
        // evidence or failing the whole acquisition.
        _factory.Provider = new StubOpenMeteoClient(
            resultFactory: () => new MarineConditionsResult(
                DateTime.UtcNow,
                DateTime.UtcNow,
                WindSpeed: 15m,
                WaveHeight: null,
                SwellHeight: null,
                Rain: 0m,
                WeatherCode: 0,
                Source: ConditionSources.OpenMeteo,
                MissingFields: ["waveHeight", "swellHeight"]));
        using var client = await CreateClientAsync();

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            ReaderToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(15m, root.GetProperty("windSpeed").GetDecimal());
        Assert.Equal(0m, root.GetProperty("rain").GetDecimal());
        Assert.False(root.TryGetProperty("waveHeight", out _) && root.GetProperty("waveHeight").ValueKind is JsonValueKind.Number);
        Assert.False(root.TryGetProperty("swellHeight", out _) && root.GetProperty("swellHeight").ValueKind is JsonValueKind.Number);
        var missing = root.GetProperty("missingFields").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToHashSet();
        Assert.Subset(missing, new HashSet<string> { "waveHeight", "swellHeight" });
        Assert.Equal("Open-Meteo", root.GetProperty("source").GetString());
    }

    [Fact]
    [Trait("CaseId", "M2-COND-010")]
    public async Task M2_COND_010_naive_query_time_is_interpreted_as_utc()
    {
        // G00 time semantics: an offset-less time is UTC, not the host's
        // local zone. The stub captures what the service actually requested.
        _factory.Provider = new StubOpenMeteoClient();
        using var client = await CreateClientAsync();

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216&time=2026-09-26T08:00:00",
            ReaderToken());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc),
            _factory.Provider.LastRequestedTime);
    }

    [Fact]
    [Trait("CaseId", "M2-COND-011")]
    public async Task M2_COND_011_explicitly_offest_times_are_honored_as_given()
    {
        // An offset-bearing request keeps its absolute instant: 08:00+05:30
        // is 02:30Z, not 08:00Z.
        _factory.Provider = new StubOpenMeteoClient();
        using var client = await CreateClientAsync();

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216&time=2026-09-26T08:00:00%2B05:30",
            ReaderToken());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new DateTime(2026, 9, 26, 2, 30, 0, DateTimeKind.Utc),
            _factory.Provider.LastRequestedTime);
    }

    [Fact]
    [Trait("CaseId", "M2-COND-012")]
    public async Task M2_COND_012_same_location_and_hour_within_the_reuse_window_reuses_the_snapshot()
    {
        _factory.Provider = new StubOpenMeteoClient();
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        using var first = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.025&longitude=80.216",
            token);
        using var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        using var firstDocument = JsonDocument.Parse(firstBody);
        var firstId = firstDocument.RootElement.GetProperty("id").GetGuid();

        using var second = Authorized(
            HttpMethod.Get,
            "/api/marine/current?latitude=6.0253&longitude=80.2161", // rounds to the same 3-dp location
            token);
        using var secondResponse = await client.SendAsync(second);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        using var secondDocument = JsonDocument.Parse(secondBody);
        var secondId = secondDocument.RootElement.GetProperty("id").GetGuid();

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, _factory.Provider.CallCount); // second read came from storage
    }

    [Fact]
    [Trait("CaseId", "M2-COND-013")]
    public async Task M2_COND_013_snapshot_read_of_unknown_id_is_not_found()
    {
        _factory.Provider = new StubOpenMeteoClient();
        using var client = await CreateClientAsync();

        using var request = Authorized(
            HttpMethod.Get,
            $"/api/marine/snapshots/{Guid.NewGuid()}",
            ReaderToken());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-COND-014")]
    public async Task M2_COND_014_history_is_capped_and_ordered_by_forecast_time_descending()

    {
        _factory.Provider = new StubOpenMeteoClient();
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        var baseTime = DateTime.UtcNow;
        var ids = new List<Guid>();
        for (var offsetMinutes = 240; offsetMinutes >= 0; offsetMinutes -= 30)
        {
            var forecast = baseTime.AddMinutes(-offsetMinutes);
            await _factory.SeedAsync(db =>
            {
                db.ConditionSnapshots.Add(new ConditionSnapshot
                {
                    Id = Guid.NewGuid(),
                    Latitude = 6.025m,
                    Longitude = 80.216m,
                    ForecastTime = forecast,
                    RetrievedAt = DateTime.UtcNow,
                    WindSpeed = 10m,
                    WaveHeight = 1m,
                    SwellHeight = 1m,
                    Rain = 0m,
                    WeatherCode = 0,
                    Source = ConditionSources.OpenMeteo,
                    FreshnessStatus = FreshnessStatuses.Fresh,
                    MissingFields = []
                });
                return db.SaveChangesAsync();
            });
        }

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/history?latitude=6.025&longitude=80.216",
            token);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var rows = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(9, rows.Count); // (240/30)+1 seeded rows
        Assert.Equal(
            rows.Select(r => r.GetProperty("forecastTime").GetDateTime()).ToArray(),
            rows.Select(r => r.GetProperty("forecastTime").GetDateTime()).OrderByDescending(t => t).ToArray());
    }

    [Fact]
    [Trait("CaseId", "M2-COND-015")]
    public async Task M2_COND_015_history_reclassifies_stale_rows_at_read_time()
    {
        // A snapshot stored as FRESH with an old retrieval time must be
        // reported STALE by the history endpoint — freshness is a read-time
        // classification, not the stored value.
        _factory.Provider = new StubOpenMeteoClient();
        using var client = await CreateClientAsync();

        await _factory.SeedAsync(db =>
        {
            db.ConditionSnapshots.Add(new ConditionSnapshot
            {
                Id = Guid.NewGuid(),
                Latitude = 6.025m,
                Longitude = 80.216m,
                ForecastTime = DateTime.UtcNow.AddMinutes(-10),
                RetrievedAt = DateTime.UtcNow.AddHours(-4),
                WindSpeed = 10m,
                WaveHeight = 1m,
                SwellHeight = 1m,
                Rain = 0m,
                WeatherCode = 0,
                Source = ConditionSources.OpenMeteo,
                FreshnessStatus = FreshnessStatuses.Fresh,
                MissingFields = []
            });
            return db.SaveChangesAsync();
        });

        using var request = Authorized(
            HttpMethod.Get,
            "/api/marine/history?latitude=6.025&longitude=80.216",
            ReaderToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var rows = document.RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal("STALE", row.GetProperty("freshnessStatus").GetString()));
    }
}
