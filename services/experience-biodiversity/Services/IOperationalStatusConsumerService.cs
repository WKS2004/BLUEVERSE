using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;

namespace Blueverse.ExperienceBiodiversity.Services;

public interface IOperationalStatusConsumerService
{
    Task<OperationalRestrictionContextDto> GetOperationalStatusAsync(
        Guid destinationId,
        Guid? offeringId,
        CancellationToken cancellationToken = default);

    Task<OperationalAdvisoriesResponseDto> GetOperationalAdvisoriesAsync(
        Guid destinationId,
        string destinationName,
        CancellationToken cancellationToken = default);
}

public sealed class OperationalStatusConsumerService : IOperationalStatusConsumerService
{
    private readonly IResilientHttpExecutor _resilientExecutor;
    private readonly ILogger<OperationalStatusConsumerService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly string _publicApiUrl;

    public OperationalStatusConsumerService(
        IResilientHttpExecutor resilientExecutor,
        IConfiguration configuration,
        ILogger<OperationalStatusConsumerService> logger,
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

    public async Task<OperationalRestrictionContextDto> GetOperationalStatusAsync(
        Guid destinationId,
        Guid? offeringId,
        CancellationToken cancellationToken = default)
    {
        var targetType = offeringId.HasValue ? "OFFERING" : "DESTINATION";
        var targetId = offeringId ?? destinationId;
        var targetUrl = $"{_publicApiUrl.TrimEnd('/')}/api/operations/targets/{targetType}/{targetId:D}/status";
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
                var returnedTargetType = ReadString(root, "targetType");
                var returnedTargetId = ReadGuid(root, "targetId");
                var state = ReadString(root, "operationalState");
                var version = ReadInt32(root, "stateVersion");
                var updatedAt = ReadTimestamp(root, "updatedAt");
                if (!string.Equals(returnedTargetType, targetType, StringComparison.Ordinal) || returnedTargetId != targetId ||
                    state is not ("OPEN" or "CAUTION" or "TEMPORARILY_SUSPENDED" or "CANCELLED" or "COMPLETED") ||
                    version is null or <= 0 || updatedAt is null)
                {
                    _logger.LogWarning("Coastal Operations returned an invalid target-status response for {TargetType} {TargetId}.", targetType, targetId);
                }
                else
                {
                    var hasRestriction = state != "OPEN";
                    return new OperationalRestrictionContextDto(
                        HasRestriction: hasRestriction,
                        RestrictionType: hasRestriction ? state : null,
                        Severity: null,
                        Reason: hasRestriction ? $"Coastal Operations reports {state} for this target." : "Coastal Operations reports OPEN for this target.",
                        EffectiveUntil: null,
                        SourceStatus: "AUTHORITATIVE_REMOTE",
                        Responded: true,
                        TargetEndpoint: targetUrl,
                        AttemptsCount: execution.AttemptsCount,
                        LatencyMs: execution.DurationMs,
                        RemoteStatus: "RESPONDED",
                        Message: execution.Message,
                        TargetType: returnedTargetType,
                        TargetId: returnedTargetId,
                        OperationalState: state,
                        StateVersion: version,
                        UpdatedAt: updatedAt);
                }
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Failed to parse Coastal Operations target status from {TargetUrl}", targetUrl);
            }
        }

        var reason = execution.StatusCode == 404
            ? "Coastal Operations has not established authoritative state for this target."
            : execution.Responded
                ? $"Coastal Operations returned HTTP {execution.StatusCode}; operational state cannot be confirmed."
                : $"Coastal Operations could not be reached ({execution.RemoteStatus}); operational state cannot be confirmed.";
        return new OperationalRestrictionContextDto(
            HasRestriction: false,
            RestrictionType: null,
            Severity: null,
            Reason: reason,
            EffectiveUntil: null,
            SourceStatus: "UNAVAILABLE",
            Responded: execution.Responded,
            TargetEndpoint: targetUrl,
            AttemptsCount: execution.AttemptsCount,
            LatencyMs: execution.DurationMs,
            RemoteStatus: execution.RemoteStatus,
            Message: execution.Message,
            TargetType: targetType,
            TargetId: targetId);
    }

    public async Task<OperationalAdvisoriesResponseDto> GetOperationalAdvisoriesAsync(
        Guid destinationId,
        string destinationName,
        CancellationToken cancellationToken = default)
    {
        var targetUrl = $"{_publicApiUrl.TrimEnd('/')}/api/operations/alerts?targetType=DESTINATION&targetId={destinationId:D}&lifecycle=ACTIVE&visibility=PUBLIC&pageSize=100";
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
                if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    var now = DateTimeOffset.UtcNow;
                    var advisories = new List<ActiveAdvisoryItemDto>();
                    foreach (var item in items.EnumerateArray())
                    {
                        var alertId = ReadGuid(item, "alertId");
                        var title = ReadString(item, "title");
                        var severity = ReadString(item, "severity");
                        var description = ReadString(item, "description");
                        var validFrom = ReadTimestamp(item, "validFrom");
                        var validUntil = ReadTimestamp(item, "validUntil");
                        if (alertId is null || alertId.Value == Guid.Empty || string.IsNullOrWhiteSpace(title) ||
                            string.IsNullOrWhiteSpace(description) || severity is not ("LOW" or "MODERATE" or "HIGH" or "CRITICAL") ||
                            validFrom is null || validUntil is null || validUntil <= validFrom)
                        {
                            _logger.LogWarning("Coastal Operations returned a malformed active alert for destination {DestinationId}; the alert was omitted.", destinationId);
                            continue;
                        }

                        if (validFrom <= now && validUntil > now)
                            advisories.Add(new ActiveAdvisoryItemDto(alertId.Value.ToString("D"), title, severity, description, validFrom.Value, validUntil.Value));
                    }

                    return new OperationalAdvisoriesResponseDto(
                        DestinationId: destinationId,
                        DestinationName: destinationName,
                        Responded: true,
                        TargetEndpoint: targetUrl,
                        AttemptsCount: execution.AttemptsCount,
                        LatencyMs: execution.DurationMs,
                        RemoteStatus: "RESPONDED",
                        Advisories: advisories,
                        FallbackUsed: false,
                        Message: execution.Message);
                }
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Failed to parse Coastal Operations alerts from {TargetUrl}", targetUrl);
            }
        }

        var message = execution.Responded
            ? $"Coastal Operations returned HTTP {execution.StatusCode}; public advisories are unavailable."
            : $"Coastal Operations did not respond after {execution.AttemptsCount} attempts ({execution.RemoteStatus}); public advisories are unavailable.";
        return new OperationalAdvisoriesResponseDto(
            DestinationId: destinationId,
            DestinationName: destinationName,
            Responded: execution.Responded,
            TargetEndpoint: targetUrl,
            AttemptsCount: execution.AttemptsCount,
            LatencyMs: execution.DurationMs,
            RemoteStatus: execution.RemoteStatus,
            Advisories: [],
            FallbackUsed: true,
            Message: message);
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Guid? ReadGuid(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id) ? id : null;

    private static int? ReadInt32(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static DateTimeOffset? ReadTimestamp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var timestamp) ? timestamp : null;
}
