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

    [Fact]
    [Trait("CaseId", "M2-SUIT-011")]
    public async Task M2_SUIT_011_naive_request_time_is_interpreted_as_utc()
    {
        // G00 time semantics: a timestamp supplied without an offset is UTC,
        // never the host machine's local time zone. In a UTC+5:30 host a
        // naive local interpretation would shift the requested hour by 5.5
        // hours and the reported requestedTime would not echo the input.
        using var client = await CreateClientAsync();

        var requested = new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Unspecified);
        using var response = await EvaluateAsync(
            client,
            ReaderToken(),
            windSpeed: 15m,
            time: requested.ToString("yyyy-MM-ddTHH:mm:ss"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var echoed = root.GetProperty("requestedTime").GetDateTime();
        Assert.Equal(TimeSpan.Zero, echoed - requested);
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-012")]
    public async Task M2_SUIT_012_missing_rain_is_disclosed_without_inventing_a_rain_rule()
    {
        // Rain is acquired and disclosed but no profile rule consumes it
        // (profiles configure wind/wave/swell limits only), so its absence is
        // reported in missingFields while the limit-bearing decision stands.
        // Forcing UNKNOWN here would require an implicit rain rule, which the
        // frozen rule vocabulary prohibits.
        using var client = await CreateClientAsync();

        _factory.Provider = new StubOpenMeteoClient(() => new MarineConditionsResult(
            DateTime.UtcNow,
            DateTime.UtcNow,
            WindSpeed: 15m,
            WaveHeight: 0.8m,
            SwellHeight: 0.7m,
            Rain: null,
            WeatherCode: 0,
            Source: ConditionSources.OpenMeteo,
            MissingFields: ["rain"]));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/marine/evaluate")
        {
            Content = JsonContent.Create(new
            {
                activityId = MarineSafetyTestSeed.ActivityId,
                latitude = 6.025m,
                longitude = 80.216m
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ReaderToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("SUITABLE", root.GetProperty("status").GetString());
        var missing = root.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains("rain", missing);
        Assert.Null(root.GetProperty("conditions").GetProperty("rain").GetString());
        Assert.Empty(root.GetProperty("violations").EnumerateArray());
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-013")]
    public async Task M2_SUIT_013_multiple_violations_are_all_reported()
    {
        using var client = await CreateClientAsync();

        // Wind 40 exceeds 25; wave 2.0 exceeds 1.5; swell 2 exceeds 1.2.
        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 40m, waveHeight: 2.0m, swellHeight: 2m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("UNSUITABLE", root.GetProperty("status").GetString());
        var violations = root.GetProperty("violations").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains(violations, v => v.StartsWith("windSpeed", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.StartsWith("waveHeight", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.StartsWith("swellHeight", StringComparison.Ordinal));
        Assert.Equal(3, violations.Count);
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-014")]
    public async Task M2_SUIT_014_violations_decide_the_status_and_caution_factors_stay_reported()
    {
        // Wind 22 sits in its caution band, wave 2.0 hard-violates: the status
        // is UNSUITABLE (violations decide), while the caution factor remains
        // reported as the evidence record documents — the two arrays are
        // independent facts, not mutually exclusive states.
        using var client = await CreateClientAsync();

        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 22m, waveHeight: 2.0m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("UNSUITABLE", root.GetProperty("status").GetString());
        var cautionFactors = root.GetProperty("cautionFactors").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains("windSpeed", cautionFactors);
        Assert.DoesNotContain("waveHeight", cautionFactors);
        var violations = root.GetProperty("violations").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains(violations, v => v.StartsWith("waveHeight", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-015")]
    public async Task M2_SUIT_015_wave_caution_band_produces_caution()
    {
        using var client = await CreateClientAsync();

        // Seeded profile: caution wave 1.0 below max 1.5.
        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 10m, waveHeight: 1.2m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("CAUTION", root.GetProperty("status").GetString());
        var cautionFactors = root.GetProperty("cautionFactors").EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.Contains("waveHeight", cautionFactors);
        Assert.DoesNotContain("windSpeed", cautionFactors);
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-016")]
    public async Task M2_SUIT_016_value_equal_to_the_limit_is_suitable_on_a_strict_profile()
    {
        // The contract says only greater-than violates; values exactly at the
        // limit are within it. A strict profile (no caution bands) isolates
        // the boundary semantics from band behavior.
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        var strictActivityId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.MarineActivities.Add(new MarineActivity
            {
                Id = strictActivityId,
                Name = "Strict Boundary Test " + Guid.NewGuid().ToString("N"),
                ActivityType = "Diving",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            return db.SaveChangesAsync();
        });

        using var create = AuthorizedJson(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(strictActivityId, 25m, 1.5m, 1.2m));
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createDocument = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var draftId = createDocument.RootElement.GetProperty("id").GetGuid();
        using var review = new HttpRequestMessage(HttpMethod.Post, $"/api/marine/safety-profiles/{draftId}/review");
        review.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", _factory.CreateToken(MarineSafetyTestSeed.ReviewerUserId));
        using var reviewResponse = await client.SendAsync(review);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);

        _factory.Provider = new StubOpenMeteoClient(() => new MarineConditionsResult(
            DateTime.UtcNow,
            DateTime.UtcNow,
            WindSpeed: 25m,
            WaveHeight: 1.5m,
            SwellHeight: 1.2m,
            Rain: 0m,
            WeatherCode: 0,
            Source: ConditionSources.OpenMeteo,
            MissingFields: []));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/marine/evaluate")
        {
            Content = JsonContent.Create(new
            {
                activityId = strictActivityId,
                latitude = 6.025m,
                longitude = 80.216m
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ReaderToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("SUITABLE", root.GetProperty("status").GetString());
        Assert.Empty(root.GetProperty("violations").EnumerateArray());
        Assert.Empty(root.GetProperty("cautionFactors").EnumerateArray());
    }

    private static HttpRequestMessage AuthorizedJson(HttpMethod method, string uri, string token, object body)
    {
        var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-017")]
    public async Task M2_SUIT_017_assessment_is_persisted_with_full_provenance()
    {
        using var client = await CreateClientAsync();

        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 30m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var assessmentId = root.GetProperty("assessmentId").GetGuid();
        var snapshotId = root.GetProperty("snapshotId").GetGuid();

        // The complete evidence chain is stored: profile version, result,
        // violations, source, freshness and both evidence references.
        await _factory.SeedAsync(async db =>
        {
            var record = await db.SuitabilityAssessments.SingleAsync(a => a.Id == assessmentId);
            Assert.Equal(MarineSafetyTestSeed.ActivityId, record.ActivityId);
            Assert.Equal(1, record.ProfileVersion);
            Assert.Equal(6.025m, record.Latitude);
            Assert.Equal(80.216m, record.Longitude);
            Assert.Equal(SuitabilityResults.Unsuitable, record.Result);
            Assert.Contains(record.Violations, v => v.StartsWith("windSpeed", StringComparison.Ordinal));
            Assert.Equal(ConditionSources.OpenMeteo, record.Source);
            Assert.Equal(FreshnessStatuses.Fresh, record.FreshnessStatus);
            Assert.Equal(snapshotId, record.ConditionSnapshotId);
            Assert.NotEqual(Guid.Empty, record.SafetyProfileId);
            await Task.CompletedTask;
        });
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-018")]
    public async Task M2_SUIT_018_inactive_activity_cannot_be_evaluated()
    {
        using var client = await CreateClientAsync();

        // Inactive activities are rejected exactly like unknown ones: without
        // a profile gate they would otherwise still consume provider calls.
        await _factory.SeedAsync(async db =>
        {
            var activity = await db.MarineActivities.SingleAsync(a => a.Id == MarineSafetyTestSeed.ActivityId);
            activity.IsActive = false;
            await db.SaveChangesAsync();
        });

        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 15m);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Activity Not Found", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(0, _factory.Provider.CallCount); // rejected before any provider call
    }

    [Fact]
    [Trait("CaseId", "M2-SUIT-019")]
    public async Task M2_SUIT_019_evaluation_is_blocked_until_a_draft_has_been_reviewed()
    {
        using var client = await CreateClientAsync();
        var managerToken = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);
        using var create = AuthorizedJson(HttpMethod.Post, "/api/marine/safety-profiles", managerToken,
            MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, 24m, 1.4m, 1.1m));
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        _factory.Provider = new StubOpenMeteoClient(() => new MarineConditionsResult(
            DateTime.UtcNow, DateTime.UtcNow, 10m, 0.8m, 0.7m, 0m, 0,
            ConditionSources.OpenMeteo, []));
        using var response = await EvaluateAsync(client, ReaderToken(), windSpeed: 10m);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Safety Profile Review Required", body.RootElement.GetProperty("title").GetString());
        Assert.Contains("review", body.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _factory.Provider.CallCount);
    }
}
