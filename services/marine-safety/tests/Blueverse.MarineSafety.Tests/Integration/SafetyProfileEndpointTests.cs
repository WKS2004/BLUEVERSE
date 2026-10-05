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
    public async Task M2_PROF_001_manager_creates_a_cited_draft_and_a_different_manager_approves_it()
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

        using var request = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(newActivityId, 30m, 2.0m, 1.5m));
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
        Assert.False(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("Test safety standard, section 4", root.GetProperty("windCriteriaSource").GetString());
        Assert.Equal("This cited limit is used as deterministic test evidence.", root.GetProperty("swellCriteriaRationale").GetString());
        Assert.Null(root.GetProperty("reviewedAt").GetString());

        // The author cannot approve their own draft; the assigned reviewer can.
        var profileId = root.GetProperty("id").GetGuid();
        using var selfReview = Authorized(HttpMethod.Post, $"/api/marine/safety-profiles/{profileId}/review", token);
        using var selfReviewResponse = await client.SendAsync(selfReview);
        Assert.Equal(HttpStatusCode.Conflict, selfReviewResponse.StatusCode);

        var reviewerToken = _factory.CreateToken(MarineSafetyTestSeed.ReviewerUserId);
        using var review = Authorized(HttpMethod.Post, $"/api/marine/safety-profiles/{profileId}/review", reviewerToken);
        using var reviewResponse = await client.SendAsync(review);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        using var reviewDocument = JsonDocument.Parse(await reviewResponse.Content.ReadAsStringAsync());
        Assert.True(reviewDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal(MarineSafetyTestSeed.ReviewerUserId, reviewDocument.RootElement.GetProperty("reviewedByUserId").GetGuid());
        Assert.True(reviewDocument.RootElement.GetProperty("effectiveFrom").GetDateTime() > DateTime.MinValue);

        // Persistence: the profile is retrievable through the read path.
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

        using var request = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(Guid.NewGuid(), 30m, 2.0m, 1.5m));
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
                     MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, -5m, 1.5m, 1m),
                     MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, 25m, 0m, 1m),
                     MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, 25m, 1.5m, -1m)
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

        using var request = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, 25m, 1.5m, 1.2m, cautionWaveHeight: 1.5m));
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
    public async Task M2_PROF_007_manager_update_creates_an_immutable_pending_version()
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

        using var request = Authorized(HttpMethod.Put, $"/api/marine/safety-profiles/{profileId}", token,
            MarineSafetyTestSeed.ValidProfilePayload(maxWindSpeed: 22m, maxWaveHeight: 1.2m, maxSwellHeight: 1.0m));
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(22m, document.RootElement.GetProperty("maxWindSpeed").GetDecimal());
        Assert.Equal(1.2m, document.RootElement.GetProperty("maxWaveHeight").GetDecimal());
        Assert.Equal(versionBefore + 1, document.RootElement.GetProperty("version").GetInt32());
        Assert.NotEqual(profileId, document.RootElement.GetProperty("id").GetGuid());
        Assert.False(document.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Null(document.RootElement.GetProperty("reviewedAt").GetString());

        using var oldProfile = Authorized(HttpMethod.Get, $"/api/marine/safety-profiles/{profileId}", token);
        using var oldProfileResponse = await client.SendAsync(oldProfile);
        using var oldProfileDocument = JsonDocument.Parse(await oldProfileResponse.Content.ReadAsStringAsync());
        Assert.Equal(25m, oldProfileDocument.RootElement.GetProperty("maxWindSpeed").GetDecimal());
        Assert.True(oldProfileDocument.RootElement.GetProperty("isActive").GetBoolean());
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

    [Fact]
    [Trait("CaseId", "M2-PROF-010")]
    public async Task M2_PROF_010_recreating_after_draft_deactivation_never_reuses_a_version()
    {
        // Regression for the create-path version collision: the next version
        // must come from all of the activity's profile rows, not only the
        // active ones. After every profile of the activity has been
        // deactivated, creating a new profile must still continue the version
        // sequence instead of reusing a discarded version (which would make
        // profile-version references ambiguous).
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        // A draft receives v2 while the previously approved version remains active.
        using var first = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, 28m, 1.8m, 1.4m));
        using var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        using var firstDocument = JsonDocument.Parse(firstBody);
        Assert.Equal(2, firstDocument.RootElement.GetProperty("version").GetInt32());
        var secondProfileId = firstDocument.RootElement.GetProperty("id").GetGuid();

        // Discarding an unreviewed draft leaves the approved version effective.
        using var deactivate = Authorized(HttpMethod.Delete, $"/api/marine/safety-profiles/{secondProfileId}", token);
        using var deactivateResponse = await client.SendAsync(deactivate);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        // Recreating must resume at v3, not collide at v1 or v2.
        using var second = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, 20m, 1.0m, 0.8m));
        using var secondResponse = await client.SendAsync(second);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        using var secondDocument = JsonDocument.Parse(secondBody);
        Assert.Equal(3, secondDocument.RootElement.GetProperty("version").GetInt32());

        // Exactly one reviewed profile remains active; both new immutable
        // versions remain visible as pending review drafts.
        using var list = Authorized(HttpMethod.Get, "/api/marine/safety-profiles", token);
        using var listResponse = await client.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        using var listDocument = JsonDocument.Parse(listBody);
        var forActivity = listDocument.RootElement.EnumerateArray()
            .Where(p => p.GetProperty("activityId").GetGuid() == MarineSafetyTestSeed.ActivityId)
            .Select(p => (version: p.GetProperty("version").GetInt32(), isActive: p.GetProperty("isActive").GetBoolean()))
            .ToList();

        Assert.Equal(3, forActivity.Count);
        Assert.Equal(1, forActivity.Count(p => p.isActive));
        Assert.Equal(1, forActivity.Single(p => p.isActive).version);
        Assert.Equal(new[] { 1, 2, 3 }, forActivity.Select(p => p.version).OrderBy(v => v).ToArray());
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-011")]
    public async Task M2_PROF_011_update_creates_a_draft_and_cannot_activate_it_directly()
    {
        // Updating thresholds cannot bypass the evidence review gate. Legacy
        // isActive input is ignored and the currently approved version stays
        // effective until the new immutable version is reviewed.
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        using var current = Authorized(HttpMethod.Get,
            $"/api/marine/safety-profiles/by-activity/{MarineSafetyTestSeed.ActivityId}", token);
        using var currentResponse = await client.SendAsync(current);
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        using var currentDocument = JsonDocument.Parse(await currentResponse.Content.ReadAsStringAsync());
        var seededId = currentDocument.RootElement.GetProperty("id").GetGuid();

        var updatePayload = MarineSafetyTestSeed.ValidProfilePayload(maxWindSpeed: 26m, maxWaveHeight: 1.6m, maxSwellHeight: 1.3m);
        updatePayload["isActive"] = true;
        using var update = Authorized(HttpMethod.Put, $"/api/marine/safety-profiles/{seededId}", token, updatePayload);
        using var updateResponse = await client.SendAsync(update);
        var updateBody = await updateResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        using var updateDocument = JsonDocument.Parse(updateBody);
        var draftId = updateDocument.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(seededId, draftId);
        Assert.False(updateDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Null(updateDocument.RootElement.GetProperty("reviewedAt").GetString());
        Assert.Equal(2, updateDocument.RootElement.GetProperty("version").GetInt32());

        using var byActivity = Authorized(
            HttpMethod.Get,
            $"/api/marine/safety-profiles/by-activity/{MarineSafetyTestSeed.ActivityId}",
            token);
        using var byActivityResponse = await client.SendAsync(byActivity);
        Assert.Equal(HttpStatusCode.OK, byActivityResponse.StatusCode);
        var byActivityBody = await byActivityResponse.Content.ReadAsStringAsync();
        using var byActivityDocument = JsonDocument.Parse(byActivityBody);
        Assert.Equal(seededId, byActivityDocument.RootElement.GetProperty("id").GetGuid());
        Assert.True(byActivityDocument.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-012")]
    public async Task M2_PROF_012_review_activates_the_cited_draft_and_atomically_supersedes_the_active_version()
    {
        // Approval records its reviewer and effective time while replacing
        // the old active version in the same transaction.
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        // Create a second profile (supersedes the seeded one) and capture both IDs.
        using var create = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(MarineSafetyTestSeed.ActivityId, 18m, 0.9m, 0.7m));
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        using var createDocument = JsonDocument.Parse(createBody);
        var draftId = createDocument.RootElement.GetProperty("id").GetGuid();
        using var review = Authorized(HttpMethod.Post, $"/api/marine/safety-profiles/{draftId}/review",
            _factory.CreateToken(MarineSafetyTestSeed.ReviewerUserId));
        using var reviewResponse = await client.SendAsync(review);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        using var reviewDocument = JsonDocument.Parse(await reviewResponse.Content.ReadAsStringAsync());
        Assert.True(reviewDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal(MarineSafetyTestSeed.ReviewerUserId, reviewDocument.RootElement.GetProperty("reviewedByUserId").GetGuid());
        Assert.NotEqual(default, reviewDocument.RootElement.GetProperty("effectiveFrom").GetDateTimeOffset());

        using var list = Authorized(HttpMethod.Get, "/api/marine/safety-profiles", token);
        using var listResponse = await client.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        using var listDocument = JsonDocument.Parse(listBody);
        var forActivity = listDocument.RootElement.EnumerateArray()
            .Where(p => p.GetProperty("activityId").GetGuid() == MarineSafetyTestSeed.ActivityId)
            .Select(p => (id: p.GetProperty("id").GetGuid(), isActive: p.GetProperty("isActive").GetBoolean()))
            .ToList();

        Assert.Equal(2, forActivity.Count);
        Assert.Equal(1, forActivity.Count(p => p.isActive));
        Assert.Equal(draftId, forActivity.Single(p => p.isActive).id);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-013")]
    public async Task M2_PROF_013_create_for_inactive_activity_is_rejected()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        var inactiveId = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.MarineActivities.Add(new MarineActivity
            {
                Id = inactiveId,
                Name = "Jet Ski " + Guid.NewGuid().ToString("N"),
                ActivityType = "BoatTour",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
            return db.SaveChangesAsync();
        });

        using var request = Authorized(HttpMethod.Post, "/api/marine/safety-profiles", token,
            MarineSafetyTestSeed.ValidProfilePayload(inactiveId));
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("not active", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-014")]
    public async Task M2_PROF_014_by_activity_for_unknown_activity_is_not_found()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ReaderUserId);

        using var request = Authorized(
            HttpMethod.Get,
            $"/api/marine/safety-profiles/by-activity/{Guid.NewGuid()}",
            token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-015")]
    public async Task M2_PROF_015_by_activity_with_no_active_profile_is_not_found()
    {
        // The seeded activity's profile is deactivated by M2-PROF-008 within
        // its own database; here a fresh store gets its profile deactivated.
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
        using var deleteResponse = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var request = Authorized(
            HttpMethod.Get,
            $"/api/marine/safety-profiles/by-activity/{MarineSafetyTestSeed.ActivityId}",
            token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-016")]
    public async Task M2_PROF_016_update_of_unknown_profile_is_not_found()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        using var request = Authorized(HttpMethod.Put, $"/api/marine/safety-profiles/{Guid.NewGuid()}", token,
            MarineSafetyTestSeed.ValidProfilePayload(maxWindSpeed: 20m, maxWaveHeight: 1m, maxSwellHeight: 0.8m));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-017")]
    public async Task M2_PROF_017_deactivate_of_unknown_profile_is_not_found()
    {
        using var client = await CreateClientAsync();
        var token = _factory.CreateToken(MarineSafetyTestSeed.ManagerUserId);

        using var request = Authorized(HttpMethod.Delete, $"/api/marine/safety-profiles/{Guid.NewGuid()}", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-018")]
    public async Task M2_PROF_018_deactivate_is_idempotent_on_an_inactive_profile()
    {
        // Deleting an already-inactive profile reports success without
        // touching the row again: the domain rule is "no active profile with
        // this id remains", which holds before and after.
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

        using var first = Authorized(HttpMethod.Delete, $"/api/marine/safety-profiles/{profileId}", token);
        using var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);

        using var second = Authorized(HttpMethod.Delete, $"/api/marine/safety-profiles/{profileId}", token);
        using var secondResponse = await client.SendAsync(second);
        Assert.Equal(HttpStatusCode.NoContent, secondResponse.StatusCode);

        using var verify = Authorized(HttpMethod.Get, $"/api/marine/safety-profiles/{profileId}", token);
        using var verifyResponse = await client.SendAsync(verify);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        using var verifyDocument = JsonDocument.Parse(verifyBody);
        Assert.False(verifyDocument.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-019")]
    public async Task M2_PROF_019_update_with_caution_band_at_or_above_maximum_is_rejected()
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

        using var request = Authorized(HttpMethod.Put, $"/api/marine/safety-profiles/{profileId}", token,
            MarineSafetyTestSeed.ValidProfilePayload(maxWindSpeed: 25m, maxWaveHeight: 1.5m,
                maxSwellHeight: 1.2m, cautionSwellHeight: 1.2m));
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Caution swell height", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("CaseId", "M2-PROF-020")]
    public async Task M2_PROF_020_update_rejects_non_positive_limits()
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

        foreach (var invalidLimits in new[]
                 {
                     MarineSafetyTestSeed.ValidProfilePayload(maxWindSpeed: 0m, maxWaveHeight: 1.5m, maxSwellHeight: 1.2m),
                     MarineSafetyTestSeed.ValidProfilePayload(maxWindSpeed: 25m, maxWaveHeight: -1m, maxSwellHeight: 1.2m)
                 })
        {
            using var request = Authorized(HttpMethod.Put, $"/api/marine/safety-profiles/{profileId}", token, invalidLimits);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
