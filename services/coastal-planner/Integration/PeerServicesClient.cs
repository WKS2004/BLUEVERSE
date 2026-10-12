using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;

namespace Blueverse.CoastalPlanner.Integration;

public sealed class PeerServicesClient : IPeerServicesClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PeerServicesClient> _logger;
    private readonly BiodiversityCircuitBreaker _biodiversityCircuit;
    private readonly TimeSpan _timeout;
    private readonly int _retryCount;

    public PeerServicesClient(HttpClient http, IConfiguration config, ILogger<PeerServicesClient> logger,
        BiodiversityCircuitBreaker? biodiversityCircuit = null)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _biodiversityCircuit = biodiversityCircuit ?? new BiodiversityCircuitBreaker(config, TimeProvider.System);

        var timeoutSeconds = config.GetValue<int?>("PeerServices:TimeoutSeconds") ?? 3;
        if (timeoutSeconds is < 1 or > 30)
        {
            throw new InvalidOperationException("PeerServices:TimeoutSeconds must be between 1 and 30.");
        }

        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _retryCount = config.GetValue<int?>("PeerServices:RetryCount") ?? 1;
        if (_retryCount is < 0 or > 5)
        {
            throw new InvalidOperationException("PeerServices:RetryCount must be between 0 and 5.");
        }
    }

    public async Task<(List<PeerCatalogueItem> Items, bool Responded, string? Note)> GetCatalogueOfferingsAsync(
        Guid destinationId,
        List<Guid>? preferredActivityIds,
        CancellationToken ct = default)
    {
        var query = $"destinationId={Uri.EscapeDataString(destinationId.ToString())}";
        if (preferredActivityIds is { Count: > 0 })
        {
            query += "&activityIds=" + Uri.EscapeDataString(string.Join(",", preferredActivityIds));
        }

        var url = BuildEndpoint(_config["PeerServices:ExperienceCatalogueUrl"], "api/experiences/catalogue", query);
        if (url is null)
        {
            return ([], false, UnavailableNote("Experience Catalogue", "endpoint URL is not configured"));
        }

        var (data, responded, note) = await ExecuteWithRetryAsync<List<PeerCatalogueItem>>(
            url, "Experience Catalogue", ct);

        if (responded && data is not null)
        {
            var matchingItems = data
                .Where(item => item is not null && item.DestinationId == destinationId &&
                    item.ActivityId != Guid.Empty && item.OfferingId is { } offeringId && offeringId != Guid.Empty &&
                    !string.IsNullOrWhiteSpace(item.Title) && item.Title.Length <= 200 &&
                    !string.IsNullOrWhiteSpace(item.ActivityName) && item.ActivityName.Length <= 100 &&
                    item.AvailabilityStatus is ("AVAILABLE" or "UNAVAILABLE") &&
                    item.PublicationState == "PUBLISHED" &&
                    item.Latitude is { } latitude && double.IsFinite(latitude) && latitude is >= -90 and <= 90 &&
                    item.Longitude is { } longitude && double.IsFinite(longitude) && longitude is >= -180 and <= 180 &&
                    (item.ExperienceLevels is null || (item.ExperienceLevels.Count <= 3 &&
                        item.ExperienceLevels.All(level => level is "BEGINNER" or "INTERMEDIATE" or "ADVANCED") &&
                        item.ExperienceLevels.Distinct(StringComparer.Ordinal).Count() == item.ExperienceLevels.Count)))
                .ToList();

            if (matchingItems.Count != data.Count)
            {
                _logger.LogWarning("Experience Catalogue returned {InvalidCount} malformed or mismatched offerings.",
                    data.Count - matchingItems.Count);
            }

            return (matchingItems, true, matchingItems.Count == data.Count
                ? null
                : "Experience Catalogue returned invalid or mismatched offerings; those records were omitted.");
        }

        _logger.LogWarning("Experience Catalogue endpoint is unavailable: {Reason}", note);
        return ([], false, note);
    }

    public async Task<(PeerSuitabilityResponse? Result, bool Responded, string? Note)> GetMarineSuitabilityAsync(
        Guid destinationId,
        Guid activityId,
        double? latitude,
        double? longitude,
        DateTime requestedTimeUtc,
        CancellationToken ct = default)
    {
        var url = BuildEndpoint(_config["PeerServices:MarineConditionsUrl"], "api/marine/evaluate", string.Empty);
        if (url is null)
        {
            return (null, false, UnavailableNote("Marine Conditions", "endpoint URL is not configured"));
        }
        if (destinationId == Guid.Empty || activityId == Guid.Empty || latitude is null || longitude is null ||
            !double.IsFinite(latitude.Value) || latitude is < -90 or > 90 ||
            !double.IsFinite(longitude.Value) || longitude is < -180 or > 180 || requestedTimeUtc == default)
        {
            return (null, false, "Marine suitability could not be checked because the catalogue location or requested time is invalid.");
        }

        var requestedTime = requestedTimeUtc.ToUniversalTime();
        var requestContent = new MarineEvaluationRequest(activityId, (decimal)latitude.Value, (decimal)longitude.Value, requestedTime);
        var (wire, responded, note) = await ExecuteWithRetryAsync<MarineEvaluationResponse>(
            url, "Marine Conditions", ct, () => new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestContent)
            });

        if (!responded || wire is null)
        {
            _logger.LogWarning("Marine Conditions endpoint is unavailable: {Reason}", note);
            return (null, false, note);
        }

        if (wire.ActivityId != activityId || wire.RequestedTime == default || wire.RequestedTime.Kind != DateTimeKind.Utc ||
            wire.RequestedTime != requestedTime || wire.ForecastTime == default || wire.ForecastTime.Kind != DateTimeKind.Utc ||
            wire.Status is not ("SUITABLE" or "CAUTION" or "UNSUITABLE" or "UNKNOWN") ||
            wire.Freshness is not ("FRESH" or "STALE" or "UNAVAILABLE") ||
            wire.AssessmentId == Guid.Empty || wire.SnapshotId == Guid.Empty ||
            wire.EvaluatedAt == default || wire.EvaluatedAt.Kind != DateTimeKind.Utc)
        {
            return (null, false, "Marine Conditions returned an invalid suitability response; the candidate was omitted.");
        }

        var advisory = string.Join("; ", (wire.Violations ?? []).Concat(wire.CautionFactors ?? []));
        return (new PeerSuitabilityResponse(
            DestinationId: destinationId,
            ActivityId: wire.ActivityId,
            Status: wire.Status,
            ConditionTimestamp: wire.ForecastTime,
            SafetyProfileId: null,
            Advisory: string.IsNullOrWhiteSpace(advisory) ? null : advisory,
            IsFresh: wire.Freshness == "FRESH",
            AssessmentId: wire.AssessmentId,
            SnapshotId: wire.SnapshotId,
            EvaluatedAt: wire.EvaluatedAt,
            RetrievedAt: wire.RetrievedAt,
            Source: wire.Source,
            Freshness: wire.Freshness,
            MissingFields: wire.MissingFields,
            Violations: wire.Violations,
            CautionFactors: wire.CautionFactors), true, null);
    }

    public async Task<(PeerOperationStatusResponse? Result, bool Responded, string? Note)> GetOperationalStatusAsync(
        Guid destinationId,
        CancellationToken ct = default)
    {
        var url = BuildEndpoint(_config["PeerServices:CoastalOperationsUrl"],
            $"api/operations/targets/DESTINATION/{destinationId:D}/status", string.Empty);
        if (url is null)
        {
            return (null, false, UnavailableNote("Coastal Operations", "endpoint URL is not configured"));
        }

        var (wire, responded, note) = await ExecuteWithRetryAsync<OperationsTargetStatusResponse>(
            url, "Coastal Operations", ct);
        if (!responded || wire is null)
        {
            _logger.LogWarning("Coastal Operations endpoint is unavailable: {Reason}", note);
            return (null, false, note);
        }

        if (wire.TargetId != destinationId || wire.TargetType != "DESTINATION" ||
            wire.OperationalState is not ("OPEN" or "CAUTION" or "TEMPORARILY_SUSPENDED" or "CANCELLED" or "COMPLETED") ||
            wire.StateVersion <= 0 || wire.UpdatedAt == default || wire.UpdatedAt.Kind != DateTimeKind.Utc)
        {
            return (null, false, "Coastal Operations returned an invalid status response; the candidate was omitted.");
        }

        return (new PeerOperationStatusResponse(destinationId, wire.OperationalState, null,
            wire.TargetType, wire.TargetId, wire.StateVersion, wire.UpdatedAt), true, null);
    }

    public async Task<(PeerBiodiversityInferenceResponse? Result, bool Responded, string? Note)> GetBiodiversityInferenceAsync(
        Guid destinationId,
        Guid? activityId,
        CancellationToken ct = default)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var enabled = _config.GetValue<bool?>("PeerServices:BiodiversityEnabled") ?? true;
        if (!enabled)
        {
            return Outcome("not_requested", destinationId, activityId,
                "Wildlife context was not requested.", correlationId);
        }

        if (destinationId == Guid.Empty || activityId == Guid.Empty)
        {
            return Outcome("invalid", destinationId, activityId,
                "Wildlife context could not be checked for this location.", correlationId);
        }

        if (!_biodiversityCircuit.TryEnter(out var retryAfter))
        {
            _logger.LogInformation("Biodiversity inference outcome {Outcome}; correlation {CorrelationId}; retry after {RetryAfterMs} ms",
                "unavailable", correlationId, (long)retryAfter.TotalMilliseconds);
            return (null, false, "Biodiversity predictions are temporarily unavailable. Please try again later.");
        }

        var url = BuildEndpoint(_config["PeerServices:BiodiversityMlUrl"], "v1/predictions", string.Empty);
        if (url is null)
        {
            _biodiversityCircuit.RecordFailure();
            return Outcome("unavailable", destinationId, activityId,
                "Wildlife predictions are temporarily unavailable.", correlationId, responded: false);
        }

        var request = new BiodiversityInferenceRequest(1, destinationId, activityId, DateTime.UtcNow);
        var maxAttempts = 2;
        string? failureCategory = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(request)
                };
                requestMessage.Options.Set(PeerRequestContextHandler.DoNotForwardUserAuthorization, true);
                requestMessage.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
                var apiKey = _config["PeerServices:BiodiversityApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                using var response = await _http.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((int)response.StatusCode is 400 or 404 or 422)
                {
                    _biodiversityCircuit.RecordSuccess();
                    return Outcome("invalid", destinationId, activityId,
                        "Wildlife predictions are not available for this location.", correlationId);
                }

                if ((int)response.StatusCode is 401 or 403 or 429)
                {
                    _biodiversityCircuit.RecordFailure();
                    return Outcome("unavailable", destinationId, activityId,
                        "Wildlife predictions are temporarily unavailable.", correlationId, responded: false);
                }

                if (!response.IsSuccessStatusCode)
                {
                    failureCategory = $"http_{(int)response.StatusCode}";
                    if ((int)response.StatusCode < 500 || attempt == maxAttempts) break;
                    await Task.Delay(TimeSpan.FromMilliseconds(150), ct);
                    continue;
                }

                PeerBiodiversityInferenceResponse? data;
                try
                {
                    data = await response.Content.ReadFromJsonAsync<PeerBiodiversityInferenceResponse>(cancellationToken: timeout.Token);
                }
                catch (JsonException)
                {
                    _biodiversityCircuit.RecordFailure();
                    return Outcome("invalid", destinationId, activityId,
                        "The wildlife prediction response could not be verified.", correlationId);
                }

                if (data is null)
                {
                    _biodiversityCircuit.RecordFailure();
                    return Outcome("invalid", destinationId, activityId,
                        "The wildlife prediction response could not be verified.", correlationId);
                }

                var validation = ValidateBiodiversityResponse(data, destinationId, activityId, DateTime.UtcNow);
                if (validation is not null)
                {
                    if (validation.Value.Status == "unavailable") _biodiversityCircuit.RecordSuccess();
                    else _biodiversityCircuit.RecordFailure();
                    var (status, message) = validation.Value;
                    var invalidResult = status == "stale" ? data with { Status = status, Species = [] } : null;
                    LogBiodiversityOutcome(status, correlationId);
                    return invalidResult is null
                        ? Outcome(status, destinationId, activityId, message, correlationId)
                        : (invalidResult, true, message);
                }

                if (data.Status == "not_requested")
                {
                    _biodiversityCircuit.RecordSuccess();
                    return Outcome("not_requested", destinationId, activityId,
                        "Wildlife context was not requested.", correlationId);
                }

                _biodiversityCircuit.RecordSuccess();
                LogBiodiversityOutcome("available", correlationId);
                return (data, true, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                failureCategory = "timeout";
            }
            catch (HttpRequestException)
            {
                failureCategory = "connection_failure";
            }

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(150), ct);
        }

        _biodiversityCircuit.RecordFailure();
        _logger.LogInformation("Biodiversity inference outcome {Outcome}; failure category {FailureCategory}; correlation {CorrelationId}",
            "unavailable", failureCategory ?? "unknown", correlationId);
        return (null, false, "Wildlife predictions are temporarily unavailable. Please try again later.");
    }

    private (PeerBiodiversityInferenceResponse? Result, bool Responded, string? Note) Outcome(
        string status, Guid destinationId, Guid? activityId, string message, string correlationId, bool responded = true)
    {
        LogBiodiversityOutcome(status, correlationId);
        return (new PeerBiodiversityInferenceResponse(destinationId, activityId, status, [], null, null, message), responded, message);
    }

    private (string Status, string Message)? ValidateBiodiversityResponse(
        PeerBiodiversityInferenceResponse data, Guid destinationId, Guid? activityId, DateTime now)
    {
        if (data.ContractVersion != 1 || data.DestinationId != destinationId || data.ActivityId != activityId)
            return ("invalid", "The wildlife prediction response could not be verified.");

        if (data.Status == "not_requested")
            return null;
        if (data.Status == "unavailable")
            return ("unavailable", "Wildlife predictions are temporarily unavailable.");
        if (data.Status != "available")
            return ("invalid", "The wildlife prediction response could not be verified.");

        if (data.Timestamp is null || data.Timestamp.Value.Kind != DateTimeKind.Utc)
            return ("invalid", "The wildlife prediction response could not be verified.");
        if (data.Timestamp.Value < now.AddHours(-6) || data.Timestamp.Value > now.AddMinutes(1))
            return ("stale", "The wildlife prediction is no longer current. Request a fresh result.");
        if (string.IsNullOrWhiteSpace(data.Provider) || data.Provider.Length > 100 ||
            string.IsNullOrWhiteSpace(data.ModelVersion) || data.ModelVersion.Length > 64 ||
            string.IsNullOrWhiteSpace(data.Limitations) || data.Limitations.Length > 4000 ||
            (data.UncertaintyText?.Length ?? 0) > 1000 ||
            data.Species is null || data.Species.Count > 100 || data.Species.Any(species =>
                species is null || species.SpeciesId == Guid.Empty || string.IsNullOrWhiteSpace(species.ScientificName) ||
                species.ScientificName.Length > 200 || string.IsNullOrWhiteSpace(species.CommonName) ||
                species.CommonName.Length > 200 || !double.IsFinite(species.HabitatSuitability) ||
                species.HabitatSuitability is < 0 or > 1 || string.IsNullOrWhiteSpace(species.ConfidenceLevel) ||
                species.ConfidenceLevel.Length > 64 || (species.UncertaintyText?.Length ?? 0) > 1000))
        {
            return ("invalid", "The wildlife prediction response could not be verified.");
        }

        var expectedVersion = _config["PeerServices:BiodiversityExpectedModelVersion"];
        if (!string.IsNullOrWhiteSpace(expectedVersion) && !string.Equals(expectedVersion, data.ModelVersion, StringComparison.Ordinal))
            return ("invalid", "The wildlife prediction uses an unsupported model version.");
        return null;
    }

    private void LogBiodiversityOutcome(string outcome, string correlationId) =>
        _logger.LogInformation("Biodiversity inference outcome {Outcome}; correlation {CorrelationId}", outcome, correlationId);

    private async Task<(T? Data, bool Responded, string? Note)> ExecuteWithRetryAsync<T>(
        string url,
        string dependencyName,
        CancellationToken ct,
        Func<HttpRequestMessage>? createRequest = null) where T : class
    {
        string? lastFailure = null;
        var maxAttempts = _retryCount + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(_timeout);
                using var request = createRequest?.Invoke() ?? new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<T>(cancellationToken: timeout.Token);
                    if (data is not null)
                    {
                        return (data, true, null);
                    }

                    lastFailure = "the endpoint returned an empty response";
                }
                else
                {
                    lastFailure = $"the endpoint returned HTTP {(int)response.StatusCode}";
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastFailure = $"the request timed out after {_timeout.TotalSeconds:0.#} seconds";
            }
            catch (HttpRequestException)
            {
                lastFailure = "the endpoint could not be reached";
            }
            catch (JsonException)
            {
                lastFailure = "the endpoint returned an invalid response";
            }
            catch (NotSupportedException)
            {
                lastFailure = "the endpoint returned an unsupported response format";
            }
            catch (UriFormatException)
            {
                lastFailure = "the configured endpoint URL is invalid";
            }

            if (attempt < maxAttempts)
            {
                await DelayBeforeRetryAsync(TimeSpan.FromMilliseconds(Math.Min(200 * (1 << (attempt - 1)), 1000)), ct);
            }
        }

        var note = UnavailableNote(dependencyName, lastFailure ?? "the endpoint did not respond");
        _logger.LogWarning("{DependencyName} endpoint did not respond after {RetryCount} retries ({AttemptCount} attempts): {Failure}",
            dependencyName, _retryCount, maxAttempts, lastFailure);
        return (null, false, note);
    }

    private static async Task DelayBeforeRetryAsync(TimeSpan delay, CancellationToken ct)
    {
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct);
        }
    }

    private static string? BuildEndpoint(string? baseUrl, string path, string query)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            return null;
        }

        var endpoint = $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        return string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}?{query}";
    }

    private string UnavailableNote(string dependencyName, string reason) =>
        $"{dependencyName} endpoint has not responded ({reason}); {_retryCount} retries were configured ({_retryCount + 1} attempts maximum, {_timeout.TotalSeconds:0.#}-second timeout per attempt). Data requiring this dependency is unavailable.";

    private sealed record MarineEvaluationRequest(Guid ActivityId, decimal Latitude, decimal Longitude, DateTime DateTime);

    private sealed record MarineEvaluationResponse(
        string Status,
        Guid ActivityId,
        DateTime RequestedTime,
        DateTime ForecastTime,
        DateTime EvaluatedAt,
        object? Conditions,
        string? Source,
        DateTime? RetrievedAt,
        string? Freshness,
        string[]? MissingFields,
        string[]? Violations,
        string[]? CautionFactors,
        Guid AssessmentId,
        Guid SnapshotId);

    private sealed record OperationsTargetStatusResponse(
        string TargetType,
        Guid TargetId,
        string OperationalState,
        int StateVersion,
        DateTime UpdatedAt);
}
