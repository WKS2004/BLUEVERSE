using System.Net.Http.Json;
using System.Text.Json;

namespace Blueverse.CoastalPlanner.Integration;

public class PeerServicesClient : IPeerServicesClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PeerServicesClient> _logger;

    private readonly int _timeoutSeconds;
    private readonly int _retryCount;

    public PeerServicesClient(HttpClient http, IConfiguration config, ILogger<PeerServicesClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;

        _timeoutSeconds = _config.GetValue<int>("PeerServices:TimeoutSeconds", 3);
        _retryCount = _config.GetValue<int>("PeerServices:RetryCount", 2);
    }

    public async Task<(List<PeerCatalogueItem> Items, bool Responded, string? Note)> GetCatalogueOfferingsAsync(
        Guid destinationId, 
        List<Guid>? preferredActivityIds, 
        CancellationToken ct = default)
    {
        var baseUrl = _config["PeerServices:ExperienceCatalogueUrl"];
        var endpoint = $"{baseUrl}/api/experiences/catalogue?destinationId={destinationId}";

        var (data, responded, error) = await ExecuteWithRetryAsync<List<PeerCatalogueItem>>(endpoint, ct);
        if (responded && data != null)
        {
            return (data, true, null);
        }

        _logger.LogWarning("Experience Catalogue service unreachable or failed. Falling back to default planner items. Reason: {Reason}", error);
        
        // Fallback default candidate set so planner keeps functioning autonomously
        var fallbackItems = new List<PeerCatalogueItem>
        {
            new PeerCatalogueItem(
                DestinationId: destinationId,
                ActivityId: preferredActivityIds?.FirstOrDefault() ?? Guid.NewGuid(),
                OfferingId: Guid.NewGuid(),
                Title: "Coastal Snorkeling & Reef Discovery",
                AvailabilityStatus: "UNKNOWN",
                PublicationState: "PUBLISHED"
            )
        };

        return (fallbackItems, false, $"Experience catalogue service did not respond after {_retryCount} retries. Used fallback candidates.");
    }

    public async Task<(PeerSuitabilityResponse? Result, bool Responded, string? Note)> GetMarineSuitabilityAsync(
        Guid destinationId, 
        Guid activityId, 
        DateTime windowStart, 
        DateTime windowEnd, 
        CancellationToken ct = default)
    {
        var baseUrl = _config["PeerServices:MarineConditionsUrl"];
        var endpoint = $"{baseUrl}/api/marine/suitability?destinationId={destinationId}&activityId={activityId}&start={windowStart:O}&end={windowEnd:O}";

        var (data, responded, error) = await ExecuteWithRetryAsync<PeerSuitabilityResponse>(endpoint, ct);
        if (responded && data != null)
        {
            return (data, true, null);
        }

        _logger.LogWarning("Marine conditions service unreachable. Defaulting suitability to UNKNOWN. Reason: {Reason}", error);
        return (new PeerSuitabilityResponse(
            DestinationId: destinationId,
            ActivityId: activityId,
            Status: "UNKNOWN",
            ConditionTimestamp: DateTime.UtcNow,
            SafetyProfileId: Guid.NewGuid(),
            Advisory: "Marine conditions service did not respond. Live safety intelligence is currently unknown."
        ), false, "Marine conditions service did not respond. Suitability marked UNKNOWN.");
    }

    public async Task<(PeerOperationStatusResponse? Result, bool Responded, string? Note)> GetOperationalStatusAsync(
        Guid destinationId, 
        CancellationToken ct = default)
    {
        var baseUrl = _config["PeerServices:CoastalOperationsUrl"];
        var endpoint = $"{baseUrl}/api/operations/status?destinationId={destinationId}";

        var (data, responded, error) = await ExecuteWithRetryAsync<PeerOperationStatusResponse>(endpoint, ct);
        if (responded && data != null)
        {
            return (data, true, null);
        }

        _logger.LogWarning("Coastal operations service unreachable. Defaulting status to CAUTION. Reason: {Reason}", error);
        return (new PeerOperationStatusResponse(
            DestinationId: destinationId,
            OperationalStatus: "CAUTION",
            ActiveAlerts: new List<string> { "Coastal operations service not responding. Operational alerts could not be verified." }
        ), false, "Coastal operations service did not respond. Status marked CAUTION.");
    }

    public async Task<(PeerBiodiversityInferenceResponse? Result, bool Responded, string? Note)> GetBiodiversityInferenceAsync(
        Guid destinationId, 
        Guid? activityId, 
        CancellationToken ct = default)
    {
        var baseUrl = _config["PeerServices:BiodiversityMlUrl"];
        var endpoint = $"{baseUrl}/predict?destinationId={destinationId}&activityId={activityId}";

        var (data, responded, error) = await ExecuteWithRetryAsync<PeerBiodiversityInferenceResponse>(endpoint, ct);
        if (responded && data != null)
        {
            return (data, true, null);
        }

        _logger.LogWarning("IT3091 Biodiversity ML inference service unreachable. Reason: {Reason}", error);
        return (null, false, "IT3091 Biodiversity ML model service did not respond. Contextual predictions unavailable.");
    }

    private async Task<(T? Data, bool Success, string? Error)> ExecuteWithRetryAsync<T>(string url, CancellationToken ct) where T : class
    {
        string? lastError = null;

        for (int attempt = 1; attempt <= _retryCount; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

                var response = await _http.GetAsync(url, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cts.Token);
                    if (result != null) return (result, true, null);
                }

                lastError = $"Status {(int)response.StatusCode}";
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastError = $"Timeout after {_timeoutSeconds}s";
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            if (attempt < _retryCount)
            {
                await Task.Delay(200 * attempt, ct);
            }
        }

        return (null, false, lastError);
    }
}
