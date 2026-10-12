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
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly string _publicApiUrl;

    public BiodiversityConsumerService(
        IResilientHttpExecutor resilientExecutor,
        IConfiguration configuration,
        ILogger<BiodiversityConsumerService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _resilientExecutor = resilientExecutor;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _publicApiUrl = configuration["PublicApiUrl"]
            ?? configuration["PUBLIC_API_URL"]
            ?? configuration["Services:PublicApiUrl"]
            ?? "http://api:8080";
    }

    public async Task<BiodiversityContextResponseDto> GetBiodiversityContextAsync(
        Guid destinationId,
        string destinationName,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var targetUrl = BuildPredictionsUrl(_publicApiUrl, destinationId);
        var execution = await _resilientExecutor.ExecuteGetWithAuthorizationAsync(
            targetUrl,
            perAttemptTimeout: TimeSpan.FromSeconds(2),
            maxRetries: 2,
            initialRetryDelay: TimeSpan.FromMilliseconds(100),
            bearerToken: PeerAuthorization.GetBearerToken(_httpContextAccessor),
            cancellationToken: cancellationToken);

        if (execution.Responded && execution.StatusCode == 200 && !string.IsNullOrWhiteSpace(execution.Content))
        {
            try
            {
                using var document = JsonDocument.Parse(execution.Content);
                var root = document.RootElement;
                var status = ReadString(root, "status")?.ToLowerInvariant();
                var responseDestinationId = ReadGuid(root, "destinationId");
                if (responseDestinationId != destinationId)
                    return Unavailable(destinationId, destinationName, latitude, longitude, targetUrl, execution,
                        "The biodiversity service returned a different destination; its predictions were discarded.", "invalid");

                if (status != "available")
                {
                    var message = ReadString(root, "message") ?? ReadString(root, "limitations") ??
                        "The biodiversity service has no current prediction for this destination.";
                    return Unavailable(destinationId, destinationName, latitude, longitude, targetUrl, execution, message,
                        status is "stale" or "invalid" or "not_requested" ? status : "unavailable");
                }

                var metadata = TryGetObject(root, "modelMetadata");
                var provenance = TryGetObject(root, "provenance");
                var modelVersion = ReadString(metadata, "modelVersion") ?? ReadString(provenance, "modelVersion");
                var modelSource = ReadString(provenance, "provider");
                var evaluatedAt = ReadTimestamp(metadata, "inferenceTimestamp") ?? ReadTimestamp(provenance, "inferenceTimestamp");
                if (string.IsNullOrWhiteSpace(modelVersion) || string.IsNullOrWhiteSpace(modelSource) || evaluatedAt is null)
                    return Unavailable(destinationId, destinationName, latitude, longitude, targetUrl, execution,
                        "The biodiversity service omitted required model provenance; its predictions were discarded.", "invalid");

                if (!TryReadPredictions(root, out var predictions))
                    return Unavailable(destinationId, destinationName, latitude, longitude, targetUrl, execution,
                        "The biodiversity service returned a malformed prediction; its predictions were discarded.", "invalid");

                var limitations = ReadString(root, "limitations");
                var uncertainty = ReadString(root, "uncertaintyText");
                return new BiodiversityContextResponseDto(
                    DestinationId: destinationId,
                    DestinationName: destinationName,
                    Latitude: latitude,
                    Longitude: longitude,
                    Status: "available",
                    Predictions: predictions,
                    ModelVersion: modelVersion,
                    ModelSource: modelSource,
                    UncertaintyNotes: JoinContext(limitations, uncertainty),
                    EvaluatedAt: evaluatedAt,
                    Disclaimer: "Biodiversity predictions are informative model outputs, not confirmed sightings, conservation assessments, safety decisions or availability evidence.",
                    Responded: true,
                    TargetEndpoint: targetUrl,
                    AttemptsCount: execution.AttemptsCount,
                    LatencyMs: execution.DurationMs,
                    RemoteStatus: "RESPONDED",
                    Message: ReadString(root, "message"));
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Failed to parse Coastal Planner biodiversity response from {TargetUrl}", targetUrl);
            }
        }

        var failureMessage = execution.Responded
            ? $"Coastal Planner returned HTTP {execution.StatusCode}; the optional context is unavailable."
            : $"The optional context from Coastal Planner did not respond ({execution.RemoteStatus}); biodiversity predictions are unavailable.";
        return Unavailable(destinationId, destinationName, latitude, longitude, targetUrl, execution, failureMessage);
    }

    private static bool TryReadPredictions(JsonElement root, out IReadOnlyList<FocalSpeciesPredictionDto> predictions)
    {
        predictions = [];
        if (!root.TryGetProperty("predictedSpecies", out var speciesArray) || speciesArray.ValueKind != JsonValueKind.Array || speciesArray.GetArrayLength() > 100)
            return false;

        var result = new List<FocalSpeciesPredictionDto>(speciesArray.GetArrayLength());
        foreach (var item in speciesArray.EnumerateArray())
        {
            var speciesId = ReadGuid(item, "speciesId");
            var scientificName = ReadString(item, "scientificName");
            var commonName = ReadString(item, "commonName");
            if (speciesId is null || speciesId.Value == Guid.Empty || string.IsNullOrWhiteSpace(scientificName) ||
                string.IsNullOrWhiteSpace(commonName) ||
                !item.TryGetProperty("habitatSuitability", out var suitabilityValue) ||
                suitabilityValue.ValueKind != JsonValueKind.Number || !suitabilityValue.TryGetDouble(out var suitability) ||
                !double.IsFinite(suitability) || suitability is < 0 or > 1)
                return false;

            result.Add(new FocalSpeciesPredictionDto(
                SpeciesId: speciesId.Value,
                SpeciesName: commonName,
                ScientificName: scientificName,
                ConservationStatus: null,
                OccurrenceProbability: null,
                HabitatSuitability: suitability,
                PrimaryThreats: null,
                ConfidenceLevel: ReadString(item, "confidenceLevel"),
                UncertaintyText: ReadString(item, "uncertaintyText")));
        }

        predictions = result;
        return true;
    }

    private static BiodiversityContextResponseDto Unavailable(
        Guid destinationId,
        string destinationName,
        double latitude,
        double longitude,
        string targetUrl,
        ResilientExecutionResult execution,
        string message,
        string status = "unavailable") => new(
        DestinationId: destinationId,
        DestinationName: destinationName,
        Latitude: latitude,
        Longitude: longitude,
        Status: status,
        Predictions: [],
        ModelVersion: null,
        ModelSource: null,
        UncertaintyNotes: message,
        EvaluatedAt: null,
        Disclaimer: "Biodiversity predictions are informative model outputs, not confirmed sightings, conservation assessments, safety decisions or availability evidence.",
        Responded: execution.Responded,
        TargetEndpoint: targetUrl,
        AttemptsCount: execution.AttemptsCount,
        LatencyMs: execution.DurationMs,
        RemoteStatus: execution.RemoteStatus,
        Message: message);

    private static string BuildPredictionsUrl(string baseUrl, Guid destinationId) =>
        $"{baseUrl.TrimEnd('/')}/api/planner/biodiversity/predictions?destinationId={Uri.EscapeDataString(destinationId.ToString("D"))}";

    private static JsonElement TryGetObject(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;

    private static string? ReadString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Guid? ReadGuid(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id)
            ? id
            : null;

    private static DateTimeOffset? ReadTimestamp(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var timestamp)
            ? timestamp
            : null;

    private static string JoinContext(string? limitations, string? uncertainty)
    {
        var values = new[] { limitations, uncertainty }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal);
        return string.Join(" ", values);
    }
}
