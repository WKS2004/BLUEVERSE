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
    private readonly IResilientHttpExecutor _resilientExecutor;
    private readonly ILogger<MarineSafetyConsumerService> _logger;
    private readonly string _marineSafetyUrl;

    public MarineSafetyConsumerService(
        IResilientHttpExecutor resilientExecutor,
        IConfiguration configuration,
        ILogger<MarineSafetyConsumerService> logger)
    {
        _resilientExecutor = resilientExecutor;
        _logger = logger;
        _marineSafetyUrl = configuration["MarineSafetyUrl"]
            ?? configuration["MARINE_SAFETY_URL"]
            ?? configuration["Services:MarineSafetyUrl"]
            ?? "http://marine-safety:8080";
    }

    public async Task<MarineConditionsContextDto> GetMarineConditionsAsync(
        Guid destinationId,
        string destinationName,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var targetUrl = $"{_marineSafetyUrl.TrimEnd('/')}/api/marine/conditions?destinationId={destinationId}&latitude={latitude}&longitude={longitude}";

        // Resilient query with 2-second timeout and 2 retries
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

                var safetyLevel = root.TryGetProperty("safetyLevel", out var slProp) ? slProp.GetString() ?? "SAFE" : "SAFE";
                var waterCond = root.TryGetProperty("waterCondition", out var wcProp) ? wcProp.GetString() ?? "CALM" : "CALM";
                double? waveHeight = root.TryGetProperty("waveHeightMeters", out var whProp) && whProp.TryGetDouble(out var wh) ? wh : null;
                double? windSpeed = root.TryGetProperty("windSpeedKnots", out var wsProp) && wsProp.TryGetDouble(out var ws) ? ws : null;
                var tideStatus = root.TryGetProperty("tideStatus", out var tsProp) ? tsProp.GetString() : null;
                var advisory = root.TryGetProperty("advisory", out var advProp) ? advProp.GetString() : null;
                var evalAt = root.TryGetProperty("updatedAt", out var upProp) && upProp.TryGetDateTimeOffset(out var upDto) ? upDto : DateTimeOffset.UtcNow;

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
                    SafetyLevel: safetyLevel,
                    WaterCondition: waterCond,
                    WaveHeightMeters: waveHeight,
                    WindSpeedKnots: windSpeed,
                    TideStatus: tideStatus,
                    AdvisoryMessage: advisory ?? "Current marine conditions reported by Marine Safety Intelligence service.",
                    FallbackUsed: false,
                    EvaluatedAt: evalAt,
                    Disclaimer: "Marine safety conditions are dynamic oceanographic telemetry; always obey local beach flags and lifeguard directives.");
            }
            catch (JsonException jEx)
            {
                _logger.LogWarning(jEx, "Failed to parse JSON response from Member 2 Marine Safety service at {TargetUrl}", targetUrl);
            }
        }

        // Resilient safe fallback when remote Marine Safety service did not respond or failed
        var fallbackAdvisory = execution.Responded
            ? $"Remote marine-safety microservice returned status {execution.StatusCode}. Standard coastal caution advised."
            : $"Remote marine-safety microservice did not respond ({execution.RemoteStatus}) after {execution.AttemptsCount} attempts ({execution.Message}). Safe fallback: exercise standard coastal caution.";

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
            AdvisoryMessage: fallbackAdvisory,
            FallbackUsed: true,
            EvaluatedAt: DateTimeOffset.UtcNow,
            Disclaimer: "Marine safety conditions are dynamic oceanographic telemetry; always obey local beach flags and lifeguard directives.");
    }
}
