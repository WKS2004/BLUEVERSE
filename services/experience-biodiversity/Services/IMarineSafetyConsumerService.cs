using System.Globalization;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;

namespace Blueverse.ExperienceBiodiversity.Services;

public interface IMarineSafetyConsumerService
{
    Task<MarineConditionsContextDto> GetMarineConditionsAsync(
        Guid destinationId,
        string destinationName,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}

public sealed class MarineSafetyConsumerService : IMarineSafetyConsumerService
{
    private const double KilometresPerHourPerKnot = 1.852;
    private readonly IResilientHttpExecutor _resilientExecutor;
    private readonly ILogger<MarineSafetyConsumerService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly string _publicApiUrl;

    public MarineSafetyConsumerService(
        IResilientHttpExecutor resilientExecutor,
        IConfiguration configuration,
        ILogger<MarineSafetyConsumerService> logger,
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

    public async Task<MarineConditionsContextDto> GetMarineConditionsAsync(
        Guid destinationId,
        string destinationName,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var targetUrl = BuildCurrentConditionsUrl(_publicApiUrl, latitude, longitude);
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
                var windSpeedKmh = ReadNullableDouble(root, "windSpeed");
                var waveHeight = ReadNullableDouble(root, "waveHeight");
                var swellHeight = ReadNullableDouble(root, "swellHeight");
                var forecastTime = ReadTimestamp(root, "forecastTime");
                var retrievedAt = ReadTimestamp(root, "retrievedAt");
                var freshness = ReadString(root, "freshnessStatus");
                var missingFields = ReadStringArray(root, "missingFields");

                return new MarineConditionsContextDto(
                    DestinationId: destinationId,
                    DestinationName: destinationName,
                    Latitude: latitude,
                    Longitude: longitude,
                    Responded: true,
                    TargetEndpoint: targetUrl,
                    AttemptsCount: execution.AttemptsCount,
                    LatencyMs: execution.DurationMs,
                    RemoteStatus: "RESPONDED",
                    SafetyLevel: "UNKNOWN",
                    WaterCondition: "UNKNOWN",
                    WaveHeightMeters: waveHeight,
                    WindSpeedKnots: windSpeedKmh is { } kmh ? kmh / KilometresPerHourPerKnot : null,
                    TideStatus: null,
                    AdvisoryMessage: "Marine telemetry is available. This endpoint does not classify safety or water conditions; follow local beach flags and lifeguard directives.",
                    FallbackUsed: false,
                    EvaluatedAt: retrievedAt,
                    Disclaimer: "Marine telemetry is dynamic environmental evidence, not a safety classification. Always obey local beach flags and lifeguard directives.",
                    Source: ReadString(root, "source"),
                    FreshnessStatus: freshness,
                    MissingFields: missingFields,
                    SnapshotId: ReadGuid(root, "id"),
                    ForecastTime: forecastTime,
                    RetrievedAt: retrievedAt);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Failed to parse Marine Safety current-conditions response from {TargetUrl}", targetUrl);
            }
        }

        var message = execution.Responded
            ? $"Marine Safety returned HTTP {execution.StatusCode}; safety classification and telemetry are unavailable."
            : $"Marine Safety did not respond after {execution.AttemptsCount} attempts ({execution.RemoteStatus}); safety classification and telemetry are unavailable.";

        return new MarineConditionsContextDto(
            DestinationId: destinationId,
            DestinationName: destinationName,
            Latitude: latitude,
            Longitude: longitude,
            Responded: execution.Responded,
            TargetEndpoint: targetUrl,
            AttemptsCount: execution.AttemptsCount,
            LatencyMs: execution.DurationMs,
            RemoteStatus: execution.RemoteStatus,
            SafetyLevel: "UNKNOWN",
            WaterCondition: "UNKNOWN",
            WaveHeightMeters: null,
            WindSpeedKnots: null,
            TideStatus: null,
            AdvisoryMessage: message,
            FallbackUsed: true,
            EvaluatedAt: null,
            Disclaimer: "Marine telemetry is dynamic environmental evidence, not a safety classification. Always obey local beach flags and lifeguard directives.",
            Source: null,
            FreshnessStatus: null,
            MissingFields: [],
            SnapshotId: null,
            ForecastTime: null,
            RetrievedAt: null);
    }

    private static string BuildCurrentConditionsUrl(string baseUrl, double latitude, double longitude) =>
        $"{baseUrl.TrimEnd('/')}/api/marine/current?latitude={latitude.ToString("R", CultureInfo.InvariantCulture)}&longitude={longitude.ToString("R", CultureInfo.InvariantCulture)}";

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? ReadNullableDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? number
            : null;

    private static DateTimeOffset? ReadTimestamp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var timestamp)
            ? timestamp
            : null;

    private static Guid? ReadGuid(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id)
            ? id
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray()
            : [];
}
