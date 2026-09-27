using System.Net;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Tests;

public sealed class ComponentDependencyEdgeCaseTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly DateTimeOffset EvaluatedAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory(DisplayName = "COASTAL-DEPENDENCY-010 transient 408 and 429 responses retry before success")]
    [Trait("TestId", "COASTAL-DEPENDENCY-010")]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task TransientClientStatusesAreRetried(HttpStatusCode transientStatus)
    {
        var calls = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var collector = CreateCollector((request, _) =>
        {
            var host = request.RequestUri!.Host;
            var call = calls.AddOrUpdate(host, 1, static (_, current) => current + 1);
            if (call == 1)
                return Task.FromResult(new HttpResponseMessage(transientStatus));
            return Task.FromResult(host == "experience.test"
                ? Json(HttpStatusCode.OK, Availability("AVAILABLE", DateTimeOffset.UtcNow.AddMinutes(5)))
                : Json(HttpStatusCode.OK, Suitability()));
        }, retries: 1);

        var result = await collector.CollectAsync("ACTIVITY", TargetId, EvaluatedAt, EvaluatedAt.AddHours(1), null, CancellationToken.None);
        var experience = result.Single(item => item.Service == "member-1-experience");

        Assert.Equal("RESPONDED", experience.Status);
        Assert.Equal(2, experience.Attempts);
        Assert.Equal(1, experience.Retries);
        Assert.Equal(2, calls["experience.test"]);
        Assert.Equal(2, calls["marine.test"]);
    }

    [Theory(DisplayName = "COASTAL-DEPENDENCY-011 client authorization and validation errors are not retried")]
    [Trait("TestId", "COASTAL-DEPENDENCY-011")]
    [InlineData(HttpStatusCode.BadRequest, "UNAVAILABLE", "HTTP_400")]
    [InlineData(HttpStatusCode.Unauthorized, "REJECTED", "HTTP_401")]
    [InlineData(HttpStatusCode.Forbidden, "REJECTED", "HTTP_403")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "UNAVAILABLE", "HTTP_422")]
    public async Task NonRetryableHttpResponsesReturnImmediately(HttpStatusCode status, string expectedStatus, string errorCode)
    {
        var calls = 0;
        var collector = CreateCollector((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new HttpResponseMessage(status));
        }, retries: 4);

        var result = await collector.CollectAsync("ACTIVITY", TargetId, EvaluatedAt, EvaluatedAt.AddHours(1), null, CancellationToken.None);
        var experience = result.Single(item => item.Service == "member-1-experience");

        Assert.Equal(expectedStatus, experience.Status);
        Assert.Equal(errorCode, experience.ErrorCode);
        Assert.Equal(1, experience.Attempts);
        Assert.Equal(0, experience.Retries);
        Assert.False(experience.Retryable);
        Assert.Equal(2, calls); // one response per configured peer
    }

    [Theory(DisplayName = "COASTAL-DEPENDENCY-012 response media type and JSON syntax are validated")]
    [Trait("TestId", "COASTAL-DEPENDENCY-012")]
    [InlineData("text/plain", "{}", "INVALID_CONTENT_TYPE")]
    [InlineData("application/json", "{broken", "INVALID_RESPONSE")]
    public async Task InvalidWireRepresentationIsRejected(string mediaType, string body, string expectedError)
    {
        var collector = CreateCollector((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        }));

        var result = await collector.CollectAsync("ACTIVITY", TargetId, EvaluatedAt, EvaluatedAt.AddHours(1), null, CancellationToken.None);
        var experience = result.Single(item => item.Service == "member-1-experience");

        Assert.Equal("INVALID_RESPONSE", experience.Status);
        Assert.Equal(expectedError, experience.ErrorCode);
        Assert.Equal(1, experience.Attempts);
        Assert.False(experience.Retryable);
    }

    [Theory(DisplayName = "COASTAL-DEPENDENCY-013 peer payload identity, enums and evidence timestamps are validated")]
    [Trait("TestId", "COASTAL-DEPENDENCY-013")]
    [InlineData("wrong-target")]
    [InlineData("unknown-status")]
    [InlineData("invalid-validity")]
    public async Task StructurallyInvalidPeerEvidenceIsRejected(string scenario)
    {
        object payload = scenario switch
        {
            "wrong-target" => Availability("AVAILABLE", DateTimeOffset.UtcNow.AddMinutes(5), targetId: Guid.NewGuid()),
            "unknown-status" => Availability("MAYBE", DateTimeOffset.UtcNow.AddMinutes(5)),
            "invalid-validity" => Availability("AVAILABLE", DateTimeOffset.UtcNow.AddMinutes(-2)),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var collector = CreateCollector((_, _) => Task.FromResult(Json(HttpStatusCode.OK, payload)));

        var result = await collector.CollectAsync("ACTIVITY", TargetId, EvaluatedAt, EvaluatedAt.AddHours(1), null, CancellationToken.None);
        var experience = result.Single(item => item.Service == "member-1-experience");

        Assert.Equal("INVALID_RESPONSE", experience.Status);
        Assert.Equal("INVALID_SCHEMA", experience.ErrorCode);
        Assert.Null(experience.Data);
        Assert.Equal(1, experience.Attempts);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-014 streaming payload limit is enforced without Content-Length")]
    [Trait("TestId", "COASTAL-DEPENDENCY-014")]
    public async Task StreamingBodyLimitIsEnforced()
    {
        var collector = CreateCollector((request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(Encoding.UTF8.GetBytes(new string('x', 2048)))
        }), maxBytes: 1024);

        var result = await collector.CollectAsync("ACTIVITY", TargetId, EvaluatedAt, EvaluatedAt.AddHours(1), null, CancellationToken.None);
        var experience = result.Single(item => item.Service == "member-1-experience");

        Assert.Equal("INVALID_RESPONSE", experience.Status);
        Assert.Equal("RESPONSE_TOO_LARGE", experience.ErrorCode);
        Assert.Equal(1, experience.Attempts);
        Assert.False(experience.Retryable);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-016 configuration cannot raise the retry ceiling above two retries")]
    [Trait("TestId", "COASTAL-DEPENDENCY-016")]
    public async Task RetryCountIsCappedAtTheContractMaximum()
    {
        var calls = 0;
        var collector = CreateCollector((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }, retries: 99);

        var result = await collector.CollectAsync("ACTIVITY", TargetId, EvaluatedAt, EvaluatedAt.AddHours(1), null, CancellationToken.None);
        var experience = result.Single(item => item.Service == "member-1-experience");

        Assert.Equal("UNAVAILABLE", experience.Status);
        Assert.Equal(3, experience.Attempts);
        Assert.Equal(2, experience.Retries);
        Assert.Equal(6, calls);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-017 configured response limit cannot exceed the 32 KiB contract bound")]
    [Trait("TestId", "COASTAL-DEPENDENCY-017")]
    public async Task ResponseSizeIsCappedAtThirtyTwoKiB()
    {
        var payload = JsonSerializer.Serialize(new
        {
            targetType = "ACTIVITY",
            targetId = TargetId,
            availabilityStatus = "AVAILABLE",
            targetVersion = "experience-v1",
            evaluatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            validUntil = DateTimeOffset.UtcNow.AddMinutes(5),
            padding = new string('x', 40 * 1024)
        });
        var collector = CreateCollector((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }), maxBytes: 64 * 1024);

        var result = await collector.CollectAsync("ACTIVITY", TargetId, EvaluatedAt, EvaluatedAt.AddHours(1), null, CancellationToken.None);
        var experience = result.Single(item => item.Service == "member-1-experience");

        Assert.Equal("INVALID_RESPONSE", experience.Status);
        Assert.Equal("RESPONSE_TOO_LARGE", experience.ErrorCode);
        Assert.Equal(1, experience.Attempts);
        Assert.False(experience.Retryable);
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-018 configured peer timeout cannot exceed two seconds")]
    [Trait("TestId", "COASTAL-DEPENDENCY-018")]
    public async Task TimeoutIsCappedAtTwoSeconds()
    {
        var collector = CreateCollector(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, timeoutSeconds: 10);
        using var overallLimit = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var startedAt = DateTimeOffset.UtcNow;

        var result = await collector.CollectAsync("ACTIVITY", TargetId, EvaluatedAt, EvaluatedAt.AddHours(1), null, overallLimit.Token);
        var experience = result.Single(item => item.Service == "member-1-experience");

        Assert.Equal("UNAVAILABLE", experience.Status);
        Assert.Equal("TIMEOUT", experience.ErrorCode);
        Assert.Equal(1, experience.Attempts);
        Assert.True(DateTimeOffset.UtcNow - startedAt < TimeSpan.FromSeconds(4));
    }

    [Fact(DisplayName = "COASTAL-DEPENDENCY-015 dependency health snapshots default every peer and ignore out-of-order updates")]
    [Trait("TestId", "COASTAL-DEPENDENCY-015")]
    public void HealthRegistryProvidesCompleteMonotonicSnapshot()
    {
        var registry = new ComponentDependencyHealthRegistry();
        var latest = new ComponentDependencyResult("member-1-experience", "experience-availability", "RESPONDED", 1, 0, false,
            null, "current", EvaluatedAt.AddMinutes(2), null);
        var stale = latest with { Status = "UNAVAILABLE", CheckedAt = EvaluatedAt };
        registry.Record(latest);
        registry.Record(stale);
        var snapshot = registry.GetSnapshot();

        Assert.Equal(3, snapshot.Count);
        Assert.Equal("RESPONDED", snapshot.Single(item => item.Service == "member-1-experience").Status);
        Assert.Equal("NOT_CHECKED", snapshot.Single(item => item.Service == "member-2-marine-safety").Status);
        Assert.Equal("NOT_CHECKED", snapshot.Single(item => item.Service == "member-3-coastal-planner").Status);
        Assert.Null(snapshot.Single(item => item.Service == "member-2-marine-safety").CheckedAt);
    }

    private static ComponentDependencyCollector CreateCollector(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        int retries = 0,
        int maxBytes = 32768,
        int timeoutSeconds = 2)
    {
        var options = new ComponentDependencyOptions
        {
            TimeoutSeconds = timeoutSeconds,
            MaxRetries = retries,
            RetryDelayMilliseconds = 0,
            MaxResponseBytes = maxBytes,
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
        return new ComponentDependencyCollector(
            new StubHttpClientFactory(new DelegateHandler(send)),
            Options.Create(options), new ComponentDependencyHealthRegistry(), NullLogger<ComponentDependencyCollector>.Instance);
    }

    private static object Availability(string status, DateTimeOffset? validUntil, Guid? targetId = null) => new
    {
        targetType = "ACTIVITY",
        targetId = targetId ?? TargetId,
        availabilityStatus = status,
        targetVersion = "experience-v1",
        evaluatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        validUntil
    };

    private static object Suitability() => new
    {
        targetType = "ACTIVITY", targetId = TargetId, classification = "SUITABLE",
        profileVersion = "marine-v1", assessedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        validUntil = DateTimeOffset.UtcNow.AddHours(1), reasonCodes = new[] { "CHECKED" }
    };

    private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(ComponentDependencyCollector.HttpClientName, name);
            return new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        }
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _body;

        public UnknownLengthContent(byte[] body)
        {
            _body = body;
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(_body).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(_body, writable: false));
    }
}
