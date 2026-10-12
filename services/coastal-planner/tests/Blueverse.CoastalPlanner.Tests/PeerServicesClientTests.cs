using System.Net;
using System.Net.Http.Json;
using Blueverse.CoastalPlanner.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class PeerServicesClientTests
{
    [Fact]
    [Trait("TestId", "PLANNER-PEER-RETRY-001")]
    public async Task Failed_peer_response_uses_initial_attempt_plus_configured_retries()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var http = new HttpClient(handler);
        var client = CreateClient(http, retryCount: 2);

        var (items, responded, note) = await client.GetCatalogueOfferingsAsync(Guid.NewGuid(), null);

        Assert.False(responded);
        Assert.Empty(items);
        Assert.Equal(3, handler.CallCount);
        Assert.Contains("2 retries", note, StringComparison.Ordinal);
        Assert.Contains("3 attempts maximum", note, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-CONTRACT-001")]
    public async Task Catalogue_adapter_omits_entries_without_an_offering_or_valid_state()
    {
        var destinationId = Guid.NewGuid();
        var valid = new PeerCatalogueItem(destinationId, Guid.NewGuid(), Guid.NewGuid(), "Valid offering", "AVAILABLE", "PUBLISHED",
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), ["INTERMEDIATE"], DateTime.UtcNow,
            "Asia/Colombo", 5.94, 80.45, "Snorkelling", "Southern Province", "Mirissa");
        var invalidOffering = new PeerCatalogueItem(destinationId, Guid.NewGuid(), null, "Missing offering", "AVAILABLE", "PUBLISHED");
        var invalidAvailability = new PeerCatalogueItem(destinationId, Guid.NewGuid(), Guid.NewGuid(), "Invalid state", "MAYBE", "PUBLISHED");
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { valid, invalidOffering, invalidAvailability })
        }));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var (items, responded, note) = await client.GetCatalogueOfferingsAsync(destinationId, null);

        Assert.True(responded);
        var item = Assert.Single(items);
        Assert.Equivalent(valid, item);
        Assert.Equal(valid.ExperienceLevels, item.ExperienceLevels);
        Assert.Contains("invalid or mismatched offerings", note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-CONTRACT-002")]
    public async Task Marine_adapter_posts_the_M2_request_and_preserves_assessment_evidence()
    {
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var requestedAt = DateTime.UtcNow.AddDays(1);
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var assessmentId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var handler = new RecordingHandler(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = "SUITABLE", activityId, activityName = "Snorkelling",
                    location = new { latitude = 5.94m, longitude = 80.45m },
                    requestedTime = requestedAt, forecastTime = requestedAt, evaluatedAt = requestedAt,
                    conditions = new { windSpeed = 10m, waveHeight = 0.8m, swellHeight = 0.7m },
                    source = "Open-Meteo", retrievedAt = requestedAt, freshness = "FRESH",
                    missingFields = Array.Empty<string>(), violations = Array.Empty<string>(), cautionFactors = Array.Empty<string>(),
                    assessmentId, snapshotId
                })
            };
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var (result, responded, note) = await client.GetMarineSuitabilityAsync(
            destinationId, activityId, 5.94, 80.45, requestedAt);

        Assert.True(responded);
        Assert.NotNull(result);
        Assert.Null(note);
        Assert.Equal("SUITABLE", result.Status);
        Assert.Null(result.SafetyProfileId);
        Assert.Equal(assessmentId, result.AssessmentId);
        Assert.Equal(snapshotId, result.SnapshotId);
        Assert.True(result.IsFresh);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("/api/marine/evaluate", capturedRequest.RequestUri!.AbsolutePath);
        Assert.NotNull(capturedBody);
        using var requestDocument = System.Text.Json.JsonDocument.Parse(capturedBody);
        var requestRoot = requestDocument.RootElement;
        Assert.Equal(activityId, requestRoot.GetProperty("activityId").GetGuid());
        Assert.Equal(5.94m, requestRoot.GetProperty("latitude").GetDecimal());
        Assert.Equal(80.45m, requestRoot.GetProperty("longitude").GetDecimal());
        Assert.Equal(requestedAt, requestRoot.GetProperty("dateTime").GetDateTime());
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-CONTRACT-003")]
    public async Task Marine_adapter_accepts_a_forecast_timestamp_inside_the_future_itinerary_window()
    {
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var requestedAt = DateTime.UtcNow.AddDays(2);
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                status = "SUITABLE", activityId, activityName = "Snorkelling",
                location = new { latitude = 5.94m, longitude = 80.45m },
                requestedTime = requestedAt, forecastTime = requestedAt, evaluatedAt = requestedAt,
                conditions = new { windSpeed = 10m, waveHeight = 0.8m, swellHeight = 0.7m },
                source = "Open-Meteo", retrievedAt = requestedAt, freshness = "FRESH",
                missingFields = Array.Empty<string>(), violations = Array.Empty<string>(), cautionFactors = Array.Empty<string>(),
                assessmentId = Guid.NewGuid(), snapshotId = Guid.NewGuid()
            })
        }));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var (result, responded, note) = await client.GetMarineSuitabilityAsync(
            destinationId, activityId, 5.94, 80.45, requestedAt);

        Assert.True(responded);
        Assert.Equal(requestedAt, result?.ConditionTimestamp);
        Assert.Null(note);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-TIMEOUT-001")]
    public async Task Peer_request_timeout_is_bounded_and_retried()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http, timeoutSeconds: 1, retryCount: 1);

        var startedAt = DateTime.UtcNow;
        var (items, responded, note) = await client.GetCatalogueOfferingsAsync(Guid.NewGuid(), null);
        var elapsed = DateTime.UtcNow - startedAt;

        Assert.False(responded);
        Assert.Empty(items);
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("timed out after 1 seconds", note, StringComparison.Ordinal);
        Assert.True(elapsed < TimeSpan.FromSeconds(4), $"Peer calls took {elapsed.TotalSeconds:0.00} seconds.");
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-CANCEL-001")]
    public async Task Caller_cancellation_stops_retries_and_propagates()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http, timeoutSeconds: 3, retryCount: 3);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await client.GetCatalogueOfferingsAsync(Guid.NewGuid(), null, cancellation.Token));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-CONFIG-001")]
    public void Invalid_retry_configuration_is_rejected_at_client_creation()
    {
        using var http = new HttpClient(new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var configuration = CreateConfiguration(timeoutSeconds: 3, retryCount: 6);

        Assert.Throws<InvalidOperationException>(() => new PeerServicesClient(http, configuration, NullLogger<PeerServicesClient>.Instance));
    }

    private static PeerServicesClient CreateClient(HttpClient http, int timeoutSeconds = 3, int retryCount = 1) =>
        new(http, CreateConfiguration(timeoutSeconds, retryCount), NullLogger<PeerServicesClient>.Instance);

    private static IConfiguration CreateConfiguration(int timeoutSeconds, int retryCount) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PeerServices:ExperienceCatalogueUrl"] = "http://api:8080",
                ["PeerServices:MarineConditionsUrl"] = "http://api:8080",
                ["PeerServices:CoastalOperationsUrl"] = "http://api:8080",
                ["PeerServices:BiodiversityMlUrl"] = "http://it3091-biodiversity:5000",
                ["PeerServices:TimeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["PeerServices:RetryCount"] = retryCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .Build();

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return send(request, cancellationToken);
        }
    }
}
