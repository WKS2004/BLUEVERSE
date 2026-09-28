namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Requirement-based assessment-history tests (M2-ASMT-*). Assessments are
/// persisted by every evaluate call; the read surface must return the stored
/// evidence — newest first, filtered by activity, result and evaluation
/// window — with profile version, snapshot reference, violations, caution
/// factors, missing fields, source and freshness intact. There is no
/// client-facing write: history rows are created only by evaluate.
/// </summary>
public sealed class AssessmentHistoryEndpointTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;

    public AssessmentHistoryEndpointTests(MarineSafetyWebApplicationFactory factory)
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

    private async Task<HttpResponseMessage> EvaluateAsync(
        HttpClient client,
        string token,
        decimal windSpeed,
        Guid? activityId = null,
        string? time = null)
    {
        // Distinct requested hours per call: snapshot reuse only covers the
        // same rounded coordinate and hour, so distinct times force a fresh
        // provider acquisition per evaluate and distinct condition evidence.
        _factory.Provider = new StubOpenMeteoClient(() => new MarineConditionsResult(
            DateTime.UtcNow,
            DateTime.UtcNow,
            windSpeed,
            0.8m,
            0.7m,
            Rain: 0m,
            WeatherCode: 0,
            Source: ConditionSources.OpenMeteo,
            MissingFields: []));

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

    private static HttpRequestMessage AuthorizedGet(string uri, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    [Fact]
    [Trait("CaseId", "M2-ASMT-001")]
    public async Task M2_ASMT_001_history_lists_stored_assessments_with_full_evidence()
    {
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        using var evaluate = await EvaluateAsync(client, token, windSpeed: 15m);
        Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
        var evaluateBody = await evaluate.Content.ReadAsStringAsync();
        using var evaluateDocument = JsonDocument.Parse(evaluateBody);
        var assessmentId = evaluateDocument.RootElement.GetProperty("assessmentId").GetGuid();

        using var request = AuthorizedGet("/api/marine/assessments", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(1, document.RootElement.GetArrayLength());
        var row = document.RootElement[0];
        Assert.Equal(assessmentId, row.GetProperty("id").GetGuid());
        Assert.Equal(MarineSafetyTestSeed.ActivityId, row.GetProperty("activityId").GetGuid());
        Assert.Equal("Surfing", row.GetProperty("activityName").GetString());
        Assert.Equal("SUITABLE", row.GetProperty("result").GetString());
        Assert.Equal(1, row.GetProperty("profileVersion").GetInt32());
        Assert.Equal(6.025m, row.GetProperty("latitude").GetDecimal());
        Assert.Equal(80.216m, row.GetProperty("longitude").GetDecimal());
        Assert.Empty(row.GetProperty("violations").EnumerateArray());
        Assert.Empty(row.GetProperty("cautionFactors").EnumerateArray());
        Assert.Empty(row.GetProperty("missingFields").EnumerateArray());
        Assert.Equal("Open-Meteo", row.GetProperty("source").GetString());
        Assert.Equal("FRESH", row.GetProperty("freshnessStatus").GetString());
        Assert.False(row.GetProperty("conditionSnapshotId").GetGuid().Equals(Guid.Empty));
        Assert.False(row.GetProperty("safetyProfileId").GetGuid().Equals(Guid.Empty));
        Assert.True(row.GetProperty("evaluatedAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
        Assert.True(row.GetProperty("requestedTime").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
        Assert.True(row.GetProperty("createdAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
    }

    [Fact]
    [Trait("CaseId", "M2-ASMT-002")]
    public async Task M2_ASMT_002_history_is_newest_first_by_evaluated_at()
    {
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        // Three evaluations at distinct requested hours; each persists a row.
        for (var hour = 10; hour <= 12; hour++)
        {
            using var evaluate = await EvaluateAsync(
                client, token, windSpeed: 15m, time: $"2026-09-28T{hour:00}:00:00Z");
            Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
        }

        using var request = AuthorizedGet("/api/marine/assessments", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(3, document.RootElement.GetArrayLength());
        var times = new List<DateTimeOffset>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            times.Add(row.GetProperty("evaluatedAt").GetDateTimeOffset());
        }

        // Newest first: the requirement is a descending order, and ties keep
        // a stable identifier order. Ascending order fails the contract.
        Assert.Equal(times.OrderByDescending(t => t).ToList(), times);
        Assert.Equal(3, times.Distinct().Count());
    }

    [Fact]
    [Trait("CaseId", "M2-ASMT-003")]
    public async Task M2_ASMT_003_result_and_activity_filters_return_matching_rows_only()
    {
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        // A suitable run and an unsuitable run on the seeded activity...
        using var suitable = await EvaluateAsync(client, token, windSpeed: 15m, time: "2026-09-28T08:00:00Z");
        Assert.Equal(HttpStatusCode.OK, suitable.StatusCode);
        suitable.Dispose();
        using var unsuitable = await EvaluateAsync(client, token, windSpeed: 40m, time: "2026-09-28T09:00:00Z");
        Assert.Equal(HttpStatusCode.OK, unsuitable.StatusCode);
        unsuitable.Dispose();

        // ...and a profile-backed second activity with its own assessment.
        var second = Guid.NewGuid();
        await _factory.SeedAsync(db =>
        {
            db.MarineActivities.Add(new MarineActivity
            {
                Id = second,
                Name = "Jet Skiing " + Guid.NewGuid().ToString("N"),
                ActivityType = "BoatTour",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            db.SafetyProfiles.Add(new SafetyProfile
            {
                Id = Guid.NewGuid(),
                ActivityId = second,
                MaxWindSpeed = 25m,
                MaxWaveHeight = 1.5m,
                MaxSwellHeight = 1.2m,
                IsActive = true,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            return db.SaveChangesAsync();
        });

        using var secondEvaluate = await EvaluateAsync(client, token, windSpeed: 15m, activityId: second, time: "2026-09-28T10:00:00Z");
        Assert.Equal(HttpStatusCode.OK, secondEvaluate.StatusCode);

        // Result filter: two SUITABLE rows exist (Surfing + Jet Skiing).
        using var suitableOnly = AuthorizedGet("/api/marine/assessments?result=SUITABLE", token);
        using var suitableResponse = await client.SendAsync(suitableOnly);
        using var suitableBody = JsonDocument.Parse(await suitableResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, suitableBody.RootElement.GetArrayLength());
        Assert.Equal("SUITABLE", suitableBody.RootElement[0].GetProperty("result").GetString());

        using var unsuitableOnly = AuthorizedGet("/api/marine/assessments?result=UNSUITABLE", token);
        using var unsuitableResponse = await client.SendAsync(unsuitableOnly);
        using var unsuitableBody = JsonDocument.Parse(await unsuitableResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, unsuitableBody.RootElement.GetArrayLength());
        Assert.Equal("UNSUITABLE", unsuitableBody.RootElement[0].GetProperty("result").GetString());

        // A result vocabulary value that matches nothing yields an empty page.
        using var unknownOnly = AuthorizedGet("/api/marine/assessments?result=UNKNOWN", token);
        using var unknownResponse = await client.SendAsync(unknownOnly);
        using var unknownBody = JsonDocument.Parse(await unknownResponse.Content.ReadAsStringAsync());
        Assert.Empty(unknownBody.RootElement.EnumerateArray());

        // Case-insensitive result matching mirrors the stored vocabulary; the
        // same two SUITABLE rows are returned regardless of case.
        using var lowerCase = AuthorizedGet("/api/marine/assessments?result=suitable", token);
        using var lowerResponse = await client.SendAsync(lowerCase);
        using var lowerBody = JsonDocument.Parse(await lowerResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, lowerBody.RootElement.GetArrayLength());

        // Activity filter: only the second activity's assessment.
        using var secondOnly = AuthorizedGet($"/api/marine/assessments?activityId={second}", token);
        using var secondResponse = await client.SendAsync(secondOnly);
        using var secondBody = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, secondBody.RootElement.GetArrayLength());
        Assert.Equal(second, secondBody.RootElement[0].GetProperty("activityId").GetGuid());

        // A nonexistent activity never leaks rows.
        using var ghost = AuthorizedGet($"/api/marine/assessments?activityId={Guid.NewGuid()}", token);
        using var ghostResponse = await client.SendAsync(ghost);
        using var ghostBody = JsonDocument.Parse(await ghostResponse.Content.ReadAsStringAsync());
        Assert.Empty(ghostBody.RootElement.EnumerateArray());
    }

    [Fact]
    [Trait("CaseId", "M2-ASMT-004")]
    public async Task M2_ASMT_004_evaluation_window_filter_bounds_the_result()
    {
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        using var first = await EvaluateAsync(client, token, windSpeed: 15m, time: "2026-09-28T08:00:00Z");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        first.Dispose();
        using var second = await EvaluateAsync(client, token, windSpeed: 15m, time: "2026-09-28T12:00:00Z");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        second.Dispose();

        // The window filters on the requested (forecast) time — the
        // domain-meaningful axis for an assessment, not the wall-clock
        // evaluation moment. A window covering only the later evaluation
        // returns one row.
        using var late = AuthorizedGet("/api/marine/assessments?from=2026-09-28T11:00:00Z", token);
        using var lateResponse = await client.SendAsync(late);
        using var lateBody = JsonDocument.Parse(await lateResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, lateBody.RootElement.GetArrayLength());
        Assert.Equal("2026-09-28T12:00:00Z", lateBody.RootElement[0].GetProperty("requestedTime").GetString());

        // A window covering only the earlier evaluation returns the other row.
        using var early = AuthorizedGet("/api/marine/assessments?to=2026-09-28T09:00:00Z", token);
        using var earlyResponse = await client.SendAsync(early);
        using var earlyBody = JsonDocument.Parse(await earlyResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, earlyBody.RootElement.GetArrayLength());
        Assert.Equal("2026-09-28T08:00:00Z", earlyBody.RootElement[0].GetProperty("requestedTime").GetString());

        // A window containing no evaluations yields an empty page.
        using var empty = AuthorizedGet("/api/marine/assessments?from=2026-09-28T09:00:00Z&to=2026-09-28T10:00:00Z", token);
        using var emptyResponse = await client.SendAsync(empty);
        using var emptyBody = JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync());
        Assert.Empty(emptyBody.RootElement.EnumerateArray());
    }

    [Fact]
    [Trait("CaseId", "M2-ASMT-005")]
    public async Task M2_ASMT_005_one_row_by_id_and_unknown_id_is_not_found()
    {
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        using var evaluate = await EvaluateAsync(client, token, windSpeed: 15m);
        var evaluateBody = await evaluate.Content.ReadAsStringAsync();
        using var evaluateDocument = JsonDocument.Parse(evaluateBody);
        var assessmentId = evaluateDocument.RootElement.GetProperty("assessmentId").GetGuid();

        using var request = AuthorizedGet($"/api/marine/assessments/{assessmentId}", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(assessmentId, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Surfing", document.RootElement.GetProperty("activityName").GetString());
        Assert.Equal("SUITABLE", document.RootElement.GetProperty("result").GetString());

        using var unknown = AuthorizedGet($"/api/marine/assessments/{Guid.NewGuid()}", token);
        using var unknownResponse = await client.SendAsync(unknown);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-ASMT-006")]
    public async Task M2_ASMT_006_history_reads_are_permission_gated()
    {
        using var client = await CreateClientAsync();
        var token = ReaderToken();

        using var evaluate = await EvaluateAsync(client, token, windSpeed: 15m);
        var assessmentId = JsonDocument.Parse(await evaluate.Content.ReadAsStringAsync())
            .RootElement.GetProperty("assessmentId").GetGuid();

        // Anonymous callers are unauthenticated on both surfaces.
        using var anonymousList = AuthorizedGet("/api/marine/assessments", null);
        using var anonymousListResponse = await client.SendAsync(anonymousList);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousListResponse.StatusCode);

        using var anonymousById = AuthorizedGet($"/api/marine/assessments/{assessmentId}", null);
        using var anonymousByIdResponse = await client.SendAsync(anonymousById);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousByIdResponse.StatusCode);

        // A user without the marine read grant is forbidden.
        var foreign = _factory.CreateToken(MarineSafetyTestSeed.UnauthorizedUserId);
        using var foreignList = AuthorizedGet("/api/marine/assessments", foreign);
        using var foreignListResponse = await client.SendAsync(foreignList);
        Assert.Equal(HttpStatusCode.Forbidden, foreignListResponse.StatusCode);

        using var foreignById = AuthorizedGet($"/api/marine/assessments/{assessmentId}", foreign);
        using var foreignByIdResponse = await client.SendAsync(foreignById);
        Assert.Equal(HttpStatusCode.Forbidden, foreignByIdResponse.StatusCode);

        // The read grant is sufficient on both surfaces.
        using var readerList = AuthorizedGet("/api/marine/assessments", token);
        using var readerListResponse = await client.SendAsync(readerList);
        Assert.Equal(HttpStatusCode.OK, readerListResponse.StatusCode);

        using var readerById = AuthorizedGet($"/api/marine/assessments/{assessmentId}", token);
        using var readerByIdResponse = await client.SendAsync(readerById);
        Assert.Equal(HttpStatusCode.OK, readerByIdResponse.StatusCode);
    }
}
