using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Blueverse.ExperienceBiodiversity.Services;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Unit;

public sealed class MergedPeerAdapterTests
{
    [Fact]
    [Trait("CaseId", "EXP-MERGE-MARINE-001")]
    public async Task Marine_adapter_uses_current_route_forwards_bearer_and_keeps_unclassified_fields_unknown()
    {
        var destinationId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var retrieved = DateTimeOffset.UtcNow;
        var executor = new FakeExecutor(Response($$"""
            {"id":"{{snapshotId}}","latitude":5.94,"longitude":80.45,"forecastTime":"{{retrieved:O}}","retrievedAt":"{{retrieved:O}}","windSpeed":18.52,"waveHeight":0.7,"swellHeight":0.4,"rain":0.1,"weatherCode":2,"source":"Open-Meteo","freshnessStatus":"FRESH","missingFields":[]}
            """));
        var context = AuthenticatedContext("caller-token-123");
        var service = new MarineSafetyConsumerService(executor, Configuration(), NullLogger<MarineSafetyConsumerService>.Instance, context);

        var result = await service.GetMarineConditionsAsync(destinationId, "Mirissa", 5.94, 80.45);

        Assert.Equal("http://api.test/api/marine/current?latitude=5.94&longitude=80.45", executor.TargetUrl);
        Assert.Equal("caller-token-123", executor.BearerToken);
        Assert.True(result.Responded);
        Assert.Equal("UNKNOWN", result.SafetyLevel);
        Assert.Equal("UNKNOWN", result.WaterCondition);
        Assert.Equal(snapshotId, result.SnapshotId);
        Assert.Equal("FRESH", result.FreshnessStatus);
        Assert.Equal(10d, result.WindSpeedKnots!.Value, precision: 5);
        Assert.Equal(retrieved, result.RetrievedAt);
        Assert.False(result.FallbackUsed);
    }

    [Fact]
    [Trait("CaseId", "EXP-MERGE-BIO-001")]
    public async Task Biodiversity_adapter_maps_actual_planner_species_without_occurrence_or_conservation_claims()
    {
        var destinationId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();
        var evaluated = DateTimeOffset.UtcNow;
        var executor = new FakeExecutor(Response($$$"""
            {"destinationId":"{{{destinationId}}}","activityId":null,"status":"available","predictedSpecies":[{"speciesId":"{{{speciesId}}}","scientificName":"Chelonia mydas","commonName":"Green turtle","habitatSuitability":0.82,"confidenceLevel":"HIGH","uncertaintyText":"Model confidence varies by season."}],"modelMetadata":{"modelVersion":"fixture-v2","inferenceTimestamp":"{{{evaluated:O}}}"},"limitations":"Not a confirmed sighting.","provenance":{"provider":"it3091-model","modelVersion":"fixture-v2","inferenceTimestamp":"{{{evaluated:O}}}"}}
            """));
        var service = new BiodiversityConsumerService(executor, Configuration(), NullLogger<BiodiversityConsumerService>.Instance,
            AuthenticatedContext("planner-read-token"));

        var result = await service.GetBiodiversityContextAsync(destinationId, "Mirissa", 5.94, 80.45);

        Assert.Equal($"http://api.test/api/planner/biodiversity/predictions?destinationId={destinationId:D}", executor.TargetUrl);
        Assert.Equal("planner-read-token", executor.BearerToken);
        var prediction = Assert.Single(result.Predictions);
        Assert.Equal(speciesId, prediction.SpeciesId);
        Assert.Equal(0.82, prediction.HabitatSuitability);
        Assert.Null(prediction.OccurrenceProbability);
        Assert.Null(prediction.ConservationStatus);
        Assert.Equal("it3091-model", result.ModelSource);
        Assert.Equal("fixture-v2", result.ModelVersion);
        Assert.Equal("available", result.Status);
    }

