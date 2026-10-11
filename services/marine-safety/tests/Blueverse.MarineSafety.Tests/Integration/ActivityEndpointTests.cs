namespace Blueverse.MarineSafety.Tests.Integration;

// User-approved correction of M2-ACT: Member 1 owns writes; Marine consumes published IDs.
public sealed class ActivityEndpointTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;
    public ActivityEndpointTests(MarineSafetyWebApplicationFactory factory) => _factory = factory;
    private async Task<HttpClient> ClientAsync(bool manager = false)
    {
        _factory.ResetDatabase();
        _factory.Provider = new StubOpenMeteoClient();
        var client = _factory.CreateClient();
        await _factory.SeedAsync(db => MarineSafetyTestSeed.SeedIdentitiesAsync(db, _factory.Resolver));
        await _factory.SeedAsync(MarineSafetyTestSeed.SeedConfigurationAsync);
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.CreateToken(manager ? MarineSafetyTestSeed.ManagerUserId : MarineSafetyTestSeed.ReaderUserId));
        return client;
    }
    private static MarineActivityDto Published(Guid id, string name = "Canonical surfing") => new(id, name, "Surfing", true, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

    [Fact, Trait("CaseId", "M2-ACT-001")]
    public async Task M2_ACT_001_catalogue_creation_is_not_owned_by_marine()
    {
        using var client = await ClientAsync(true);
        using var response = await client.PostAsJsonAsync("/api/marine/activities", new { name = "Local duplicate", activityType = "Surfing" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("GET", response.Content.Headers.Allow);
        await _factory.SeedAsync(async db => Assert.False(await db.MarineActivities.AnyAsync(a => a.Name == "Local duplicate")));
    }
    [Fact, Trait("CaseId", "M2-ACT-002")]
    public async Task M2_ACT_002_distinct_canonical_ids_can_share_a_display_name()
    {
        using var client = await ClientAsync();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        _factory.CatalogueOverride = [Published(first), Published(second)];
        var rows = await client.GetFromJsonAsync<List<MarineActivityDto>>("/api/marine/activities");
        Assert.Equal(new[] { first, second }, rows!.Select(a => a.Id));
        Assert.All(rows!, row => Assert.Equal("Canonical surfing", row.Name));
    }
    [Fact, Trait("CaseId", "M2-ACT-003")]
    public async Task M2_ACT_003_unavailable_catalogue_never_returns_cached_placeholder_rows()
    {
        using var client = await ClientAsync();
        _factory.CatalogueUnavailable = true;
        using var response = await client.GetAsync("/api/marine/activities");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("5", response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ToString());
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(503, payload.GetProperty("status").GetInt32());
        Assert.Contains("catalogue", payload.GetProperty("detail").GetString());
        using var history = await client.GetAsync("/api/marine/assessments");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
    }
    [Fact, Trait("CaseId", "M2-ACT-004")]
    public async Task M2_ACT_004_reader_lists_published_rows_and_inactive_filter_is_empty()
    {
        using var client = await ClientAsync();
        var id = Guid.NewGuid(); _factory.CatalogueOverride = [Published(id)];
        var rows = await client.GetFromJsonAsync<List<MarineActivityDto>>("/api/marine/activities?isActive=true");
        Assert.Equal(id, Assert.Single(rows!).Id);
        Assert.Empty((await client.GetFromJsonAsync<List<MarineActivityDto>>("/api/marine/activities?isActive=false"))!);
    }
    [Fact, Trait("CaseId", "M2-ACT-005")]
    public async Task M2_ACT_005_get_by_canonical_id_and_unknown_id()
    {
        using var client = await ClientAsync();
        var id = Guid.NewGuid(); _factory.CatalogueOverride = [Published(id)];
        var row = await client.GetFromJsonAsync<MarineActivityDto>($"/api/marine/activities/{id}");
        Assert.Equal(id, row!.Id); Assert.True(row.IsActive);
        using var missing = await client.GetAsync($"/api/marine/activities/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
    [Fact, Trait("CaseId", "M2-ACT-006")]
    public async Task M2_ACT_006_marine_cannot_rename_the_canonical_activity()
    {
        using var client = await ClientAsync(true);
        var row = (await client.GetFromJsonAsync<List<MarineActivityDto>>("/api/marine/activities"))!.First();
        using var response = await client.PutAsJsonAsync($"/api/marine/activities/{row.Id}", new { name = "Renamed", activityType = "Other" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        var unchanged = await client.GetFromJsonAsync<MarineActivityDto>($"/api/marine/activities/{row.Id}");
        Assert.Equal(row, unchanged);
    }
    [Fact, Trait("CaseId", "M2-ACT-007")]
    public async Task M2_ACT_007_empty_catalogue_is_an_empty_success_not_a_synthetic_default()
    {
        using var client = await ClientAsync(); _factory.CatalogueOverride = [];
        using var response = await client.GetAsync("/api/marine/activities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<MarineActivityDto>>())!);
    }
    [Fact, Trait("CaseId", "M2-ACT-008")]
    public async Task M2_ACT_008_marine_cannot_delete_catalogue_rows_or_profile_references()
    {
        using var client = await ClientAsync(true);
        var row = (await client.GetFromJsonAsync<List<MarineActivityDto>>("/api/marine/activities"))!.First();
        using var response = await client.DeleteAsync($"/api/marine/activities/{row.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        await _factory.SeedAsync(async db => Assert.True(await db.MarineActivities.AnyAsync(a => a.Id == row.Id)));
        Assert.NotEmpty((await client.GetFromJsonAsync<List<SafetyProfileDto>>("/api/marine/safety-profiles"))!);
    }
    [Fact, Trait("CaseId", "M2-ACT-009")]
    public async Task M2_ACT_009_unpublished_activity_cannot_receive_a_new_assessment()
    {
        using var client = await ClientAsync();
        var row = (await client.GetFromJsonAsync<List<MarineActivityDto>>("/api/marine/activities"))!.First();
        _factory.CatalogueOverride = [];
        using var response = await client.PostAsJsonAsync("/api/marine/evaluate", new { activityId = row.Id, latitude = 6.025, longitude = 80.216 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await _factory.SeedAsync(async db => Assert.Empty(await db.SuitabilityAssessments.ToListAsync()));
    }
    [Fact, Trait("CaseId", "M2-ACT-010")]
    public async Task M2_ACT_010_catalogue_reads_require_a_current_read_grant()
    {
        using var client = await ClientAsync(); client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await client.GetAsync("/api/marine/activities");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.CreateToken(MarineSafetyTestSeed.UnauthorizedUserId));
        using var denied = await client.GetAsync("/api/marine/activities");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var body = await denied.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, body.GetProperty("status").GetInt32());
    }
    [Fact, Trait("CaseId", "M2-ACT-011")]
    public async Task Canonical_activity_profile_review_assessment_and_history_share_the_same_id()
    {
        using var client = await ClientAsync(true);
        var id = Guid.NewGuid(); _factory.CatalogueOverride = [Published(id, "Canonical coast activity")];
        using var created = await client.PostAsJsonAsync("/api/marine/safety-profiles", MarineSafetyTestSeed.ValidProfilePayload(id, 25m, 1.5m, 1.2m));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<SafetyProfileDto>();
        Assert.Equal(id, draft!.ActivityId); Assert.False(draft.IsActive); Assert.Equal(1,draft.Version);
        Assert.Equal(MarineSafetyTestSeed.ManagerUserId,draft.CreatedByUserId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.CreateToken(MarineSafetyTestSeed.ReviewerUserId));
        using var approved = await client.PostAsync($"/api/marine/safety-profiles/{draft.Id}/review", null);
        Assert.Equal(HttpStatusCode.OK,approved.StatusCode);
        var active = await approved.Content.ReadFromJsonAsync<SafetyProfileDto>();
        Assert.True(active!.IsActive); Assert.Equal(MarineSafetyTestSeed.ReviewerUserId,active.ReviewedByUserId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId));
        using var assessed = await client.PostAsJsonAsync("/api/marine/evaluate", new { activityId=id,latitude=6.025m,longitude=80.216m });
        Assert.Equal(HttpStatusCode.OK,assessed.StatusCode);
        var result = await assessed.Content.ReadFromJsonAsync<SuitabilityResultDto>();
        Assert.Equal(id,result!.ActivityId); Assert.Equal("Canonical coast activity",result.ActivityName);
        Assert.NotEqual(Guid.Empty,result.AssessmentId); Assert.NotEqual(Guid.Empty,result.SnapshotId);
        _factory.CatalogueOverride = [Published(id,"Renamed by catalogue owner")];
        using var reassessed = await client.PostAsJsonAsync("/api/marine/evaluate", new { activityId=id,latitude=6.025m,longitude=80.216m });
        Assert.Equal(HttpStatusCode.OK,reassessed.StatusCode);
        _factory.CatalogueOverride = [];
        var history = await client.GetFromJsonAsync<AssessmentHistoryDto>($"/api/marine/assessments/{result.AssessmentId}");
        Assert.Equal(id,history!.ActivityId); Assert.Equal("Canonical coast activity",history.ActivityName);
        Assert.Equal(draft.Id,history.SafetyProfileId); Assert.Equal(1,history.ProfileVersion);
        Assert.Equal(25m,Assert.Single(history.Criteria, c => c.Factor == "windSpeed").Maximum); Assert.Equal(result.SnapshotId,history.ConditionSnapshotId);
        using var removed = await client.PostAsJsonAsync("/api/marine/evaluate", new { activityId=id,latitude=6.025m,longitude=80.216m });
        Assert.Equal(HttpStatusCode.NotFound,removed.StatusCode);
        await _factory.SeedAsync(async db => Assert.Equal(2,await db.SuitabilityAssessments.CountAsync(a => a.ActivityId == id)));
    }
}
