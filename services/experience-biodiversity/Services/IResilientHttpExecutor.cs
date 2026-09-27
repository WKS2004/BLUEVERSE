using System.Diagnostics;

namespace Blueverse.ExperienceBiodiversity.Services;

public sealed record ResilientExecutionResult(
    bool Responded,
    int? StatusCode,
    string? Content,
    int AttemptsCount,
    long DurationMs,
    string RemoteStatus, // "RESPONDED", "UNREACHABLE", "TIMEOUT", "HTTP_ERROR", "NOT_CONFIGURED"
    string Message,
    Exception? LastException);

public interface IResilientHttpExecutor
{
    Task<ResilientExecutionResult> ExecuteGetAsync(
        string? targetUrl,
        TimeSpan perAttemptTimeout,
        int maxRetries = 2,
        TimeSpan? initialRetryDelay = null,
        CancellationToken cancellationToken = default);
}

public sealed class ResilientHttpExecutor : IResilientHttpExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResilientHttpExecutor> _logger;

    public ResilientHttpExecutor(HttpClient httpClient, ILogger<ResilientHttpExecutor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ResilientExecutionResult> ExecuteGetAsync(
        string? targetUrl,
        TimeSpan perAttemptTimeout,
        int maxRetries = 2,
        TimeSpan? initialRetryDelay = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return new ResilientExecutionResult(
                Responded: false,
                StatusCode: null,
                Content: null,
                AttemptsCount: 0,
                DurationMs: 0,
                RemoteStatus: "NOT_CONFIGURED",
                Message: "Target remote service URL is not configured; running in standalone mode.",
                LastException: null);
        }

        var retryDelay = initialRetryDelay ?? TimeSpan.FromMilliseconds(150);
        var totalAttempts = 1 + Math.Max(0, maxRetries);
        var stopwatch = Stopwatch.StartNew();
        Exception? lastException = null;

        for (int attempt = 1; attempt <= totalAttempts; attempt++)
        {
            if (attempt > 1)
            {
                var backoff = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * Math.Pow(2, attempt - 2));
                _logger.LogInformation(
                    "Retrying call to {TargetUrl} (Attempt {Attempt}/{TotalAttempts}) after {DelayMs}ms backoff.",
                    targetUrl, attempt, totalAttempts, backoff.TotalMilliseconds);
                try
                {
                    await Task.Delay(backoff, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(perAttemptTimeout);

                var response = await _httpClient.GetAsync(targetUrl, timeoutCts.Token);
                var duration = stopwatch.ElapsedMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    return new ResilientExecutionResult(
                        Responded: true,
                        StatusCode: (int)response.StatusCode,
                        Content: content,
                        AttemptsCount: attempt,
                        DurationMs: duration,
                        RemoteStatus: "RESPONDED",
                        Message: $"Remote endpoint {targetUrl} responded with status {(int)response.StatusCode} in {duration}ms.",
                        LastException: null);
                }

                // If response is 5xx server error, we can retry if attempts remain
                if ((int)response.StatusCode >= 500 && attempt < totalAttempts)
                {
                    _logger.LogWarning(
                        "Remote endpoint {TargetUrl} returned {StatusCode} on attempt {Attempt}. Will retry.",
                        targetUrl, response.StatusCode, attempt);
                    continue;
                }

                // If 4xx (client error) or final 5xx attempt:
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ResilientExecutionResult(
                    Responded: true,
                    StatusCode: (int)response.StatusCode,
                    Content: errorBody,
                    AttemptsCount: attempt,
                    DurationMs: duration,
                    RemoteStatus: (int)response.StatusCode >= 500 ? "HTTP_ERROR" : "RESPONDED",
                    Message: $"Remote endpoint {targetUrl} responded with HTTP status {(int)response.StatusCode} ({response.ReasonPhrase}) after {attempt} attempt(s).",
                    LastException: null);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Request timed out for this attempt
                lastException = ex;
                _logger.LogWarning(
                    "Timeout ({TimeoutMs}ms) reaching {TargetUrl} on attempt {Attempt}/{TotalAttempts}.",
                    perAttemptTimeout.TotalMilliseconds, targetUrl, attempt, totalAttempts);

                if (attempt == totalAttempts)
                {
                    return new ResilientExecutionResult(
                        Responded: false,
                        StatusCode: null,
                        Content: null,
                        AttemptsCount: attempt,
                        DurationMs: stopwatch.ElapsedMilliseconds,
                        RemoteStatus: "TIMEOUT",
                        Message: $"Remote endpoint {targetUrl} timed out after {attempt} attempt(s) ({perAttemptTimeout.TotalMilliseconds}ms per attempt). Safe fallback applied.",
                        LastException: ex);
                }
            }
            catch (HttpRequestException ex)
            {
                // Network failure: connection refused, DNS error, host unreachable
                lastException = ex;
                _logger.LogWarning(
                    "HTTP connection error reaching {TargetUrl} on attempt {Attempt}/{TotalAttempts}: {Error}",
                    targetUrl, attempt, totalAttempts, ex.Message);

                if (attempt == totalAttempts)
                {
                    return new ResilientExecutionResult(
                        Responded: false,
                        StatusCode: null,
                        Content: null,
                        AttemptsCount: attempt,
                        DurationMs: stopwatch.ElapsedMilliseconds,
                        RemoteStatus: "UNREACHABLE",
                        Message: $"Remote endpoint {targetUrl} could not be reached (connection refused / network unreachable) after {attempt} attempt(s). Safe fallback applied.",
                        LastException: ex);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(
                    ex, "Unexpected error calling {TargetUrl} on attempt {Attempt}/{TotalAttempts}.",
                    targetUrl, attempt, totalAttempts);

                if (attempt == totalAttempts)
                {
                    return new ResilientExecutionResult(
                        Responded: false,
                        StatusCode: null,
                        Content: null,
                        AttemptsCount: attempt,
                        DurationMs: stopwatch.ElapsedMilliseconds,
                        RemoteStatus: "UNREACHABLE",
                        Message: $"Remote endpoint {targetUrl} failed with error: {ex.Message} after {attempt} attempt(s). Safe fallback applied.",
                        LastException: ex);
                }
            }
        }

        return new ResilientExecutionResult(
            Responded: false,
            StatusCode: null,
            Content: null,
            AttemptsCount: totalAttempts,
            DurationMs: stopwatch.ElapsedMilliseconds,
            RemoteStatus: "UNREACHABLE",
            Message: $"Remote endpoint {targetUrl} was not available after {totalAttempts} attempts. Safe fallback applied.",
            LastException: lastException);
    }
}
