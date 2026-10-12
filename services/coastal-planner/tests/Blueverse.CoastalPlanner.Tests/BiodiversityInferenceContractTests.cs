using Blueverse.CoastalPlanner.Integration;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

[Collection(BiodiversityFakeServiceCollection.Name)]
public sealed class BiodiversityInferenceContractTests(BiodiversityFakeServiceFixture fake)
{
    private static readonly Guid DestinationId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid ActivityId = Guid.Parse("4fa85f64-5717-4562-b3fc-2c963f66afa7");

    [Fact]
    [Trait("TestId", "PLANNER-IT3091-001")]
    public async Task Valid_response_preserves_identity_provenance_and_minimal_request()
    {
        var realBaseUrl = Environment.GetEnvironmentVariable("IT3091_TEST_BASE_URL");
        if (!string.IsNullOrWhiteSpace(realBaseUrl))
        {
            var realClient = CreateClient(realBaseUrl,
                Environment.GetEnvironmentVariable("IT3091_TEST_API_KEY"),
                Environment.GetEnvironmentVariable("IT3091_TEST_MODEL_VERSION"));
            var (realResult, realResponded, realNote) = await realClient.GetBiodiversityInferenceAsync(DestinationId, ActivityId);
            Assert.True(realResponded, realNote);
            Assert.Equal("available", realResult!.Status);
            Assert.Equal(DestinationId, realResult.DestinationId);
            Assert.Equal(ActivityId, realResult.ActivityId);
            Assert.False(string.IsNullOrWhiteSpace(realResult.Provider));
            Assert.False(string.IsNullOrWhiteSpace(realResult.ModelVersion));
            Assert.NotNull(realResult.Timestamp);
            Assert.False(string.IsNullOrWhiteSpace(realResult.Limitations));
            return;
        }

        await fake.SetBehaviorAsync("valid");
        var (result, responded, note) = await CreateClient().GetBiodiversityInferenceAsync(DestinationId, ActivityId);
        var state = await fake.GetStateAsync();

        Assert.True(responded);
        Assert.Equal("available", result!.Status);
        Assert.Equal(DestinationId, result.DestinationId);
        Assert.Equal(ActivityId, result.ActivityId);
        Assert.Equal("fake-v1", result.ModelVersion);
        Assert.Equal("synthetic-it3091-fake", result.Provider);
        Assert.NotNull(result.Timestamp);
        Assert.Equal("synthetic-test-only", result.Species![0].ConfidenceLevel);
        Assert.Null(note);
        Assert.True(state.GetProperty("lastHadAuthorization").GetBoolean());
        Assert.True(state.GetProperty("lastAuthorizationMatchesCredential").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(state.GetProperty("lastCorrelationId").GetString()));
        var request = state.GetProperty("lastInput");
        Assert.Equal(1, request.GetProperty("contractVersion").GetInt32());
        Assert.Equal(DestinationId.ToString(), request.GetProperty("destinationId").GetString());
        Assert.Equal(ActivityId.ToString(), request.GetProperty("activityId").GetString());
        Assert.Equal(4, request.EnumerateObject().Count());
        Assert.False(request.TryGetProperty("userId", out _));
        Assert.False(request.TryGetProperty("coordinates", out _));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("wrong-identity")]
    [InlineData("out-of-range")]
    [InlineData("unsupported-location")]
    [Trait("TestId", "PLANNER-IT3091-002")]
    public async Task Invalid_provider_results_are_never_returned_as_predictions(string behavior)
    {
        await fake.SetBehaviorAsync(behavior);

        var (result, responded, _) = await CreateClient().GetBiodiversityInferenceAsync(DestinationId, ActivityId);

        Assert.True(responded);
        Assert.Equal("invalid", result!.Status);
        Assert.Empty(result.Species ?? []);
    }

