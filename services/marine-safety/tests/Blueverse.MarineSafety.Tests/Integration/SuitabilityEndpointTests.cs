namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Requirement-based suitability tests (M2-SUIT-*). Expectations come from
/// the deterministic rule contract: all required limits respected means
/// SUITABLE, any required factor above its limit means UNSUITABLE, missing or
/// stale evidence means UNKNOWN, and missing configuration is a client error —
/// never a fabricated positive result.
/// </summary>
public sealed class SuitabilityEndpointTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;

    public SuitabilityEndpointTests(MarineSafetyWebApplicationFactory factory)
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

    private static HttpContent EvaluateBody(
        decimal windSpeed,
        decimal? waveHeight,
        decimal? swellHeight,
        Guid? activityId = null,
        string? time = null) =>
        JsonContent.Create(new
        {
            activityId = activityId ?? MarineSafetyTestSeed.ActivityId,
            latitude = 6.025m,
            longitude = 80.216m,
            dateTime = time
        });

    private async Task<HttpResponseMessage> EvaluateAsync(
        HttpClient client,
        string token,
        decimal windSpeed,
        decimal? waveHeight = 0.8m,
        decimal? swellHeight = 0.7m,
        Guid? activityId = null,
        string? time = null,
        DateTime? retrievedAt = null)
    {
        _factory.Provider = new StubOpenMeteoClient(() => new MarineConditionsResult(
            DateTime.UtcNow,
            retrievedAt ?? DateTime.UtcNow,
            windSpeed,
            waveHeight,
            swellHeight,
            Rain: 0m,
            WeatherCode: 0,
            Source: ConditionSources.OpenMeteo,
            MissingFields: (waveHeight.HasValue, swellHeight.HasValue) switch
            {
                (true, true) => [],
                (true, false) => ["swellHeight"],
                (false, true) => ["waveHeight"],
                _ => ["waveHeight", "swellHeight"]
            }));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/marine/evaluate")
        {
            Content = JsonContent.Create(new
            {
                activityId = activityId ?? MarineSafetyTestSeed.ActivityId,
                latitude = 6.025m,
                longitude = 80.216m,
                dateTime = time
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-001")]
    public async Task M2_SUIT_001_conditions_within_all_limits_are_suitable()
    {
        using var client = await CreateClientAsync();

        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 15m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("SUITABLE", root.GetProperty("status").GetString());
        Assert.Equal(MarineSafetyTestSeed.ActivityId, root.GetProperty("activityId").GetGuid());
        Assert.Equal("Open-Meteo", root.GetProperty("source").GetString());
        Assert.Equal("FRESH", root.GetProperty("freshness").GetString());
        Assert.Equal(15m, root.GetProperty("conditions").GetProperty("windSpeed").GetDecimal());
        Assert.Equal(0.8m, root.GetProperty("conditions").GetProperty("waveHeight").GetDecimal());
        Assert.Empty(root.GetProperty("violations").EnumerateArray());
        Assert.Empty(root.GetProperty("cautionFactors").EnumerateArray());
        Assert.Empty(root.GetProperty("missingFields").EnumerateArray());

        // The assessment is persisted as history with the applied profile version.
        var assessmentId = root.GetProperty("assessmentId").GetGuid();
        Assert.NotEqual(Guid.Empty, assessmentId);
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-002")]
    public async Task M2_SUIT_002_wind_above_maximum_is_unsuitable()
    {
        using var client = await CreateClientAsync();

        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 40m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("UNSUITABLE", root.GetProperty("status").GetString());
        var violations = root.GetProperty("violations").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains(violations, violation => violation.StartsWith("windSpeed", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-003")]
    public async Task M2_SUIT_003_wave_above_maximum_is_unsuitable()
    {
        using var client = await CreateClientAsync();

        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 10m, waveHeight: 2.0m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("UNSUITABLE", root.GetProperty("status").GetString());
        var violations = root.GetProperty("violations").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains(violations, violation => violation.StartsWith("waveHeight", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-004")]
    public async Task M2_SUIT_004_missing_required_field_yields_unknown()
    {
        using var client = await CreateClientAsync();

        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 15m, swellHeight: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("UNKNOWN", root.GetProperty("status").GetString());
        var missing = root.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains("swellHeight", missing);
        Assert.Empty(root.GetProperty("violations").EnumerateArray());
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-005")]
    public async Task M2_SUIT_005_stale_evidence_yields_unknown()
    {
        using var client = await CreateClientAsync();

        // Retrieved long before the evaluation moment: outside the configured
        // freshness window, the evidence cannot support a positive decision.
        using var response = await EvaluateAsync(
            client,
            ReaderToken(),
            windSpeed: 15m,
            retrievedAt: DateTime.UtcNow.AddHours(-3));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("UNKNOWN", root.GetProperty("status").GetString());
        Assert.Equal("STALE", root.GetProperty("freshness").GetString());
        var missing = root.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains("freshConditions", missing);
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-006")]
    public async Task M2_SUIT_006_activity_without_a_profile_is_rejected_not_treated_as_safe()
    {
        using var client = await CreateClientAsync();

        using var response = await EvaluateAsync(
            client,
            ReaderToken(),
            windSpeed: 15m,
            activityId: MarineSafetyTestSeed.UnprofiledActivityId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("No Safety Profile Configured", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-007")]
    public async Task M2_SUIT_007_unknown_activity_is_not_found()
    {
        using var client = await CreateClientAsync();

        using var response = await EvaluateAsync(
            client,
            ReaderToken(),
            windSpeed: 15m,
            activityId: Guid.NewGuid());
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Activity Not Found", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-008")]
    public async Task M2_SUIT_008_invalid_request_body_is_bad_request()
    {
        using var client = await CreateClientAsync();

        var token = ReaderToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/marine/evaluate")
        {
            Content = JsonContent.Create(new { latitude = 6.025m, longitude = 80.216m })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var missingActivity = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, missingActivity.StatusCode);

        var outOfRange = new HttpRequestMessage(HttpMethod.Post, "/api/marine/evaluate")
        {
            Content = JsonContent.Create(new
            {
                activityId = MarineSafetyTestSeed.ActivityId,
                latitude = 123.5m,
                longitude = 80.216m
            })
        };
        outOfRange.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var outOfRangeResponse = await client.SendAsync(outOfRange);
        Assert.Equal(HttpStatusCode.BadRequest, outOfRangeResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-009")]
    public async Task M2_SUIT_009_evaluation_requires_authentication_and_read_grant()
    {
        using var client = await CreateClientAsync();

        using var anonymous = await client.PostAsJsonAsync("/api/marine/evaluate", new
        {
            activityId = MarineSafetyTestSeed.ActivityId,
            latitude = 6.025m,
            longitude = 80.216m
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var deniedToken = _factory.CreateToken(MarineSafetyTestSeed.UnauthorizedUserId);
        var denied = new HttpRequestMessage(HttpMethod.Post, "/api/marine/evaluate")
        {
            Content = JsonContent.Create(new
            {
                activityId = MarineSafetyTestSeed.ActivityId,
                latitude = 6.025m,
                longitude = 80.216m
            })
        };
        denied.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deniedToken);
        using var deniedResponse = await client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-010")]
    public async Task M2_SUIT_010_caution_band_produces_caution_when_configured()
    {
        using var client = await CreateClientAsync();

        // Seeded profile: caution wind band 20 km/h below the 25 km/h maximum.
        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 22m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("CAUTION", root.GetProperty("status").GetString());
        var cautionFactors = root.GetProperty("cautionFactors").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains("windSpeed", cautionFactors);
        Assert.Empty(root.GetProperty("violations").EnumerateArray());
    }
}
