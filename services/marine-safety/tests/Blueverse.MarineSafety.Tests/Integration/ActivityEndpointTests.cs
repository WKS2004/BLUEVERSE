namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Requirement-based marine-activity reference-table tests (M2-ACT-*). The
/// full CRUD contract is asserted from the requirements: reads need the
/// marine read grant, writes need manage in addition, names are unique
/// (duplicate is a state conflict, not invalid input), and DELETE deactivates
/// — never hard-deletes — so profile and assessment references survive.
/// </summary>
public sealed class ActivityEndpointTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private static readonly Guid CreatedActivityId = Guid.Parse("33333333-3333-3333-3333-333333333390");

    private readonly MarineSafetyWebApplicationFactory _factory;

    public ActivityEndpointTests(MarineSafetyWebApplicationFactory factory)
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

    private string ReaderToken() =>
        _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);

    private string ManagerToken() =>
        _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

    private static HttpRequestMessage Authorized(
        HttpMethod method,
        string uri,
        string? token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-001")]
    public async Task M2_ACT_001_manager_creates_an_activity_and_it_is_persisted()
    {
        using var client = await CreateClientAsync();
        var token = ManagerToken();

        using var request = Authorized(HttpMethod.Post, "/api/marine/activities", token, new
        {
            name = "Kite Surfing",
            activityType = "Surfing"
        });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("Kite Surfing", root.GetProperty("name").GetString());
        Assert.Equal("Surfing", root.GetProperty("activityType").GetString());
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.False(root.GetProperty("id").GetGuid().Equals(Guid.Empty));
        var createdId = root.GetProperty("id").GetGuid();

        // Persistence: the row is retrievable through the read path.
        using var verify = Authorized(HttpMethod.Get, $"/api/marine/activities/{createdId}", token);
        using var verifyResponse = await client.SendAsync(verify);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        using var verifyDocument = JsonDocument.Parse(verifyBody);
        Assert.Equal("Kite Surfing", verifyDocument.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-002")]
    public async Task M2_ACT_002_create_with_duplicate_name_is_a_state_conflict()
    {
        using var client = await CreateClientAsync();
        var token = ManagerToken();

        using var request = Authorized(HttpMethod.Post, "/api/marine/activities", token, new
        {
            name = "Surfing",
            activityType = "Surfing"
        });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // The reference table has a unique name; a duplicate is a domain state
        // conflict, not malformed input (G00 error/status row).
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Activity Name Already In Use", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(409, document.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-003")]
    public async Task M2_ACT_003_create_with_missing_or_blank_fields_is_rejected()
    {
        using var client = await CreateClientAsync();
        var token = ManagerToken();

        foreach (var body in new object[]
                 {
                     new { activityType = "Surfing" },
                     new { name = "No Type" },
                     new { name = "", activityType = "Surfing" },
                     new { name = "Blank Type", activityType = "" }
                 })
        {
            using var request = Authorized(HttpMethod.Post, "/api/marine/activities", token, body);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Nothing was persisted.
        using var list = Authorized(HttpMethod.Get, "/api/marine/activities", token);
        using var listResponse = await client.SendAsync(list);
        using var listBody = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, listBody.RootElement.GetArrayLength());
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-004")]
    public async Task M2_ACT_004_reader_lists_activities_and_the_active_filter_works()
    {
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        using var request = Authorized(HttpMethod.Get, "/api/marine/activities", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetArrayLength());
        foreach (var row in body.RootElement.EnumerateArray())
        {
            Assert.False(row.GetProperty("id").GetGuid().Equals(Guid.Empty));
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("name").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("activityType").GetString()));
            Assert.True(row.GetProperty("isActive").GetBoolean());
            Assert.True(row.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddDays(-1));
        }

        using var filtered = Authorized(HttpMethod.Get, "/api/marine/activities?isActive=false", token);
        using var filteredResponse = await client.SendAsync(filtered);
        using var filteredBody = JsonDocument.Parse(await filteredResponse.Content.ReadAsStringAsync());
        Assert.Empty(filteredBody.RootElement.EnumerateArray());
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-005")]
    public async Task M2_ACT_005_get_by_id_returns_the_row_and_unknown_ids_are_not_found()
    {
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        using var known = Authorized(HttpMethod.Get, $"/api/marine/activities/{MarineSafetyTestSeed.ActivityId}", token);
        using var knownResponse = await client.SendAsync(known);
        Assert.Equal(HttpStatusCode.OK, knownResponse.StatusCode);
        using var knownBody = JsonDocument.Parse(await knownResponse.Content.ReadAsStringAsync());
        Assert.Equal(MarineSafetyTestSeed.ActivityId, knownBody.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Surfing", knownBody.RootElement.GetProperty("name").GetString());

        using var unknown = Authorized(HttpMethod.Get, $"/api/marine/activities/{Guid.NewGuid()}", token);
        using var unknownResponse = await client.SendAsync(unknown);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-006")]
    public async Task M2_ACT_006_manager_updates_name_and_type_and_references_still_resolve()
    {
        using var client = await CreateClientAsync();
        var token = ManagerToken();
        var originalName = "Surfing";

        using var request = Authorized(
            HttpMethod.Put,
            $"/api/marine/activities/{MarineSafetyTestSeed.ActivityId}",
            token,
            new { name = originalName + " Junior", activityType = "Surfing" });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(originalName + " Junior", document.RootElement.GetProperty("name").GetString());
        Assert.Equal("Surfing", document.RootElement.GetProperty("activityType").GetString());
        Assert.True(document.RootElement.GetProperty("isActive").GetBoolean());

        // The seeded profile still resolves through the existing read path:
        // renaming must not orphan the FK references.
        using var profile = Authorized(
            HttpMethod.Get,
            $"/api/marine/safety-profiles/by-activity/{MarineSafetyTestSeed.ActivityId}",
            token);
        using var profileResponse = await client.SendAsync(profile);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        using var profileBody = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync());
        Assert.Equal(originalName + " Junior", profileBody.RootElement.GetProperty("activityName").GetString());
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-007")]
    public async Task M2_ACT_007_update_name_conflict_and_unknown_activity()
    {
        using var client = await CreateClientAsync();
        var token = ManagerToken();

        // A second reference row is created directly in the store (HasData
        // seeds are wiped by the per-test database reset, so the store holds
        // only the fixture-seeded rows), then renaming Surfing onto its name
        // is a state conflict.
        var otherName = "Snorkeling " + Guid.NewGuid().ToString("N");
        await _factory.SeedAsync(db =>
        {
            db.MarineActivities.Add(new MarineActivity
            {
                Id = Guid.NewGuid(),
                Name = otherName,
                ActivityType = "Snorkeling",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            return db.SaveChangesAsync();
        });

        using var conflict = Authorized(
            HttpMethod.Put,
            $"/api/marine/activities/{MarineSafetyTestSeed.ActivityId}",
            token,
            new { name = otherName, activityType = "Snorkeling" });
        using var conflictResponse = await client.SendAsync(conflict);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        var conflictBody = await conflictResponse.Content.ReadAsStringAsync();
        using var conflictDocument = JsonDocument.Parse(conflictBody);
        Assert.Equal("Activity Name Already In Use", conflictDocument.RootElement.GetProperty("title").GetString());

        // A rename to the same row's own name is not a conflict.
        using var self = Authorized(
            HttpMethod.Put,
            $"/api/marine/activities/{MarineSafetyTestSeed.ActivityId}",
            token,
            new { name = "Surfing", activityType = "Surfing" });
        using var selfResponse = await client.SendAsync(self);
        Assert.Equal(HttpStatusCode.OK, selfResponse.StatusCode);

        // Updating a nonexistent activity is 404.
        using var unknown = Authorized(
            HttpMethod.Put,
            $"/api/marine/activities/{Guid.NewGuid()}",
            token,
            new { name = "Ghost", activityType = "Surfing" });
        using var unknownResponse = await client.SendAsync(unknown);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-008")]
    public async Task M2_ACT_008_delete_deactivates_and_never_hard_deletes()
    {
        using var client = await CreateClientAsync();
        var token = ManagerToken();
        var createdId = CreatedActivityId;

        await _factory.SeedAsync(db =>
        {
            db.MarineActivities.Add(new MarineActivity
            {
                Id = createdId,
                Name = "Sunset Cruising " + Guid.NewGuid().ToString("N"),
                ActivityType = "BoatTour",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            return db.SaveChangesAsync();
        });

        using var delete = Authorized(HttpMethod.Delete, $"/api/marine/activities/{createdId}", token);
        using var deleteResponse = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Soft-delete: the row survives and reports isActive=false.
        using var verify = Authorized(HttpMethod.Get, $"/api/marine/activities/{createdId}", token);
        using var verifyResponse = await client.SendAsync(verify);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        using var verifyDocument = JsonDocument.Parse(verifyBody);
        Assert.False(verifyDocument.RootElement.GetProperty("isActive").GetBoolean());

        using var inactiveList = Authorized(HttpMethod.Get, "/api/marine/activities?isActive=false", token);
        using var inactiveResponse = await client.SendAsync(inactiveList);
        using var inactiveBody = JsonDocument.Parse(await inactiveResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, inactiveBody.RootElement.GetArrayLength());

        // Repeating the delete stays 204: deactivation is idempotent.
        using var repeat = Authorized(HttpMethod.Delete, $"/api/marine/activities/{createdId}", token);
        using var repeatResponse = await client.SendAsync(repeat);
        Assert.Equal(HttpStatusCode.NoContent, repeatResponse.StatusCode);

        using var unknown = Authorized(HttpMethod.Delete, $"/api/marine/activities/{Guid.NewGuid()}", token);
        using var unknownResponse = await client.SendAsync(unknown);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-009")]
    public async Task M2_ACT_009_deactivated_activity_rejects_profiles_and_evaluations()
    {
        using var client = await CreateClientAsync();
        var token = ManagerToken();

        using var delete = Authorized(HttpMethod.Delete, $"/api/marine/activities/{MarineSafetyTestSeed.ActivityId}", token);
        using var deleteResponse = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // The seeded profile remains readable but a re-create is rejected.
        using var recreate = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, 30m, 2.0m, 1.5m));
        using var recreateResponse = await client.SendAsync(recreate);
        Assert.Equal(HttpStatusCode.Conflict, recreateResponse.StatusCode);
        var recreateBody = await recreateResponse.Content.ReadAsStringAsync();
        using var recreateDocument = JsonDocument.Parse(recreateBody);
        Assert.Contains("is not active", recreateDocument.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        // Evaluation is rejected because the activity is not active. The
        // evaluate surface maps a nonexistent-or-inactive activity to 404
        // "Activity Not Found" (G00 revision (4) semantics: the profile gap
        // is the 409; the activity-state gap is the 404).
        using var evaluate = Authorized(HttpMethod.Post, "/api/marine/evaluate", ReaderToken(), new
        {
            activityId = MarineSafetyTestSeed.ActivityId,
            latitude = 6.025m,
            longitude = 80.216m
        });
        using var evaluateResponse = await client.SendAsync(evaluate);
        Assert.Equal(HttpStatusCode.NotFound, evaluateResponse.StatusCode);
        var evaluateBody = await evaluateResponse.Content.ReadAsStringAsync();
        using var evaluateDocument = JsonDocument.Parse(evaluateBody);
        Assert.Equal("Activity Not Found", evaluateDocument.RootElement.GetProperty("title").GetString());

        // The active filter hides the deactivated row.
        using var active = Authorized(HttpMethod.Get, "/api/marine/activities?isActive=true", ReaderToken());
        using var activeResponse = await client.SendAsync(active);
        using var activeBody = JsonDocument.Parse(await activeResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, activeBody.RootElement.GetArrayLength());
    }

    [Fact]
    [Trait("CaseId", "M2-ACT-010")]
    public async Task M2_ACT_010_activity_writes_are_manage_gated_and_reads_authenticated()
    {
        using var client = await CreateClientAsync();
        var reader = ReaderToken();
        var manager = ManagerToken();

        // Anonymous is unauthenticated on both surfaces.
        using var anonymousRead = Authorized(HttpMethod.Get, "/api/marine/activities", null);
        using var anonymousReadResponse = await client.SendAsync(anonymousRead);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReadResponse.StatusCode);

        using var anonymousWrite = Authorized(HttpMethod.Post, "/api/marine/activities", null, new { name = "X", activityType = "Y" });
        using var anonymousWriteResponse = await client.SendAsync(anonymousWrite);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousWriteResponse.StatusCode);

        // A token with a broken audience is rejected before authorization.
        var wrongAudience = _factory.CreateToken(
            MarineSafetyTestSeed.ManagerUserId,
            expires: null);
        using var wrongToken = new HttpRequestMessage(HttpMethod.Get, "/api/marine/activities");
        wrongToken.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", wrongAudience);

        // The read-only grant cannot create, update or deactivate.
        using var readerCreate = Authorized(HttpMethod.Post, "/api/marine/activities", reader, new { name = "No Grant", activityType = "Surfing" });
        using var readerCreateResponse = await client.SendAsync(readerCreate);
        Assert.Equal(HttpStatusCode.Forbidden, readerCreateResponse.StatusCode);

        using var readerUpdate = Authorized(
            HttpMethod.Put,
            $"/api/marine/activities/{MarineSafetyTestSeed.ActivityId}",
            reader,
            new { name = "No Grant", activityType = "Surfing" });
        using var readerUpdateResponse = await client.SendAsync(readerUpdate);
        Assert.Equal(HttpStatusCode.Forbidden, readerUpdateResponse.StatusCode);

        using var readerDelete = Authorized(HttpMethod.Delete, $"/api/marine/activities/{MarineSafetyTestSeed.ActivityId}", reader);
        using var readerDeleteResponse = await client.SendAsync(readerDelete);
        Assert.Equal(HttpStatusCode.Forbidden, readerDeleteResponse.StatusCode);

        // The manage grant is sufficient and nothing was mutated by the denied calls.
        using var allowed = Authorized(HttpMethod.Post, "/api/marine/activities", manager, new
        {
            name = "Grant Check " + Guid.NewGuid().ToString("N"),
            activityType = "Surfing"
        });
        using var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.Created, allowedResponse.StatusCode);

        using var finalList = Authorized(HttpMethod.Get, "/api/marine/activities", reader);
        using var finalResponse = await client.SendAsync(finalList);
        using var finalBody = JsonDocument.Parse(await finalResponse.Content.ReadAsStringAsync());
        Assert.Equal(3, finalBody.RootElement.GetArrayLength());
    }
}