    [Fact]
    [Trait("TestId", "PLANNER-IT3091-003")]
    public async Task Stale_provider_response_has_a_distinct_outcome_and_no_prediction()
    {
        await fake.SetBehaviorAsync("stale");

        var (result, responded, _) = await CreateClient().GetBiodiversityInferenceAsync(DestinationId, ActivityId);

        Assert.True(responded);
        Assert.Equal("stale", result!.Status);
        Assert.Empty(result.Species ?? []);
    }

    [Fact]
    [Trait("TestId", "PLANNER-IT3091-004")]
    public async Task Server_error_retries_once_then_returns_unavailable()
    {
        await fake.SetBehaviorAsync("5xx");

        var (result, responded, _) = await CreateClient().GetBiodiversityInferenceAsync(DestinationId, ActivityId);
        var state = await fake.GetStateAsync();

        Assert.False(responded);
        Assert.Null(result);
        Assert.Equal(2, state.GetProperty("requestCount").GetInt32());
    }

    [Fact]
    [Trait("TestId", "PLANNER-IT3091-005")]
    public async Task Timeout_retries_once_then_returns_unavailable()
    {
        await fake.SetBehaviorAsync("timeout");

        var (result, responded, _) = await CreateClient().GetBiodiversityInferenceAsync(DestinationId, ActivityId);
        var state = await fake.GetStateAsync();

        Assert.False(responded);
        Assert.Null(result);
        Assert.Equal(2, state.GetProperty("requestCount").GetInt32());
    }

    [Fact]
    [Trait("TestId", "PLANNER-IT3091-006")]
    public async Task Disabled_feature_returns_not_requested_without_calling_provider()
    {
        await fake.SetBehaviorAsync("valid");

        var (result, responded, _) = await CreateClient(enabled: false).GetBiodiversityInferenceAsync(DestinationId, ActivityId);
        var state = await fake.GetStateAsync();

        Assert.True(responded);
        Assert.Equal("not_requested", result!.Status);
        Assert.Equal(0, state.GetProperty("requestCount").GetInt32());
    }

    [Fact]
    [Trait("TestId", "PLANNER-IT3091-007")]
    public async Task Circuit_breaker_short_circuits_repeated_provider_failures()
    {
        await fake.SetBehaviorAsync("5xx");
        var client = CreateClient(failureThreshold: 1);

        var first = await client.GetBiodiversityInferenceAsync(DestinationId, ActivityId);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var second = await client.GetBiodiversityInferenceAsync(DestinationId, ActivityId);
        timer.Stop();
        var state = await fake.GetStateAsync();

        Assert.False(first.Responded);
        Assert.False(second.Responded);
        Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(250));
        Assert.Equal(2, state.GetProperty("requestCount").GetInt32());
    }

    private PeerServicesClient CreateClient(bool enabled = true, int failureThreshold = 3) =>
        CreateClient(fake.BaseUrl, "fixture-only-biodiversity-key", "fake-v1", enabled, failureThreshold);

    private PeerServicesClient CreateClient(string baseUrl, string? apiKey, string? expectedVersion,
        bool enabled = true, int failureThreshold = 3)
    {
        var values = new Dictionary<string, string?>
        {
            ["PeerServices:BiodiversityMlUrl"] = baseUrl,
            ["PeerServices:BiodiversityApiKey"] = apiKey,
            ["PeerServices:BiodiversityEnabled"] = enabled.ToString(),
            ["PeerServices:BiodiversityExpectedModelVersion"] = expectedVersion,
            ["PeerServices:BiodiversityFailureThreshold"] = failureThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["PeerServices:BiodiversityCircuitOpenSeconds"] = "15"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "fixture-user")], "test"))
        };
        context.Request.Headers.Authorization = "Bearer fixture-user-token";
        var contextHandler = new PeerRequestContextHandler(new HttpContextAccessor { HttpContext = context })
        {
            InnerHandler = new HttpClientHandler()
        };
        return new PeerServicesClient(new HttpClient(contextHandler), configuration, NullLogger<PeerServicesClient>.Instance,
            new BiodiversityCircuitBreaker(configuration, TimeProvider.System));
    }
}
