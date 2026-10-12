using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Blueverse.CoastalOperations.Contracts;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Application;

public interface IComponentDependencyCollector
{
    Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(
        string targetType,
        Guid targetId,
        DateTimeOffset periodStartsAt,
        DateTimeOffset periodEndsAt,
        Guid? sourceWorkflowId,
        CancellationToken cancellationToken);
}

public sealed class ComponentDependencyCollector(
    IHttpClientFactory httpClientFactory,
    IOptions<ComponentDependencyOptions> options,
    ComponentDependencyHealthRegistry healthRegistry,
    ILogger<ComponentDependencyCollector> logger) : IComponentDependencyCollector
{
    public const string HttpClientName = "CoastalComponentDependencies";
    private readonly ComponentDependencyOptions _options = options.Value;

    public async Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(
        string targetType,
        Guid targetId,
        DateTimeOffset periodStartsAt,
        DateTimeOffset periodEndsAt,
        Guid? sourceWorkflowId,
        CancellationToken cancellationToken)
    {
        var member1Task = GetExperienceAvailabilityAsync(targetType, targetId, periodStartsAt, periodEndsAt, cancellationToken);
        var member2Task = Task.FromResult(Record(NotConnected("member-2-marine-safety", "marine-suitability")));
        var member3Task = sourceWorkflowId is Guid workflowId
            ? Task.FromResult(Record(NotConnected("member-3-coastal-planner", $"planner-workflow:{workflowId:D}")))
            : Task.FromResult(Record(NotRequested("member-3-coastal-planner", "planner-workflow")));

        return await Task.WhenAll(member1Task, member2Task, member3Task);
    }

    private async Task<ComponentDependencyResult> GetExperienceAvailabilityAsync(
        string targetType,
        Guid targetId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken)
    {
        if (targetType == "SESSION")
        {
            return Record(new ComponentDependencyResult(
                "member-1-experience", "experience-availability", "NOT_REQUESTED", 0, 0, false, null,
                "SESSION targets have no canonical Experience catalogue record.", DateTimeOffset.UtcNow, null));
        }

        var endpoint = _options.Member1Experience;
        var path = endpoint.AvailabilityPath?
            .Replace("{targetType}", Uri.EscapeDataString(targetType), StringComparison.Ordinal)
            .Replace("{targetId}", Uri.EscapeDataString(targetId.ToString("D")), StringComparison.Ordinal);
        if (!TryBuildUri(endpoint.BaseAddress, path, out var uri))
            return Record(Misconfigured("member-1-experience", "experience-availability"));

        var builder = new UriBuilder(uri!);
        builder.Query = QueryHelpers.AddQueryString(string.Empty, new Dictionary<string, string?>
        {
            ["periodStartsAt"] = startsAt.ToString("O", CultureInfo.InvariantCulture),
            ["periodEndsAt"] = endsAt.ToString("O", CultureInfo.InvariantCulture)
        }).TrimStart('?');

        return await SendAsync<ExperienceAvailabilityResponse>(
            "member-1-experience",
            "experience-availability",
            builder.Uri,
            () => new HttpRequestMessage(HttpMethod.Get, builder.Uri),
            response => response.TargetId == targetId &&
                        string.Equals(response.TargetType, targetType, StringComparison.Ordinal) &&
                        IsOneOf(response.AvailabilityStatus, "AVAILABLE", "UNAVAILABLE") &&
                        IsOptionalText(response.TargetVersion, 128) &&
                        IsValidEvaluation(response.EvaluatedAt, response.ValidUntil)
                ? new ComponentDependencyEvidence(
                    AvailabilityStatus: response.AvailabilityStatus,
                    SuitabilityClassification: null,
                    WorkflowStatus: null,
                    SourceVersion: response.TargetVersion,
                    ProfileVersion: null,
                    ObservedAt: response.EvaluatedAt,
                    ValidUntil: response.ValidUntil,
                    ReasonCodes: [])
                : null,
            cancellationToken);
    }

    private async Task<ComponentDependencyResult> SendAsync<TResponse>(
        string service,
        string endpoint,
        Uri uri,
        Func<HttpRequestMessage> createRequest,
        Func<TResponse, ComponentDependencyEvidence?> normalize,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var maxRetries = Math.Clamp(_options.MaxRetries, 0, 2);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 2));
        var retryDelay = Math.Clamp(_options.RetryDelayMilliseconds, 0, 1000);
        var maxBytes = Math.Clamp(_options.MaxResponseBytes, 1024, 32 * 1024);
        using var client = httpClientFactory.CreateClient(HttpClientName);
        var lastError = "SERVICE_UNAVAILABLE";
        var checkedAt = DateTimeOffset.UtcNow;
        var attempts = 0;

        for (var attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            attempts = attempt;
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(timeout);
            try
            {
                using var request = createRequest();
                using var response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        requestTimeout.Token)
                    .WaitAsync(timeout, cancellationToken);
                checkedAt = DateTimeOffset.UtcNow;

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return Record(new ComponentDependencyResult(
                        service, endpoint, "ENDPOINT_NOT_FOUND", attempt, attempt - 1, false,
                        "HTTP_404", "The expected endpoint returned 404; its route or the requested record is not available.", checkedAt, null));
                }

                if (ShouldRetry(response.StatusCode))
                {
                    lastError = $"HTTP_{(int)response.StatusCode}";
                    if (attempt <= maxRetries)
                    {
                        await DelayBeforeRetryAsync(attempt, retryDelay, cancellationToken);
                        continue;
                    }

                    return Record(Unavailable(service, endpoint, attempts, lastError, checkedAt));
                }

                if (!response.IsSuccessStatusCode)
                {
                    var status = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        ? "REJECTED"
                        : "UNAVAILABLE";
                    return Record(new ComponentDependencyResult(
                        service, endpoint, status, attempt, attempt - 1, false,
                        $"HTTP_{(int)response.StatusCode}",
                        $"The endpoint responded with HTTP {(int)response.StatusCode}.", checkedAt, null));
                }

                if (response.Content.Headers.ContentLength > maxBytes)
                {
                    return Record(InvalidResponse(service, endpoint, attempt, "RESPONSE_TOO_LARGE", checkedAt));
                }

                if (!IsJsonContentType(response.Content.Headers.ContentType))
                {
                    return Record(InvalidResponse(service, endpoint, attempt, "INVALID_CONTENT_TYPE", checkedAt));
                }

                var responseBytes = await ReadBoundedAsync(response.Content, maxBytes, requestTimeout.Token);
                var wireResponse = JsonSerializer.Deserialize<TResponse>(responseBytes, OperationsValidation.JsonOptions);
                var evidence = wireResponse is null ? null : normalize(wireResponse);
                if (evidence is null)
                    return Record(InvalidResponse(service, endpoint, attempt, "INVALID_SCHEMA", checkedAt));

                if (evidence.ValidUntil is { } validUntil && validUntil <= DateTimeOffset.UtcNow)
                {
                    return Record(new ComponentDependencyResult(
                        service, endpoint, "STALE", attempt, attempt - 1, false,
                        "EVIDENCE_EXPIRED", "The endpoint responded with validated evidence whose validity period has ended.",
                        checkedAt, evidence));
                }

                return Record(new ComponentDependencyResult(
                    service, endpoint, "RESPONDED", attempt, attempt - 1, false,
                    null, "The endpoint responded with validated source data.", checkedAt, evidence));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastError = "TIMEOUT";
                checkedAt = DateTimeOffset.UtcNow;
                if (attempt <= maxRetries)
                {
                    await DelayBeforeRetryAsync(attempt, retryDelay, cancellationToken);
                    continue;
                }
            }
            catch (TimeoutException)
            {
                lastError = "TIMEOUT";
                checkedAt = DateTimeOffset.UtcNow;
                if (attempt <= maxRetries)
                {
                    await DelayBeforeRetryAsync(attempt, retryDelay, cancellationToken);
                    continue;
                }
            }
            catch (HttpRequestException)
            {
                lastError = "SERVICE_UNREACHABLE";
                checkedAt = DateTimeOffset.UtcNow;
                if (attempt <= maxRetries)
                {
                    await DelayBeforeRetryAsync(attempt, retryDelay, cancellationToken);
                    continue;
                }
            }
            catch (InvalidDataException)
            {
                return Record(InvalidResponse(service, endpoint, attempt, "RESPONSE_TOO_LARGE", DateTimeOffset.UtcNow));
            }
            catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
            {
                return Record(InvalidResponse(service, endpoint, attempt, "INVALID_RESPONSE", DateTimeOffset.UtcNow));
            }
            catch (IOException)
            {
                lastError = "SERVICE_UNREACHABLE";
                checkedAt = DateTimeOffset.UtcNow;
                if (attempt <= maxRetries)
                {
                    await DelayBeforeRetryAsync(attempt, retryDelay, cancellationToken);
                    continue;
                }
            }
        }

        var unavailable = Unavailable(service, endpoint, attempts, lastError, checkedAt);
        logger.LogWarning(
            "Optional component dependency {Dependency} did not respond after {Attempts} attempts ({RetryCount} retries). Error code: {ErrorCode}.",
            service,
            unavailable.Attempts,
            unavailable.Retries,
            unavailable.ErrorCode);
        return Record(unavailable);
    }

    private async Task DelayBeforeRetryAsync(int attempt, int baseDelayMilliseconds, CancellationToken cancellationToken)
    {
        if (baseDelayMilliseconds == 0) return;
        var multiplier = Math.Pow(2, Math.Min(attempt - 1, 4));
        var delay = TimeSpan.FromMilliseconds(Math.Min(baseDelayMilliseconds * multiplier, 2000));
        await Task.Delay(delay, cancellationToken);
    }

    private ComponentDependencyResult Record(ComponentDependencyResult result)
    {
        healthRegistry.Record(result);
        return result;
    }

    private static ComponentDependencyResult NotRequested(string service, string endpoint) => new(
        service, endpoint, "NOT_REQUESTED", 0, 0, false, null,
        "No planner workflow was supplied with this assessment.", DateTimeOffset.UtcNow, null);

    private static ComponentDependencyResult NotConnected(string service, string endpoint) => new(
        service, endpoint, "NOT_CONNECTED", 0, 0, false, "INTEGRATION_CONTRACT_PENDING",
        "The peer route, payload and authorized service identity are not agreed; no request was sent.",
        DateTimeOffset.UtcNow, null);

    private static ComponentDependencyResult Misconfigured(string service, string endpoint) => new(
        service, endpoint, "MISCONFIGURED", 0, 0, false, "ENDPOINT_CONFIGURATION_INVALID",
        "The optional dependency endpoint is not configured with a valid HTTP or HTTPS address and relative path.",
        DateTimeOffset.UtcNow, null);

    private static ComponentDependencyResult Unavailable(
        string service,
        string endpoint,
        int attempts,
        string errorCode,
        DateTimeOffset checkedAt) => new(
        service, endpoint, "UNAVAILABLE", attempts, attempts - 1, true, errorCode,
        $"The endpoint did not respond after {attempts} attempts ({attempts - 1} retries).",
        checkedAt, null);

    private static ComponentDependencyResult InvalidResponse(
        string service,
        string endpoint,
        int attempts,
        string errorCode,
        DateTimeOffset checkedAt) => new(
        service, endpoint, "INVALID_RESPONSE", attempts, attempts - 1, false, errorCode,
        "The endpoint responded, but its payload did not match the expected bounded contract.",
        checkedAt, null);

    private static bool TryBuildUri(string? baseAddress, string? path, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment) ||
            string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith("/api/", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\\') ||
            path.Contains('#') ||
            path.Contains('?'))
        {
            return false;
        }

        try
        {
            uri = new Uri(baseUri, path);
            return uri.Scheme == baseUri.Scheme && uri.Host == baseUri.Host;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool IsJsonContentType(MediaTypeHeaderValue? contentType) =>
        contentType?.MediaType is { } mediaType &&
        (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
         mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static async Task<string> ReadBoundedAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        await using var target = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var count = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) break;
            if (target.Length + count > maxBytes) throw new InvalidDataException("The dependency response exceeded the configured byte limit.");
            await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(target.ToArray());
    }

    private static bool IsValidEvaluation(DateTimeOffset evaluatedAt, DateTimeOffset? validUntil) =>
        evaluatedAt != default && (validUntil is null || validUntil > evaluatedAt);

    private static bool IsOptionalText(string? value, int maxLength) =>
        value is null || (value.Length <= maxLength && value.All(character => !char.IsControl(character)));

    private static bool IsOneOf(string? value, params string[] allowed) =>
        allowed.Contains(value, StringComparer.Ordinal);

}
