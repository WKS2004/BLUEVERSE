using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Controllers;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blueverse.CoastalOperations.Tests;

public sealed class ComponentDependencyCollectorTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid WorkflowId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly DateTimeOffset StartsAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EndsAt = StartsAt.AddHours(2);
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "COASTAL-DEPENDENCY-001 successful peer responses are validated and captured")]
    public async Task COASTAL_DEPENDENCY_001_SuccessfulPeerResponsesAreCaptured()
    {
        var requests = new ConcurrentBag<CapturedRequest>();
        var collector = CreateCollector(async (request, cancellationToken) =>
        {
            requests.Add(await CaptureAsync(request, cancellationToken));
            return request.RequestUri!.Host switch
            {
                "experience.test" => Json(HttpStatusCode.OK, Availability("AVAILABLE", DateTimeOffset.UtcNow.AddHours(1))),
                "marine.test" => Json(HttpStatusCode.OK, Suitability("UNSUITABLE", DateTimeOffset.UtcNow.AddHours(1))),
                _ => throw new InvalidOperationException("An unrequested peer was contacted.")
            };
        }, out var health);

        var results = await collector.CollectAsync("ACTIVITY", TargetId, StartsAt, EndsAt, null, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal("RESPONDED", Find(results, "member-1-experience").Status);
        Assert.Equal("AVAILABLE", Find(results, "member-1-experience").Data!.AvailabilityStatus);
        Assert.Equal("RESPONDED", Find(results, "member-2-marine-safety").Status);
        Assert.Equal("UNSUITABLE", Find(results, "member-2-marine-safety").Data!.SuitabilityClassification);
        Assert.Equal("NOT_REQUESTED", Find(results, "member-3-coastal-planner").Status);

        var experienceRequest = Assert.Single(requests, item => item.Uri.Host == "experience.test");
        Assert.Equal(HttpMethod.Get, experienceRequest.Method);
        Assert.Equal($"/api/experiences/ACTIVITY/{TargetId:D}/availability", experienceRequest.Uri.AbsolutePath);
        Assert.Contains("periodStartsAt=2026-10-01T08%3A00%3A00.0000000%2B00%3A00", experienceRequest.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("periodEndsAt=2026-10-01T10%3A00%3A00.0000000%2B00%3A00", experienceRequest.Uri.Query, StringComparison.Ordinal);

        var marineRequest = Assert.Single(requests, item => item.Uri.Host == "marine.test");
        Assert.Equal(HttpMethod.Post, marineRequest.Method);
        using var body = JsonDocument.Parse(marineRequest.Body!);
        Assert.Equal("ACTIVITY", body.RootElement.GetProperty("targetType").GetString());
        Assert.Equal(TargetId, body.RootElement.GetProperty("targetId").GetGuid());
        Assert.Equal(StartsAt, body.RootElement.GetProperty("periodStartsAt").GetDateTimeOffset());
        Assert.Equal(EndsAt, body.RootElement.GetProperty("periodEndsAt").GetDateTimeOffset());
        Assert.Equal("NOT_REQUESTED", Find(health.GetSnapshot(), "member-3-coastal-planner").Status);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-002 absent peers exhaust bounded network retries without failing collection")]
    public async Task COASTAL_DEPENDENCY_002_AbsentPeersReturnUnavailableAfterRetries()
    {
        var callCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var collector = CreateCollector((request, _) =>
        {
            callCounts.AddOrUpdate(request.RequestUri!.Host, 1, static (_, current) => current + 1);
            throw new HttpRequestException("Synthetic peer is absent.");
        }, out var health, Options(maxRetries: 2, retryDelayMilliseconds: 0));

        var results = await collector.CollectAsync("ACTIVITY", TargetId, StartsAt, EndsAt, null, CancellationToken.None);

        foreach (var service in new[] { "member-1-experience", "member-2-marine-safety" })
        {
            var result = Find(results, service);
            Assert.Equal("UNAVAILABLE", result.Status);
            Assert.Equal("SERVICE_UNREACHABLE", result.ErrorCode);
            Assert.Equal(3, result.Attempts);
            Assert.Equal(2, result.Retries);
            Assert.True(result.Retryable);
            Assert.Contains("3 attempts (2 retries)", result.Message, StringComparison.Ordinal);
        }

        Assert.Equal(3, callCounts["experience.test"]);
        Assert.Equal(3, callCounts["marine.test"]);
        Assert.Equal("NOT_REQUESTED", Find(results, "member-3-coastal-planner").Status);
        Assert.Equal("UNAVAILABLE", Find(health.GetSnapshot(), "member-1-experience").Status);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-003 timeout retries are bounded per attempt")]
    public async Task COASTAL_DEPENDENCY_003_TimeoutsAreRetriedWithinAttemptLimit()
    {
        var experienceCalls = 0;
        var collector = CreateCollector(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.Host == "experience.test")
            {
                Interlocked.Increment(ref experienceCalls);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return Json(HttpStatusCode.OK, Suitability("CAUTION", DateTimeOffset.UtcNow.AddHours(1)));
        }, out _, Options(timeoutSeconds: 1, maxRetries: 2, retryDelayMilliseconds: 0));

        var results = await collector.CollectAsync("ACTIVITY", TargetId, StartsAt, EndsAt, null, CancellationToken.None);
        var experience = Find(results, "member-1-experience");

        Assert.Equal("UNAVAILABLE", experience.Status);
        Assert.Equal("TIMEOUT", experience.ErrorCode);
        Assert.Equal(3, experience.Attempts);
        Assert.Equal(2, experience.Retries);
        Assert.Equal(3, experienceCalls);
        Assert.Equal("RESPONDED", Find(results, "member-2-marine-safety").Status);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-004 server failures retry while a missing route does not")]
    public async Task COASTAL_DEPENDENCY_004_RetriesServerErrorsAndReportsNotFound()
    {
        var experienceCalls = 0;
        var plannerPath = string.Empty;
        var workflowUpdatedAt = DateTimeOffset.UtcNow;
        var collector = CreateCollector((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.StartsWith("/api/experiences/", StringComparison.Ordinal))
            {
                var count = Interlocked.Increment(ref experienceCalls);
                return Task.FromResult(count < 3
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : Json(HttpStatusCode.OK, Availability("UNKNOWN", null)));
            }

            if (path == "/api/marine-safety/suitability-assessments")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            plannerPath = path;
            return Task.FromResult(Json(HttpStatusCode.OK, new
            {
                workflowId = WorkflowId,
                status = "IN_PROGRESS",
                updatedAt = workflowUpdatedAt
            }));
        }, out _, Options(maxRetries: 2, retryDelayMilliseconds: 0));

        var results = await collector.CollectAsync("ACTIVITY", TargetId, StartsAt, EndsAt, WorkflowId, CancellationToken.None);

        var experience = Find(results, "member-1-experience");
        Assert.Equal("RESPONDED", experience.Status);
        Assert.Equal(3, experience.Attempts);
        Assert.Equal(2, experience.Retries);
        Assert.Equal("UNKNOWN", experience.Data!.AvailabilityStatus);

        var marine = Find(results, "member-2-marine-safety");
        Assert.Equal("ENDPOINT_NOT_FOUND", marine.Status);
        Assert.Equal("HTTP_404", marine.ErrorCode);
        Assert.Equal(1, marine.Attempts);
        Assert.Equal(0, marine.Retries);

        var planner = Find(results, "member-3-coastal-planner");
        Assert.Equal("RESPONDED", planner.Status);
        Assert.Equal("IN_PROGRESS", planner.Data!.WorkflowStatus);
        Assert.Equal(workflowUpdatedAt, planner.Data.ObservedAt);
        Assert.Equal($"/api/coastal-planner/workflows/{WorkflowId:D}", plannerPath);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-005 expired source evidence is marked stale and preserved")]
    public async Task COASTAL_DEPENDENCY_005_ExpiredEvidenceRemainsVisibleAsStale()
    {
        var collector = CreateCollector((request, _) => Task.FromResult(
            request.RequestUri!.Host == "experience.test"
                ? Json(HttpStatusCode.OK, Availability("AVAILABLE", DateTimeOffset.UtcNow.AddMinutes(-1)))
                : Json(HttpStatusCode.OK, Suitability("SUITABLE", DateTimeOffset.UtcNow.AddHours(1)))), out _);

        var results = await collector.CollectAsync("ACTIVITY", TargetId, StartsAt, EndsAt, null, CancellationToken.None);
        var stale = Find(results, "member-1-experience");

        Assert.Equal("STALE", stale.Status);
        Assert.Equal("EVIDENCE_EXPIRED", stale.ErrorCode);
        Assert.Equal("AVAILABLE", stale.Data!.AvailabilityStatus);
        Assert.True(stale.Data.ValidUntil < DateTimeOffset.UtcNow);
        Assert.Equal(1, stale.Attempts);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-006 malformed and oversized peer payloads are rejected")]
    public async Task COASTAL_DEPENDENCY_006_InvalidPayloadsAreBoundedAndRejected()
    {
        var collector = CreateCollector((request, _) => Task.FromResult(
            request.RequestUri!.Host == "experience.test"
                ? Json(HttpStatusCode.OK, new { })
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(new string('x', 1025), Encoding.UTF8, "application/json")
                }), out _, Options(maxResponseBytes: 1024));

        var results = await collector.CollectAsync("ACTIVITY", TargetId, StartsAt, EndsAt, null, CancellationToken.None);

        var malformed = Find(results, "member-1-experience");
        Assert.Equal("INVALID_RESPONSE", malformed.Status);
        Assert.Equal("INVALID_SCHEMA", malformed.ErrorCode);
        Assert.Equal(1, malformed.Attempts);

        var oversized = Find(results, "member-2-marine-safety");
        Assert.Equal("INVALID_RESPONSE", oversized.Status);
        Assert.Equal("RESPONSE_TOO_LARGE", oversized.ErrorCode);
        Assert.Equal(1, oversized.Attempts);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-007 caller cancellation is propagated without retries")]
    public async Task COASTAL_DEPENDENCY_007_CallerCancellationIsPropagated()
    {
        var calls = 0;
        var collector = CreateCollector(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, out _, Options(timeoutSeconds: 2, maxRetries: 2, retryDelayMilliseconds: 0));
        using var callerCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collector.CollectAsync(
            "ACTIVITY", TargetId, StartsAt, EndsAt, null, callerCancellation.Token));

        Assert.InRange(calls, 1, 2);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-008 invalid peer address is reported without sending a request")]
    public async Task COASTAL_DEPENDENCY_008_MisconfiguredPeerIsIsolated()
    {
        var requests = 0;
        var options = Options();
        options.Member1Experience.BaseAddress = "file:///not-an-http-service";
        var collector = CreateCollector((_, _) =>
        {
            Interlocked.Increment(ref requests);
            return Task.FromResult(Json(HttpStatusCode.OK, Suitability("SUITABLE", DateTimeOffset.UtcNow.AddHours(1))));
        }, out _, options);

        var results = await collector.CollectAsync("ACTIVITY", TargetId, StartsAt, EndsAt, null, CancellationToken.None);

        var misconfigured = Find(results, "member-1-experience");
        Assert.Equal("MISCONFIGURED", misconfigured.Status);
        Assert.Equal("ENDPOINT_CONFIGURATION_INVALID", misconfigured.ErrorCode);
        Assert.Equal(0, misconfigured.Attempts);
        Assert.Equal(1, requests);
        Assert.Equal("RESPONDED", Find(results, "member-2-marine-safety").Status);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-009 optional peer failures do not change process liveness")]
    public void COASTAL_DEPENDENCY_009_PeerFailureDoesNotAffectLiveness()
    {
        var registry = new ComponentDependencyHealthRegistry();
        registry.Record(new ComponentDependencyResult(
            "member-1-experience", "experience-availability", "UNAVAILABLE", 3, 2, true,
            "SERVICE_UNREACHABLE", "The endpoint did not respond after 3 attempts (2 retries).", DateTimeOffset.UtcNow, null));
        var dbOptions = new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=not-used")
            .Options;
        using var db = new CoastalOperationsDbContext(dbOptions);
        var controller = new HealthController(db, registry);

        var result = Assert.IsType<OkObjectResult>(controller.Live());

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("coastal-operations", result.Value!.GetType().GetProperty("service")!.GetValue(result.Value));
        Assert.Equal("live", result.Value.GetType().GetProperty("status")!.GetValue(result.Value));
        Assert.Equal("UNAVAILABLE", Find(registry.GetSnapshot(), "member-1-experience").Status);
    }

    private static ComponentDependencyCollector CreateCollector(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        out ComponentDependencyHealthRegistry health,
        ComponentDependencyOptions? options = null)
    {
        health = new ComponentDependencyHealthRegistry();
        return new ComponentDependencyCollector(
            new StubHttpClientFactory(new DelegateHandler(send)),
            Microsoft.Extensions.Options.Options.Create(options ?? Options()),
            health,
            NullLogger<ComponentDependencyCollector>.Instance);
    }

    private static ComponentDependencyOptions Options(
        int timeoutSeconds = 2,
        int maxRetries = 2,
        int retryDelayMilliseconds = 0,
        int maxResponseBytes = 32768) => new()
    {
        TimeoutSeconds = timeoutSeconds,
        MaxRetries = maxRetries,
        RetryDelayMilliseconds = retryDelayMilliseconds,
        MaxResponseBytes = maxResponseBytes,
        Member1Experience = new ComponentEndpointOptions
        {
            BaseAddress = "https://experience.test",
            AvailabilityPath = "/api/experiences/{targetType}/{targetId}/availability"
        },
        Member2MarineSafety = new ComponentEndpointOptions
        {
            BaseAddress = "https://marine.test",
            SuitabilityPath = "/api/marine-safety/suitability-assessments"
        },
        Member3CoastalPlanner = new ComponentEndpointOptions
        {
            BaseAddress = "https://planner.test",
            WorkflowPath = "/api/coastal-planner/workflows/{workflowId}"
        }
    };

    private static object Availability(string status, DateTimeOffset? validUntil)
    {
        var evaluatedAt = validUntil?.AddMinutes(-1) ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        return new
        {
            targetType = "ACTIVITY",
            targetId = TargetId,
            availabilityStatus = status,
            targetVersion = "experience-v1",
            evaluatedAt,
            validUntil
        };
    }

    private static object Suitability(string classification, DateTimeOffset? validUntil)
    {
        var assessedAt = validUntil?.AddMinutes(-1) ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        return new
        {
            targetType = "ACTIVITY",
            targetId = TargetId,
            classification,
            profileVersion = "marine-profile-v1",
            assessedAt,
            validUntil,
            reasonCodes = new[] { "SOURCE_CHECKED" }
        };
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, WebJson), Encoding.UTF8, "application/json")
    };

    private static async Task<CapturedRequest> CaptureAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        new(request.Method, request.RequestUri!, request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));

    private static T Find<T>(IEnumerable<T> items, string service) where T : class =>
        items.Single(item => (string)item.GetType().GetProperty("Service")!.GetValue(item)! == service);

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(ComponentDependencyCollector.HttpClientName, name);
            return new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        }
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
