using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;

namespace Blueverse.ExperienceBiodiversity.Services;

public interface IBiodiversityConsumerService
{
    Task<BiodiversityContextResponseDto> GetBiodiversityContextAsync(
        Guid destinationId,
        string destinationName,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}

public sealed class BiodiversityConsumerService : IBiodiversityConsumerService
{
    private readonly IResilientHttpExecutor _resilientExecutor;
    private readonly ILogger<BiodiversityConsumerService> _logger;
    private readonly string _plannerServiceUrl;

    public BiodiversityConsumerService(
        IResilientHttpExecutor resilientExecutor,
        IConfiguration configuration,
        ILogger<BiodiversityConsumerService> logger)
    {
        _resilientExecutor = resilientExecutor;
        _logger = logger;
        _plannerServiceUrl = configuration["PlannerServiceUrl"]
            ?? configuration["PLANNER_SERVICE_URL"]
            ?? configuration["Services:PlannerServiceUrl"]
            ?? "http://coastal-planner:8080";
    }

    public async Task<BiodiversityContextResponseDto> GetBiodiversityContextAsync(
        Guid destinationId,
        string destinationName,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var targetUrl = $"{_plannerServiceUrl.TrimEnd('/')}/api/planner/biodiversity?destinationId={destinationId}&latitude={latitude}&longitude={longitude}";

        // Execute resiliently with 2-second timeout and 2 retries (3 attempts total)
        var execution = await _resilientExecutor.ExecuteGetAsync(
            targetUrl,
            perAttemptTimeout: TimeSpan.FromSeconds(2),
            maxRetries: 2,
            initialRetryDelay: TimeSpan.FromMilliseconds(100),
            cancellationToken: cancellationToken);

        if (execution.Responded && execution.StatusCode == 200 && !string.IsNullOrWhiteSpace(execution.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(execution.Content);
                var root = doc.RootElement;

                var predictions = new List<FocalSpeciesPredictionDto>();
                if (root.TryGetProperty("predictions", out var predsArray) && predsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in predsArray.EnumerateArray())
                    {
                        var speciesName = item.TryGetProperty("speciesName", out var sn) ? sn.GetString() ?? "Unknown Species" : "Unknown Species";
                        var sciName = item.TryGetProperty("scientificName", out var scn) ? scn.GetString() ?? "" : "";
                        var status = item.TryGetProperty("conservationStatus", out var cs) ? cs.GetString() ?? "Data Deficient" : "Data Deficient";
                        var prob = item.TryGetProperty("occurrenceProbability", out var op) ? op.GetDouble() : 0.5;
                        var habitat = item.TryGetProperty("habitatSuitability", out var hProp) ? hProp.GetString() : null;
                        var threats = item.TryGetProperty("primaryThreats", out var tProp) ? tProp.GetString() : null;

                        predictions.Add(new FocalSpeciesPredictionDto(speciesName, sciName, status, prob, habitat, threats));
                    }
                }

                var modelVersion = root.TryGetProperty("modelVersion", out var mvProp) ? mvProp.GetString() : "v1.0";
                var evalAt = root.TryGetProperty("evaluatedAt", out var eaProp) && eaProp.TryGetDateTimeOffset(out var dto) ? dto : DateTimeOffset.UtcNow;

                return new BiodiversityContextResponseDto(
                    DestinationId: destinationId,
                    DestinationName: destinationName,
                    Latitude: latitude,
                    Longitude: longitude,
                    Status: "available",
                    Predictions: predictions,
                    ModelVersion: modelVersion,
                    ModelSource: "IT3091 ML Workstream via Planner Service",
                    UncertaintyNotes: "Predictions are statistical occurrence probabilities derived from historical coastal observation data.",
                    EvaluatedAt: evalAt,
                    Disclaimer: "Biodiversity intelligence provides informative ecological context, not confirmed species presence or deterministic safety authority.",
                    Responded: true,
                    TargetEndpoint: targetUrl,
                    AttemptsCount: execution.AttemptsCount,
                    LatencyMs: execution.DurationMs,
                    RemoteStatus: "RESPONDED",
                    Message: execution.Message);
            }
            catch (JsonException jEx)
            {
                _logger.LogWarning(jEx, "Failed to parse JSON response from Member 3 Planner service at {TargetUrl}", targetUrl);
            }
        }

        // Resilient safe degradation (ADR-0019)
        var uncertaintyNote = execution.Responded
            ? $"Remote inference endpoint returned HTTP status {execution.StatusCode}. Explicit unavailable status preserved. Biodiversity predictions are optional context and operational discovery continues normally."
            : $"The remote biodiversity ML microservice did not respond ({execution.RemoteStatus}). Safe degradation active: zero predictions; biodiversity predictions are optional context and operational discovery continues normally.";

        return new BiodiversityContextResponseDto(
            DestinationId: destinationId,
            DestinationName: destinationName,
            Latitude: latitude,
            Longitude: longitude,
            Status: "unavailable",
            Predictions: Array.Empty<FocalSpeciesPredictionDto>(),
            ModelVersion: null,
            ModelSource: "IT3091 ML Workstream (Adithya Gunawardana adapter)",
            UncertaintyNotes: uncertaintyNote,
            EvaluatedAt: null,
            Disclaimer: "Biodiversity intelligence provides informative ecological context, not confirmed species presence or deterministic safety authority.",
            Responded: execution.Responded,
            TargetEndpoint: targetUrl,
            AttemptsCount: execution.AttemptsCount,
            LatencyMs: execution.DurationMs,
            RemoteStatus: execution.RemoteStatus,
            Message: execution.Message);
    }
}
