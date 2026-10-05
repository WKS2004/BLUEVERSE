using System.Net.Http.Json;
using System.Text.Json;

namespace Blueverse.CoastalPlanner.Integration;

public sealed class PeerServicesClient : IPeerServicesClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PeerServicesClient> _logger;
    private readonly TimeSpan _timeout;
    private readonly int _retryCount;

    public PeerServicesClient(HttpClient http, IConfiguration config, ILogger<PeerServicesClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;

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
                    !string.IsNullOrWhiteSpace(item.Title) && item.Title.Length <= 150 &&
                    item.AvailabilityStatus is ("AVAILABLE" or "UNAVAILABLE" or "UNKNOWN") &&
                    item.PublicationState is ("DRAFT" or "PUBLISHED" or "ARCHIVED"))
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
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct = default)
    {
        var query = $"destinationId={Uri.EscapeDataString(destinationId.ToString())}" +
                    $"&activityId={Uri.EscapeDataString(activityId.ToString())}" +
                    $"&start={Uri.EscapeDataString(windowStart.ToUniversalTime().ToString("O"))}" +
                    $"&end={Uri.EscapeDataString(windowEnd.ToUniversalTime().ToString("O"))}";
        var url = BuildEndpoint(_config["PeerServices:MarineConditionsUrl"], "api/marine/suitability", query);
        if (url is null)
        {
            return (null, false, UnavailableNote("Marine Conditions", "endpoint URL is not configured"));
        }

        var (data, responded, note) = await ExecuteWithRetryAsync<PeerSuitabilityResponse>(
            url, "Marine Conditions", ct);
        if (!responded || data is null)
        {
            _logger.LogWarning("Marine Conditions endpoint is unavailable: {Reason}", note);
            return (null, false, note);
        }

        if (data.DestinationId != destinationId || data.ActivityId != activityId ||
            data.ConditionTimestamp == default || data.ConditionTimestamp.Kind != DateTimeKind.Utc ||
            data.Status is not ("SUITABLE" or "CAUTION" or "UNSUITABLE" or "UNKNOWN") ||
            ((data.Status is "SUITABLE" or "CAUTION") && data.SafetyProfileId.GetValueOrDefault() == Guid.Empty))
        {
            return (null, false, "Marine Conditions returned an invalid suitability response; the candidate was omitted.");
        }

        return (data, true, null);
    }

    public async Task<(PeerOperationStatusResponse? Result, bool Responded, string? Note)> GetOperationalStatusAsync(
        Guid destinationId,
        CancellationToken ct = default)
    {
        var query = $"destinationId={Uri.EscapeDataString(destinationId.ToString())}";
        var url = BuildEndpoint(_config["PeerServices:CoastalOperationsUrl"], "api/operations/status", query);
        if (url is null)
        {
            return (null, false, UnavailableNote("Coastal Operations", "endpoint URL is not configured"));
        }

        var (data, responded, note) = await ExecuteWithRetryAsync<PeerOperationStatusResponse>(
            url, "Coastal Operations", ct);
        if (!responded || data is null)
        {
            _logger.LogWarning("Coastal Operations endpoint is unavailable: {Reason}", note);
            return (null, false, note);
        }

        if (data.DestinationId != destinationId ||
            data.OperationalStatus is not ("OPEN" or "CAUTION" or "TEMPORARILY_SUSPENDED" or "CANCELLED" or "COMPLETED"))
        {
            return (null, false, "Coastal Operations returned an invalid status response; the candidate was omitted.");
        }

        return (data, true, null);
    }

    public async Task<(PeerBiodiversityInferenceResponse? Result, bool Responded, string? Note)> GetBiodiversityInferenceAsync(
        Guid destinationId,
        Guid? activityId,
        CancellationToken ct = default)
    {
        var query = $"destinationId={Uri.EscapeDataString(destinationId.ToString())}";
        if (activityId.HasValue)
        {
            query += $"&activityId={Uri.EscapeDataString(activityId.Value.ToString())}";
        }

        var url = BuildEndpoint(_config["PeerServices:BiodiversityMlUrl"], "predict", query);
        if (url is null)
        {
            return (null, false, UnavailableNote("Biodiversity ML", "endpoint URL is not configured"));
        }

        var (data, responded, note) = await ExecuteWithRetryAsync<PeerBiodiversityInferenceResponse>(
            url, "Biodiversity ML", ct);
        if (!responded || data is null)
        {
            _logger.LogWarning("Biodiversity ML endpoint is unavailable: {Reason}", note);
            return (null, false, note);
        }

        if (data.DestinationId != destinationId || data.ActivityId != activityId ||
            data.Status != "AVAILABLE" || string.IsNullOrWhiteSpace(data.ModelVersion) ||
            data.ModelVersion.Length > 64 ||
            data.Timestamp is null || data.Timestamp.Value.Kind != DateTimeKind.Utc ||
            data.Timestamp.Value < DateTime.UtcNow.AddHours(-6) || data.Timestamp.Value > DateTime.UtcNow.AddMinutes(1) ||
            data.Species is null || data.Species.Any(species =>
                species is null || species.SpeciesId == Guid.Empty || string.IsNullOrWhiteSpace(species.ScientificName) ||
                species.ScientificName.Length > 200 || string.IsNullOrWhiteSpace(species.CommonName) ||
                species.CommonName.Length > 200 || !double.IsFinite(species.HabitatSuitability) ||
                species.HabitatSuitability is < 0 or > 1 || string.IsNullOrWhiteSpace(species.ConfidenceLevel) ||
                species.ConfidenceLevel.Length > 64))
        {
            return (null, false, "Biodiversity ML returned an invalid or unavailable inference; no prediction was cached.");
        }

        return (data, true, null);
    }

    private async Task<(T? Data, bool Responded, string? Note)> ExecuteWithRetryAsync<T>(
        string url,
        string dependencyName,
        CancellationToken ct) where T : class
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
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

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

        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}?{query}";
    }

    private string UnavailableNote(string dependencyName, string reason) =>
        $"{dependencyName} endpoint has not responded ({reason}); {_retryCount} retries were configured ({_retryCount + 1} attempts maximum, {_timeout.TotalSeconds:0.#}-second timeout per attempt). Data requiring this dependency is unavailable.";
}
