using System.Net.Http.Headers;
using Blueverse.MarineSafety.Services;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.MarineSafety.Tests.Integration;

public sealed class BranchCorrectionTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;
    public BranchCorrectionTests(MarineSafetyWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientAsync()
    {
        _factory.ResetDatabase();
        _factory.Provider = new StubOpenMeteoClient();
        var client = _factory.CreateClient();
        await _factory.SeedAsync(db => MarineSafetyTestSeed.SeedIdentitiesAsync(db, _factory.Resolver));
        await _factory.SeedAsync(MarineSafetyTestSeed.SeedConfigurationAsync);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId));
        return client;
    }

    [Theory]
    [InlineData("0001-01-01T00:00:00Z", false)]
    [InlineData("9999-12-31T23:59:59.9999999Z", false)]
    [InlineData("0001-01-01T00:00:00Z", true)]
    [InlineData("9999-12-31T23:59:59.9999999Z", true)]
    [Trait("CaseId", "M2-REPAIR-TIME-001")]
    public async Task Extreme_requested_times_return_problem_without_side_effects(string time, bool evaluate)
    {
        using var client = await ClientAsync();
        using var response = evaluate
            ? await client.PostAsJsonAsync("/api/marine/evaluate", new { activityId = MarineSafetyTestSeed.ActivityId, latitude = 6.025m, longitude = 80.216m, dateTime = time })
            : await client.GetAsync($"/api/marine/current?latitude=6.025&longitude=80.216&time={Uri.EscapeDataString(time)}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid requested time", body.RootElement.GetProperty("title").GetString());
        Assert.Contains("Requested time must be between", body.RootElement.GetProperty("detail").GetString());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(0, _factory.Provider.CallCount);
        await _factory.SeedAsync(async db =>
        {
            Assert.Empty(await db.ConditionSnapshots.ToListAsync());
            Assert.Empty(await db.SuitabilityAssessments.ToListAsync());
        });
    }

    [Theory]
    [InlineData("maxWindSpeed")]
    [InlineData("maxWaveHeight")]
    [InlineData("maxSwellHeight")]
    [InlineData("cautionWindSpeed")]
    [InlineData("cautionWaveHeight")]
    [InlineData("cautionSwellHeight")]
    [Trait("CaseId", "M2-REPAIR-PRECISION-001")]
    public async Task Excess_precision_is_rejected_on_create_and_update_without_changing_history(string field)
    {
        using var client = await ClientAsync();
        Guid approvedId = Guid.Empty;
        await _factory.SeedAsync(async db => approvedId = (await db.SafetyProfiles.SingleAsync(p => p.ActivityId == MarineSafetyTestSeed.ActivityId)).Id);
        foreach (var update in new[] { false, true })
        {
            var payload = MarineSafetyTestSeed.ValidProfilePayload(update ? null : MarineSafetyTestSeed.ActivityId);
            payload[field] = 1.003m;
            // Keep caution strictly below its maximum independently of the precision rule.
            if (field.StartsWith("caution")) payload[field.Replace("caution", "max")] = 2m;
            using var response = update
                ? await client.PutAsJsonAsync($"/api/marine/safety-profiles/{approvedId}", payload)
                : await client.PostAsJsonAsync("/api/marine/safety-profiles", payload);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Contains("two decimal places", body.RootElement.GetProperty("detail").GetString());
            await _factory.SeedAsync(async db =>
            {
                var original = Assert.Single(await db.SafetyProfiles.ToListAsync());
                Assert.Equal(approvedId, original.Id);
                Assert.True(original.IsActive);
                Assert.Equal(1, original.Version);
                Assert.Equal(1.5m, original.MaxWaveHeight);
                Assert.Equal(MarineSafetyTestSeed.ReviewerUserId, original.ReviewedByUserId);
            });
        }
    }

    [Fact]
    [Trait("CaseId", "M2-REPAIR-PRECISION-002")]
    public async Task Exact_two_decimal_limits_and_trailing_zeroes_preserve_values_and_audit()
    {
        using var client = await ClientAsync();
        var payload = MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.UnprofiledActivityId,
            maxWindSpeed: 20.01m, maxWaveHeight: 1.01m, maxSwellHeight: 1.000m,
            cautionWaveHeight: 1.00m);
        using var response = await client.PostAsJsonAsync("/api/marine/safety-profiles", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        Assert.EndsWith(id.ToString(), response.Headers.Location?.ToString());
        await _factory.SeedAsync(async db =>
        {
            var draft = await db.SafetyProfiles.SingleAsync(p => p.Id == id);
            Assert.Equal(1.01m, draft.MaxWaveHeight);
            Assert.Equal(1.00m, draft.CautionWaveHeight);
            Assert.Equal(1m, draft.MaxSwellHeight);
            Assert.Equal(MarineSafetyTestSeed.ManagerUserId, draft.CreatedByUserId);
            Assert.Null(draft.ReviewedAt);
            Assert.False(draft.IsActive);
        });
    }

    [Theory]
    [InlineData(-60, false)]
    [InlineData(-31, false)]
    [InlineData(-30, true)]
    [InlineData(0, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    [Trait("CaseId", "M2-REPAIR-CACHE-001")]
    public async Task Cache_matches_provider_forecast_window(int forecastOffsetMinutes, bool reusable)
    {
        using var client = await ClientAsync();
        var requested = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);
        var original = Snapshot(requested.AddMinutes(forecastOffsetMinutes), DateTime.UtcNow.AddMinutes(-1));
        await _factory.SeedAsync(async db => { db.ConditionSnapshots.Add(original); await db.SaveChangesAsync(); });
        using var response = await client.GetAsync($"/api/marine/current?latitude=6.025&longitude=80.216&time={Uri.EscapeDataString(requested.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualId = body.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(reusable, actualId == original.Id);
        Assert.Equal(reusable ? 0 : 1, _factory.Provider.CallCount);
        await _factory.SeedAsync(async db =>
        {
            Assert.Equal(reusable ? 1 : 2, await db.ConditionSnapshots.CountAsync());
            var stored = await db.ConditionSnapshots.SingleAsync(s => s.Id == original.Id);
            Assert.Equal(original.ForecastTime, stored.ForecastTime);
            Assert.Equal(original.RetrievedAt, stored.RetrievedAt);
            Assert.Equal(0.8m, stored.WaveHeight);
        });
    }

    [Theory]
    [InlineData(-16)]
    [InlineData(1)]
    [Trait("CaseId", "M2-REPAIR-CACHE-002")]
    public async Task Stale_or_future_retrieval_is_not_reused(int retrievalOffsetMinutes)
    {
        using var client = await ClientAsync();
        var requested = DateTime.UtcNow;
        var original = Snapshot(requested, DateTime.UtcNow.AddMinutes(retrievalOffsetMinutes));
        await _factory.SeedAsync(async db => { db.ConditionSnapshots.Add(original); await db.SaveChangesAsync(); });
        using var response = await client.GetAsync($"/api/marine/current?latitude=6.025&longitude=80.216&time={Uri.EscapeDataString(requested.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEqual(original.Id, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(1, _factory.Provider.CallCount);
        await _factory.SeedAsync(async db => Assert.Equal(2, await db.ConditionSnapshots.CountAsync()));
    }

    [Fact]
    [Trait("CaseId", "M2-REPAIR-CACHE-003")]
    public async Task Chronologically_first_applicable_forecast_wins_over_newer_outside_window()
    {
        using var client = await ClientAsync();
        var requested = DateTime.UtcNow;
        var expected = Snapshot(requested.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-2));
        await _factory.SeedAsync(async db =>
        {
            db.ConditionSnapshots.AddRange(expected,
                Snapshot(requested.AddMinutes(-60), DateTime.UtcNow.AddMinutes(-1)),
                Snapshot(requested.AddMinutes(30), DateTime.UtcNow.AddMinutes(-1)));
            await db.SaveChangesAsync();
        });
        using var response = await client.GetAsync($"/api/marine/current?latitude=6.025&longitude=80.216&time={Uri.EscapeDataString(requested.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expected.Id, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(0, _factory.Provider.CallCount);
    }

    private static ConditionSnapshot Snapshot(DateTime forecast, DateTime retrieved) => new()
    {
        Id = Guid.NewGuid(), Latitude = 6.025m, Longitude = 80.216m,
        ForecastTime = forecast, RetrievedAt = retrieved,
        WindSpeed = 15m, WaveHeight = 0.8m, SwellHeight = 0.7m,
        Source = ConditionSources.OpenMeteo, MissingFields = [], FreshnessStatus = FreshnessStatuses.Fresh
    };

    [Fact]
    [Trait("CaseId", "M2-REPAIR-TIME-002")]
    public void Request_range_has_safe_inclusive_arithmetic_boundaries()
    {
        var minimum = DateTime.SpecifyKind(DateTime.MinValue.AddHours(1), DateTimeKind.Utc);
        var maximum = DateTime.SpecifyKind(DateTime.MaxValue.AddHours(-1), DateTimeKind.Utc);
        foreach (var time in new[] { minimum, maximum })
        {
            Assert.Equal(time, MarineTime.RequireRequested(time));
            var window = MarineTime.ForecastWindow(time);
            Assert.Equal(time.AddMinutes(-30), window.Earliest);
            Assert.Equal(time.AddHours(1), window.Latest);
            Assert.Equal(DateTimeKind.Utc, window.Latest.Kind);
        }
        Assert.Throws<MarineTimeValidationException>(() => MarineTime.RequireRequested(minimum.AddTicks(-1)));
        Assert.Throws<MarineTimeValidationException>(() => MarineTime.RequireRequested(maximum.AddTicks(1)));
    }
}
