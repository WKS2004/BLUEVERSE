using System.Net.Http.Headers;

namespace Blueverse.MarineSafety.Tests.Integration;

public sealed class DependencyIsolationTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;
    public DependencyIsolationTests(MarineSafetyWebApplicationFactory factory) => _factory = factory;

    [Theory, Trait("CaseId", "M2-ISOLATION-007")]
    [InlineData("")]
    [InlineData("not-a-service-url")]
    [InlineData("file:///unavailable-catalogue")]
    public async Task Invalid_optional_catalogue_configuration_does_not_prevent_service_readiness(string address)
    {
        using var owner = new MarineSafetyWebApplicationFactory();
        using var app = owner.WithWebHostBuilder(builder => builder.UseSetting("EXPERIENCE_CATALOGUE_URL", address));
        using var client = app.CreateClient();
        using var health = await client.GetAsync("/api/marine/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var named = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IExperienceCatalogueClient));
        Assert.Null(named.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(5), named.Timeout);
    }

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

    [Fact, Trait("CaseId", "M2-ISOLATION-001")]
    public async Task Missing_catalogue_keeps_conditions_saved_records_and_deactivation_independent()
    {
        using var client = await ClientAsync();
        using var evaluated = await client.PostAsJsonAsync("/api/marine/evaluate", new { activityId = MarineSafetyTestSeed.ActivityId, latitude = 6.025m, longitude = 80.216m });
        Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        using var assessment = JsonDocument.Parse(await evaluated.Content.ReadAsStringAsync());
        var assessmentId = assessment.RootElement.GetProperty("assessmentId").GetGuid();
        var snapshotId = assessment.RootElement.GetProperty("snapshotId").GetGuid();
        Guid profileId = Guid.Empty;
        await _factory.SeedAsync(async db => profileId = (await db.SafetyProfiles.SingleAsync()).Id);
        _factory.CatalogueUnavailable = true;
        using var health = await client.GetAsync("/api/marine/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var healthBody = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
        Assert.Equal("healthy", healthBody.RootElement.GetProperty("status").GetString());
        foreach (var path in new[] { "/api/marine/current?latitude=6.025&longitude=80.216", "/api/marine/history", "/api/marine/assessments", $"/api/marine/assessments/{assessmentId}", $"/api/marine/snapshots/{snapshotId}", "/api/marine/safety-profiles", $"/api/marine/safety-profiles/{profileId}" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(body.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object);
            if (path == $"/api/marine/assessments/{assessmentId}")
            {
                Assert.Equal(assessmentId, body.RootElement.GetProperty("id").GetGuid());
                Assert.Equal("COMPLETE", body.RootElement.GetProperty("evidenceCompleteness").GetString());
                Assert.Equal(3, body.RootElement.GetProperty("criteria").GetArrayLength());
            }
        }
        using var deleted = await client.DeleteAsync($"/api/marine/safety-profiles/{profileId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(1, _factory.Provider.CallCount);
        await _factory.SeedAsync(async db =>
        {
            Assert.Single(await db.SuitabilityAssessments.ToListAsync());
            Assert.Single(await db.ConditionSnapshots.ToListAsync());
            var profile = await db.SafetyProfiles.SingleAsync();
            Assert.False(profile.IsActive);
            Assert.NotNull(profile.EffectiveTo);
            Assert.Equal(MarineSafetyTestSeed.ReviewerUserId, profile.ReviewedByUserId);
        });
    }

    [Theory, Trait("CaseId", "M2-ISOLATION-002")]
    [InlineData("activities")]
    [InlineData("activity")]
    [InlineData("evaluate")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("review")]
    public async Task Dependent_operations_report_missing_data_without_writing_domain_results(string operation)
    {
        using var client = await ClientAsync();
        Guid profileId = Guid.Empty;
        var draftId = Guid.NewGuid();
        await _factory.SeedAsync(async db =>
        {
            var original = await db.SafetyProfiles.SingleAsync();
            profileId = original.Id;
            db.SafetyProfiles.Add(new SafetyProfile
            {
                Id = draftId, ActivityId = original.ActivityId, Version = 2, MaxWindSpeed = 25,
                MaxWaveHeight = 1.5m, MaxSwellHeight = 1.2m, CreatedByUserId = MarineSafetyTestSeed.ManagerUserId,
                WindCriteriaSource = original.WindCriteriaSource, WindCriteriaRationale = original.WindCriteriaRationale,
                WaveCriteriaSource = original.WaveCriteriaSource, WaveCriteriaRationale = original.WaveCriteriaRationale,
                SwellCriteriaSource = original.SwellCriteriaSource, SwellCriteriaRationale = original.SwellCriteriaRationale,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
        _factory.CatalogueUnavailable = true;
        if (operation == "review") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken(MarineSafetyTestSeed.ReviewerUserId));
        using var response = operation switch
        {
            "activities" => await client.GetAsync("/api/marine/activities"),
            "activity" => await client.GetAsync($"/api/marine/activities/{MarineSafetyTestSeed.ActivityId}"),
            "evaluate" => await client.PostAsJsonAsync("/api/marine/evaluate", new { activityId = MarineSafetyTestSeed.ActivityId, latitude = 6.025m, longitude = 80.216m }),
            "create" => await client.PostAsJsonAsync("/api/marine/safety-profiles", MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId)),
            "update" => await client.PutAsJsonAsync($"/api/marine/safety-profiles/{profileId}", MarineSafetyTestSeed.ValidProfilePayload()),
            _ => await client.PostAsync($"/api/marine/safety-profiles/{draftId}/review", null)
        };
        await AssertUnavailable(response, "experience-biodiversity", "ACTIVITY_DATA_UNAVAILABLE", "activityCatalogue", "5");
        Assert.Equal(0, _factory.Provider.CallCount);
        await _factory.SeedAsync(async db =>
        {
            Assert.Empty(await db.SuitabilityAssessments.ToListAsync());
            Assert.Empty(await db.ConditionSnapshots.ToListAsync());
            Assert.Equal(2, await db.SafetyProfiles.CountAsync());
            Assert.True((await db.SafetyProfiles.SingleAsync(p => p.Id == profileId)).IsActive);
            Assert.Null((await db.SafetyProfiles.SingleAsync(p => p.Id == draftId)).ReviewedAt);
        });
    }

    [Fact, Trait("CaseId", "M2-ISOLATION-003")]
    public async Task Empty_catalogue_is_success_and_outage_recovers_without_restart()
    {
        using var client = await ClientAsync();
        _factory.CatalogueUnavailable = true;
        using var failed = await client.GetAsync("/api/marine/activities");
        await AssertUnavailable(failed, "experience-biodiversity", "ACTIVITY_DATA_UNAVAILABLE", "activityCatalogue", "5");
        _factory.CatalogueUnavailable = false;
        _factory.CatalogueOverride = [];
        using var empty = await client.GetAsync("/api/marine/activities");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Null(empty.Headers.RetryAfter);
        Assert.Empty((await empty.Content.ReadFromJsonAsync<MarineActivityDto[]>())!);
        _factory.CatalogueOverride = null;
        using var recovered = await client.GetAsync("/api/marine/activities");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Contains((await recovered.Content.ReadFromJsonAsync<MarineActivityDto[]>())!, activity => activity.Id == MarineSafetyTestSeed.ActivityId);
    }

    [Fact, Trait("CaseId", "M2-ISOLATION-004")]
    public async Task Provider_outage_reports_missing_conditions_while_saved_profile_reads_work()
    {
        using var client = await ClientAsync();
        _factory.Provider = StubOpenMeteoClient.Unavailable();
        using var current = await client.GetAsync("/api/marine/current?latitude=6.025&longitude=80.216");
        await AssertUnavailable(current, "open-meteo", "CONDITION_DATA_UNAVAILABLE", "windSpeed", "60");
        using var evaluated = await client.PostAsJsonAsync("/api/marine/evaluate", new { activityId = MarineSafetyTestSeed.ActivityId, latitude = 6.025m, longitude = 80.216m });
        await AssertUnavailable(evaluated, "open-meteo", "CONDITION_DATA_UNAVAILABLE", "waveHeight", "60");
        using var profiles = await client.GetAsync("/api/marine/safety-profiles");
        Assert.Equal(HttpStatusCode.OK, profiles.StatusCode);
        Assert.Single((await profiles.Content.ReadFromJsonAsync<SafetyProfileDto[]>())!);
        await _factory.SeedAsync(async db =>
        {
            Assert.Empty(await db.SuitabilityAssessments.ToListAsync());
            Assert.Empty(await db.ConditionSnapshots.ToListAsync());
        });
    }

    private static async Task AssertUnavailable(HttpResponseMessage response, string dependency, string code, string missing, string retry)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(retry, response.Headers.RetryAfter?.ToString());
        var json = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(json);
        Assert.Equal("UNAVAILABLE", body.RootElement.GetProperty("availability").GetString());
        Assert.Equal(dependency, body.RootElement.GetProperty("dependency").GetString());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.Contains(body.RootElement.GetProperty("missingFields").EnumerateArray(), field => field.GetString() == missing);
        Assert.True(body.RootElement.GetProperty("retryable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("data").ValueKind);
        Assert.DoesNotContain("not yet", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact, Trait("CaseId", "M2-ISOLATION-009")]
    public async Task OpenApi_declares_the_structured_unavailable_contract_for_data_dependent_operations()
    {
        using var client = await ClientAsync();
        using var response = await client.GetAsync("/api/marine/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schema = body.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(nameof(DependencyUnavailableProblemDetails));
        foreach (var field in new[] { "availability", "dependency", "code", "reason", "missingFields", "retryable", "data" })
            Assert.True(schema.GetProperty("properties").TryGetProperty(field, out _), $"Missing unavailable field {field}");
        foreach (var (path, method) in new[] { ("/api/marine/activities", "get"), ("/api/marine/current", "get"),
            ("/api/marine/evaluate", "post"), ("/api/marine/safety-profiles", "post"),
            ("/api/marine/safety-profiles/{id}", "put"), ("/api/marine/safety-profiles/{id}/review", "post") })
            Assert.Contains(nameof(DependencyUnavailableProblemDetails), body.RootElement.GetProperty("paths").GetProperty(path)
                .GetProperty(method).GetProperty("responses").GetProperty("503").GetRawText());
    }
}
