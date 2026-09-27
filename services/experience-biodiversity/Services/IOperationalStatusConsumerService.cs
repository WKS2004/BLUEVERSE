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
    private readonly string _operationsServiceUrl;

    public OperationalStatusConsumerService(
        IResilientHttpExecutor resilientExecutor,
        IConfiguration configuration,
        ILogger<OperationalStatusConsumerService> logger)
    {
        _resilientExecutor = resilientExecutor;
        _logger = logger;
        _operationsServiceUrl = configuration["OperationsServiceUrl"]
            ?? configuration["OPERATIONS_SERVICE_URL"]
            ?? configuration["Services:OperationsServiceUrl"]
            ?? "http://coastal-operations:8080";
    }

    public async Task<OperationalRestrictionContextDto> GetOperationalStatusAsync(
        Guid destinationId,
        Guid? offeringId,
        CancellationToken cancellationToken = default)
    {
        var targetUrl = $"{_operationsServiceUrl.TrimEnd('/')}/api/operations/restrictions/check?destinationId={destinationId}";
        if (offeringId.HasValue)
        {
            targetUrl += $"&offeringId={offeringId.Value}";
        }

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

                var hasRestriction = root.TryGetProperty("hasRestriction", out var hrProp) && hrProp.GetBoolean();
                string? restrictionType = root.TryGetProperty("restrictionType", out var rtProp) ? rtProp.GetString() : null;
                string? severity = root.TryGetProperty("severity", out var sProp) ? sProp.GetString() : null;
                string? reason = root.TryGetProperty("reason", out var rProp) ? rProp.GetString() : null;
                DateTimeOffset? until = root.TryGetProperty("effectiveUntil", out var uProp) && uProp.TryGetDateTimeOffset(out var dto) ? dto : null;

                return new OperationalRestrictionContextDto(
                    HasRestriction: hasRestriction,
                    RestrictionType: restrictionType,
                    Severity: severity,
                    Reason: reason,
                    EffectiveUntil: until,
                    SourceStatus: "AUTHORITATIVE_REMOTE",
                    Responded: true,
                    TargetEndpoint: targetUrl,
                    AttemptsCount: execution.AttemptsCount,
                    LatencyMs: execution.DurationMs,
                    RemoteStatus: "RESPONDED",
                    Message: execution.Message);
            }
            catch (JsonException jEx)
            {
                _logger.LogWarning(jEx, "Failed to parse JSON response from Member 4 Operations service at {TargetUrl}", targetUrl);
            }
        }

        // Resilient safe fallback when remote operational service did not respond or failed
        var statusReason = execution.Responded
            ? $"Operations service returned non-success code {execution.StatusCode}; operational state cannot be optimistically confirmed."
            : $"Remote coastal-operations microservice did not respond ({execution.RemoteStatus}) after {execution.AttemptsCount} attempts. Operational review cannot be optimistically confirmed.";

        return new OperationalRestrictionContextDto(
            HasRestriction: false,
            RestrictionType: null,
            Severity: null,
            Reason: statusReason,
            EffectiveUntil: null,
            SourceStatus: "UNAVAILABLE",
            Responded: execution.Responded,
            TargetEndpoint: targetUrl,
            AttemptsCount: execution.AttemptsCount,
            LatencyMs: execution.DurationMs,
            RemoteStatus: execution.RemoteStatus,
            Message: execution.Message);
    }

    public async Task<OperationalAdvisoriesResponseDto> GetOperationalAdvisoriesAsync(
        Guid destinationId,
        string destinationName,
        CancellationToken cancellationToken = default)
    {
        var targetUrl = $"{_operationsServiceUrl.TrimEnd('/')}/api/operations/advisories/active?destinationId={destinationId}";

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

                var advisories = new List<ActiveAdvisoryItemDto>();
                if (root.TryGetProperty("advisories", out var advArray) && advArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in advArray.EnumerateArray())
                    {
                        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                        var title = item.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "Operational Notice" : "Operational Notice";
                        var severity = item.TryGetProperty("severity", out var sProp) ? sProp.GetString() ?? "INFO" : "INFO";
                        var desc = item.TryGetProperty("description", out var dProp) ? dProp.GetString() ?? "" : "";
                        var issued = item.TryGetProperty("issuedAt", out var iProp) && iProp.TryGetDateTimeOffset(out var iDto) ? iDto : DateTimeOffset.UtcNow;
                        var expires = item.TryGetProperty("expiresAt", out var eProp) && eProp.TryGetDateTimeOffset(out var eDto) ? eDto : (DateTimeOffset?)null;

                        advisories.Add(new ActiveAdvisoryItemDto(id, title, severity, desc, issued, expires));
                    }
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
            catch (JsonException jEx)
            {
                _logger.LogWarning(jEx, "Failed to parse advisories JSON from Member 4 service at {TargetUrl}", targetUrl);
            }
        }

        var message = execution.Responded
            ? $"Remote operations microservice returned status {execution.StatusCode}. No remote advisories loaded."
            : $"Remote coastal-operations microservice did not respond ({execution.RemoteStatus}) after {execution.AttemptsCount} attempts. Assumed baseline: no active emergency coastal closures.";

        return new OperationalAdvisoriesResponseDto(
            DestinationId: destinationId,
            DestinationName: destinationName,
            Responded: execution.Responded,
            TargetEndpoint: targetUrl,
            AttemptsCount: execution.AttemptsCount,
            LatencyMs: execution.DurationMs,
            RemoteStatus: execution.RemoteStatus,
            Advisories: Array.Empty<ActiveAdvisoryItemDto>(),
            FallbackUsed: true,
            Message: message);
    }
}