    [Fact]
    [Trait("CaseId", "EXP-MERGE-OPS-001")]
    public async Task Operations_adapter_maps_target_status_and_does_not_treat_uninitialized_as_open()
    {
        var destinationId = Guid.NewGuid();
        var offeringId = Guid.NewGuid();
        var updated = DateTimeOffset.UtcNow;
        var executor = new FakeExecutor(Response($$"""
            {"targetType":"OFFERING","targetId":"{{offeringId}}","operationalState":"CAUTION","stateVersion":4,"updatedAt":"{{updated:O}}"}
            """));
        var service = new OperationalStatusConsumerService(executor, Configuration(), NullLogger<OperationalStatusConsumerService>.Instance,
            AuthenticatedContext("operations-read-token"));

        var status = await service.GetOperationalStatusAsync(destinationId, offeringId);

        Assert.Equal($"http://api.test/api/operations/targets/OFFERING/{offeringId:D}/status", executor.TargetUrl);
        Assert.Equal("operations-read-token", executor.BearerToken);
        Assert.True(status.HasRestriction);
        Assert.Equal("CAUTION", status.OperationalState);
        Assert.Equal(4, status.StateVersion);
        Assert.Equal(updated, status.UpdatedAt);

        executor.Result = new ResilientExecutionResult(true, 404, "{}", 1, 2, "RESPONDED", "not initialized", null);
        var unavailable = await service.GetOperationalStatusAsync(destinationId, offeringId);
        Assert.False(unavailable.HasRestriction);
        Assert.Equal("UNAVAILABLE", unavailable.SourceStatus);
        Assert.Contains("not established", unavailable.Reason);
    }

    [Fact]
    [Trait("CaseId", "EXP-MERGE-OPS-002")]
    public async Task Operations_advisories_use_public_active_alert_contract_and_exclude_out_of_period_rows()
    {
        var destinationId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var executor = new FakeExecutor(Response($$"""
            {"items":[{"alertId":"{{activeId}}","targetType":"DESTINATION","targetId":"{{destinationId}}","title":"High surf","description":"Avoid exposed shore breaks.","severity":"HIGH","visibility":"PUBLIC","lifecycle":"ACTIVE","validFrom":"{{now.AddMinutes(-5):O}}","validUntil":"{{now.AddMinutes(20):O}}"},{"alertId":"{{oldId}}","targetType":"DESTINATION","targetId":"{{destinationId}}","title":"Expired","description":"Expired notice.","severity":"MODERATE","visibility":"PUBLIC","lifecycle":"ACTIVE","validFrom":"{{now.AddHours(-2):O}}","validUntil":"{{now.AddMinutes(-1):O}}"}],"nextCursor":null}
            """));
        var service = new OperationalStatusConsumerService(executor, Configuration(), NullLogger<OperationalStatusConsumerService>.Instance,
            AuthenticatedContext("alerts-read-token"));

        var result = await service.GetOperationalAdvisoriesAsync(destinationId, "Mirissa");

        Assert.Equal($"http://api.test/api/operations/alerts?targetType=DESTINATION&targetId={destinationId:D}&lifecycle=ACTIVE&visibility=PUBLIC&pageSize=100", executor.TargetUrl);
        Assert.Equal("alerts-read-token", executor.BearerToken);
        var advisory = Assert.Single(result.Advisories);
        Assert.Equal(activeId.ToString("D"), advisory.AdvisoryId);
        Assert.Equal("HIGH", advisory.Severity);
        Assert.False(result.FallbackUsed);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["PublicApiUrl"] = "http://api.test" })
        .Build();

    private static IHttpContextAccessor AuthenticatedContext(string token)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Bearer"))
        };
        context.Request.Headers.Authorization = $"Bearer {token}";
        return new HttpContextAccessor { HttpContext = context };
    }

    private static ResilientExecutionResult Response(string content) =>
        new(true, 200, content, 1, 4, "RESPONDED", "fixture response", null);

    private sealed class FakeExecutor(ResilientExecutionResult result) : IResilientHttpExecutor
    {
        public string? TargetUrl { get; private set; }
        public string? BearerToken { get; private set; }
        public ResilientExecutionResult Result { get; set; } = result;

        public Task<ResilientExecutionResult> ExecuteGetAsync(string? targetUrl, TimeSpan perAttemptTimeout,
            int maxRetries = 2, TimeSpan? initialRetryDelay = null, CancellationToken cancellationToken = default)
        {
            TargetUrl = targetUrl;
            BearerToken = null;
            return Task.FromResult(Result);
        }

        public Task<ResilientExecutionResult> ExecuteGetWithAuthorizationAsync(string? targetUrl, TimeSpan perAttemptTimeout,
            int maxRetries, TimeSpan? initialRetryDelay, string? bearerToken, CancellationToken cancellationToken = default)
        {
            TargetUrl = targetUrl;
            BearerToken = bearerToken;
            return Task.FromResult(Result);
        }
    }
}
