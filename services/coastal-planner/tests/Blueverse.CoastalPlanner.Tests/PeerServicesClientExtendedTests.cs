using System.Net;
using System.Net.Http.Json;
using Blueverse.CoastalPlanner.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class PeerServicesClientExtendedTests
{
    [Fact]
    [Trait("TestId", "PLANNER-PEER-CAT-001")]
    public async Task Catalogue_returns_unavailable_when_url_is_not_configured()
    {
        var config = CreateConfiguration(catalogueUrl: null);
        using var http = new HttpClient(new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var client = new PeerServicesClient(http, config, NullLogger<PeerServicesClient>.Instance);

        var (items, responded, note) = await client.GetCatalogueOfferingsAsync(Guid.NewGuid(), null);

        Assert.False(responded);
        Assert.Empty(items);
        Assert.Contains("endpoint URL is not configured", note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-CAT-002")]
    public async Task Catalogue_appends_activity_ids_to_query_string()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new RecordingHandler((req, _) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<PeerCatalogueItem>())
            });
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var destinationId = Guid.NewGuid();
        var act1 = Guid.NewGuid();
        var act2 = Guid.NewGuid();

        await client.GetCatalogueOfferingsAsync(destinationId, [act1, act2]);

        Assert.NotNull(capturedRequest);
        var uri = capturedRequest.RequestUri?.ToString();
        Assert.NotNull(uri);
        Assert.Contains($"destinationId={destinationId}", uri, StringComparison.Ordinal);
        Assert.Contains($"activityIds={act1}%2C{act2}", uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-MAR-001")]
    public async Task Marine_returns_unavailable_when_url_is_not_configured()
    {
        var config = CreateConfiguration(marineUrl: "");
        using var http = new HttpClient(new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var client = new PeerServicesClient(http, config, NullLogger<PeerServicesClient>.Instance);

        var (result, responded, note) = await client.GetMarineSuitabilityAsync(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(2));

        Assert.False(responded);
        Assert.Null(result);
        Assert.Contains("endpoint URL is not configured", note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-MAR-002")]
    public async Task Marine_rejects_response_with_mismatched_activity_id()
    {
        var destinationId = Guid.NewGuid();
        var requestedActivityId = Guid.NewGuid();
        var differentActivityId = Guid.NewGuid();

        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PeerSuitabilityResponse(
                destinationId, differentActivityId, "SUITABLE", DateTime.UtcNow, Guid.NewGuid(), null))
        }));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var (result, responded, note) = await client.GetMarineSuitabilityAsync(
            destinationId, requestedActivityId, DateTime.UtcNow, DateTime.UtcNow.AddHours(2));

        Assert.False(responded);
        Assert.Null(result);
        Assert.Contains("invalid suitability response", note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-MAR-003")]
    public async Task Marine_accepts_unsuitable_and_unknown_status_without_safety_profile()
    {
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PeerSuitabilityResponse(
                destinationId, activityId, "UNSUITABLE", DateTime.UtcNow, null, "Dangerous conditions"))
        }));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var (result, responded, note) = await client.GetMarineSuitabilityAsync(
            destinationId, activityId, DateTime.UtcNow, DateTime.UtcNow.AddHours(2));

        Assert.True(responded);
        Assert.NotNull(result);
        Assert.Equal("UNSUITABLE", result.Status);
        Assert.Null(result.SafetyProfileId);
        Assert.Null(note);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-MAR-004")]
    public async Task Marine_accepts_caution_status_with_valid_safety_profile()
    {
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var safetyProfileId = Guid.NewGuid();

        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PeerSuitabilityResponse(
                destinationId, activityId, "CAUTION", DateTime.UtcNow, safetyProfileId, "Mild swell"))
        }));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var (result, responded, note) = await client.GetMarineSuitabilityAsync(
            destinationId, activityId, DateTime.UtcNow, DateTime.UtcNow.AddHours(2));

        Assert.True(responded);
        Assert.NotNull(result);
        Assert.Equal("CAUTION", result.Status);
        Assert.Equal(safetyProfileId, result.SafetyProfileId);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-OP-001")]
    public async Task Operations_returns_unavailable_when_url_is_not_configured()
    {
        var config = CreateConfiguration(operationsUrl: null);
        using var http = new HttpClient(new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var client = new PeerServicesClient(http, config, NullLogger<PeerServicesClient>.Instance);

        var (result, responded, note) = await client.GetOperationalStatusAsync(Guid.NewGuid());

        Assert.False(responded);
        Assert.Null(result);
        Assert.Contains("endpoint URL is not configured", note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-OP-002")]
    public async Task Operations_rejects_response_with_mismatched_destination_or_unrecognized_status()
    {
        var destinationId = Guid.NewGuid();

        // Mismatched destination
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PeerOperationStatusResponse(Guid.NewGuid(), "OPEN", []))
        }));
        using var http1 = new HttpClient(handler);
        var client1 = CreateClient(http1);
        var (result1, responded1, note1) = await client1.GetOperationalStatusAsync(destinationId);
        Assert.False(responded1);
        Assert.Null(result1);
        Assert.Contains("invalid status response", note1, StringComparison.Ordinal);

        // Unrecognized status
        var handler2 = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PeerOperationStatusResponse(destinationId, "UNKNOWN_STATUS", []))
        }));
        using var http2 = new HttpClient(handler2);
        var client2 = CreateClient(http2);
        var (result2, responded2, note2) = await client2.GetOperationalStatusAsync(destinationId);
        Assert.False(responded2);
        Assert.Null(result2);
        Assert.Contains("invalid status response", note2, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-OP-003")]
    public async Task Operations_accepts_valid_statuses()
    {
        var destinationId = Guid.NewGuid();
        foreach (var status in new[] { "OPEN", "CAUTION", "TEMPORARILY_SUSPENDED", "CANCELLED", "COMPLETED" })
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PeerOperationStatusResponse(destinationId, status, ["Advisory text"]))
            }));
            using var http = new HttpClient(handler);
            var client = CreateClient(http);

            var (result, responded, note) = await client.GetOperationalStatusAsync(destinationId);

            Assert.True(responded);
            Assert.NotNull(result);
            Assert.Equal(status, result.OperationalStatus);
            Assert.Null(note);
        }
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-BIO-001")]
    public async Task Biodiversity_returns_unavailable_when_url_is_not_configured()
    {
        var config = CreateConfiguration(bioUrl: null);
        using var http = new HttpClient(new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var client = new PeerServicesClient(http, config, NullLogger<PeerServicesClient>.Instance);

        var (result, responded, note) = await client.GetBiodiversityInferenceAsync(Guid.NewGuid(), null);

        Assert.False(responded);
        Assert.Null(result);
        Assert.Contains("endpoint URL is not configured", note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-BIO-002")]
    public async Task Biodiversity_rejects_model_version_or_confidence_exceeding_64_characters()
    {
        var destId = Guid.NewGuid();
        var actId = Guid.NewGuid();

        // Model version > 64
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PeerBiodiversityInferenceResponse(
                destId, actId, "AVAILABLE",
                [new PeerSpeciesInference(Guid.NewGuid(), "Scientific", "Common", 0.7, "HIGH")],
                new string('V', 65), DateTime.UtcNow, null))
        }));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var (result, responded, note) = await client.GetBiodiversityInferenceAsync(destId, actId);

        Assert.False(responded);
        Assert.Null(result);
        Assert.Contains("invalid or unavailable inference", note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-BIO-003")]
    public async Task Biodiversity_accepts_valid_response_and_appends_activity_id_when_provided()
    {
        HttpRequestMessage? captured = null;
        var destId = Guid.NewGuid();
        var actId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var handler = new RecordingHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PeerBiodiversityInferenceResponse(
                    destId, actId, "AVAILABLE",
                    [new PeerSpeciesInference(speciesId, "Scientific Name", "Common Name", 0.85, "HIGH")],
                    "model-v1", now, "Limitations"))
            });
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var (result, responded, note) = await client.GetBiodiversityInferenceAsync(destId, actId);

        Assert.True(responded);
        Assert.NotNull(result);
        Assert.Equal("AVAILABLE", result.Status);
        Assert.Equal(speciesId, result.Species?[0].SpeciesId);
        Assert.Null(note);

        Assert.NotNull(captured);
        Assert.Contains($"destinationId={destId}", captured.RequestUri?.ToString(), StringComparison.Ordinal);
        Assert.Contains($"activityId={actId}", captured.RequestUri?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestId", "PLANNER-PEER-CFG-001")]
    public void PeerServicesClient_rejects_out_of_range_timeout_or_retries()
    {
        using var http = new HttpClient(new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        // Timeout < 1
        Assert.Throws<InvalidOperationException>(() => new PeerServicesClient(http, CreateConfiguration(timeoutSeconds: 0), NullLogger<PeerServicesClient>.Instance));

        // Timeout > 30
        Assert.Throws<InvalidOperationException>(() => new PeerServicesClient(http, CreateConfiguration(timeoutSeconds: 31), NullLogger<PeerServicesClient>.Instance));

        // Retry < 0
        Assert.Throws<InvalidOperationException>(() => new PeerServicesClient(http, CreateConfiguration(retryCount: -1), NullLogger<PeerServicesClient>.Instance));

        // Retry > 5
        Assert.Throws<InvalidOperationException>(() => new PeerServicesClient(http, CreateConfiguration(retryCount: 6), NullLogger<PeerServicesClient>.Instance));
    }

    private static PeerServicesClient CreateClient(HttpClient http) =>
        new(http, CreateConfiguration(), NullLogger<PeerServicesClient>.Instance);

    private static IConfiguration CreateConfiguration(
        int timeoutSeconds = 3,
        int retryCount = 1,
        string? catalogueUrl = "http://experience-catalogue:8080",
        string? marineUrl = "http://marine-conditions:8080",
        string? operationsUrl = "http://coastal-operations:8080",
        string? bioUrl = "http://it3091-biodiversity:5000") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PeerServices:ExperienceCatalogueUrl"] = catalogueUrl,
                ["PeerServices:MarineConditionsUrl"] = marineUrl,
                ["PeerServices:CoastalOperationsUrl"] = operationsUrl,
                ["PeerServices:BiodiversityMlUrl"] = bioUrl,
                ["PeerServices:TimeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["PeerServices:RetryCount"] = retryCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .Build();

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
