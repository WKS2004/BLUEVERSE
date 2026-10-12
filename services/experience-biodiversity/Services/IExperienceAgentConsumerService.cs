using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;

namespace Blueverse.ExperienceBiodiversity.Services;

public interface IExperienceAgentConsumerService
{
    Task<ExperienceAgentInfoResponseDto> GetAgentInfoAsync(CancellationToken cancellationToken = default);

    Task<ExperienceAgentContextReportEnvelopeDto> GenerateContextReportAsync(
        ExperienceAgentContextRequestDto request,
        CancellationToken cancellationToken = default);
}

public sealed class ExperienceAgentConsumerService : IExperienceAgentConsumerService
{
    private static readonly IReadOnlyList<string> AllowlistedTools =
    [
        "destination_lookup",
        "activity_lookup",
        "offering_lookup",
        "schedule_lookup",
        "biodiversity_prediction_lookup"
    ];

    private static readonly IReadOnlyList<string> CanonicalEndpoints =
    [
        "/api/experiences/agent/health",
        "/api/experiences/agent/info",
        "/api/experiences/agent/context-reports",
        "/api/experiences/agent/generate"
    ];

    private readonly IResilientHttpExecutor _resilientExecutor;
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<ExperienceAgentConsumerService> _logger;
    private readonly string _agentBaseUrl;
    private readonly string _defaultModelName;

    public ExperienceAgentConsumerService(
        IResilientHttpExecutor resilientExecutor,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ExperienceAgentConsumerService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _resilientExecutor = resilientExecutor;
        _httpClient = httpClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _agentBaseUrl = (
            configuration["ExperienceAgentUrl"]
            ?? configuration["EXPERIENCE_AGENT_SERVICE_URL"]
            ?? configuration["Services:ExperienceAgentUrl"]
            ?? "http://agent-experience-biodiversity:8080"
        ).TrimEnd('/');
        _defaultModelName = configuration["GEMINI_MODEL"]
            ?? configuration["Gemini:Model"]
            ?? "gemini-2.5-flash";
    }

    public async Task<ExperienceAgentInfoResponseDto> GetAgentInfoAsync(CancellationToken cancellationToken = default)
    {
        var targetUrl = $"{_agentBaseUrl}/api/experiences/agent/info";
        var execution = await _resilientExecutor.ExecuteGetWithAuthorizationAsync(
            targetUrl,
            perAttemptTimeout: TimeSpan.FromSeconds(2),
            maxRetries: 1,
            initialRetryDelay: TimeSpan.FromMilliseconds(100),
            bearerToken: PeerAuthorization.GetBearerToken(_httpContextAccessor),
            cancellationToken: cancellationToken);

        if (execution.Responded && execution.StatusCode == 200 && !string.IsNullOrWhiteSpace(execution.Content))
        {
            try
            {
                using var document = JsonDocument.Parse(execution.Content);
                var root = document.RootElement;
                var agentId = ReadString(root, "agent_id") ?? "v1.agent.experience-biodiversity";
                var agentName = ReadString(root, "name") ?? "Coastal Experience & Biodiversity Agent";
                var owner = ReadString(root, "owner") ?? "Ushan Srinuka";
                var prefix = ReadString(root, "endpoint_prefix") ?? "/api/experiences/agent";
                var modelProvider = ReadString(root, "model_provider") ?? "google-ai-studio";
                var modelName = ReadString(root, "model_name") ?? _defaultModelName;
                var apiKeyConfigured = root.TryGetProperty("gemini_api_key_configured", out var keyProp)
                    && keyProp.ValueKind == JsonValueKind.True;
                var readOnly = !root.TryGetProperty("read_only", out var roProp)
                    || roProp.ValueKind != JsonValueKind.False;

                return new ExperienceAgentInfoResponseDto(
                    AgentId: agentId,
                    AgentName: agentName,
                    Owner: owner,
                    Status: "connected",
                    EndpointPrefix: prefix,
                    ModelProvider: modelProvider,
                    ModelName: modelName,
                    GeminiApiKeyConfigured: apiKeyConfigured,
                    ReadOnly: readOnly,
                    Tools: AllowlistedTools,
                    AgentEndpoints: CanonicalEndpoints,
                    Responded: true,
                    TargetEndpoint: targetUrl,
                    LatencyMs: execution.DurationMs,
                    Detail: "Coastal Experience & Biodiversity Agent runtime is connected and ready.",
                    CheckedAt: DateTimeOffset.UtcNow);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Malformed JSON returned from Experience Agent info endpoint {TargetUrl}", targetUrl);
            }
        }

        return new ExperienceAgentInfoResponseDto(
            AgentId: "v1.agent.experience-biodiversity",
            AgentName: "Coastal Experience & Biodiversity Agent",
            Owner: "Ushan Srinuka",
            Status: "not_connected",
            EndpointPrefix: "/api/experiences/agent",
            ModelProvider: "google-ai-studio",
            ModelName: _defaultModelName,
            GeminiApiKeyConfigured: false,
            ReadOnly: true,
            Tools: AllowlistedTools,
            AgentEndpoints: CanonicalEndpoints,
            Responded: false,
            TargetEndpoint: targetUrl,
            LatencyMs: execution.DurationMs,
            Detail: "Coastal Experience & Biodiversity Agent runtime is not currently reachable; safe fallback active.",
            CheckedAt: DateTimeOffset.UtcNow);
    }

    public async Task<ExperienceAgentContextReportEnvelopeDto> GenerateContextReportAsync(
        ExperienceAgentContextRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var targetUrl = $"{_agentBaseUrl}/api/experiences/agent/context-reports";
        var payloadDict = new Dictionary<string, object?>
        {
            ["workflow_id"] = request.WorkflowId.ToString("D"),
            ["step_id"] = request.StepId.Trim(),
            ["objective"] = request.Objective.Trim(),
            ["include_biodiversity"] = request.IncludeBiodiversity
        };

        if (request.DestinationId.HasValue)
            payloadDict["destination_id"] = request.DestinationId.Value.ToString("D");
        if (request.ActivityId.HasValue)
            payloadDict["activity_id"] = request.ActivityId.Value.ToString("D");
        if (request.OfferingId.HasValue)
            payloadDict["offering_id"] = request.OfferingId.Value.ToString("D");
        if (!string.IsNullOrWhiteSpace(request.TargetDate))
            payloadDict["target_date"] = request.TargetDate.Trim();
        if (request.PeriodStartsAt.HasValue)
            payloadDict["period_starts_at"] = request.PeriodStartsAt.Value.ToString("O");
        if (request.PeriodEndsAt.HasValue)
            payloadDict["period_ends_at"] = request.PeriodEndsAt.Value.ToString("O");

        var jsonBody = JsonSerializer.Serialize(payloadDict);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var bearerToken = PeerAuthorization.GetBearerToken(_httpContextAccessor);
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            using var response = await _httpClient.SendAsync(httpRequest, timeoutCts.Token);
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var rootClone = doc.RootElement.Clone();
                var completionOutcome = ReadString(rootClone, "completion_outcome") ?? "success_complete";
                var modelExecution = TryParseModelExecution(rootClone, _defaultModelName);

                return new ExperienceAgentContextReportEnvelopeDto(
                    AgentId: "v1.agent.experience-biodiversity",
                    AgentName: "Coastal Experience & Biodiversity Agent",
                    Status: "connected",
                    WorkflowId: request.WorkflowId,
                    StepId: request.StepId.Trim(),
                    Objective: request.Objective.Trim(),
                    CompletionOutcome: completionOutcome,
                    ModelExecution: modelExecution,
                    Report: rootClone,
                    Responded: true,
                    TargetEndpoint: targetUrl,
                    LatencyMs: elapsedMs,
                    Detail: "Context report generated by Coastal Experience & Biodiversity Agent.",
                    CheckedAt: DateTimeOffset.UtcNow);
            }

            return FallbackReportEnvelope(
                request,
                targetUrl,
                elapsedMs,
                responded: true,
                status: "unavailable",
                detail: $"Coastal Experience & Biodiversity Agent returned HTTP {(int)response.StatusCode}; safe fallback active.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            _logger.LogInformation(
                "Coastal Experience & Biodiversity Agent at {TargetUrl} was unreachable ({Error}); returning safe not_connected envelope.",
                targetUrl,
                ex.Message);

            return FallbackReportEnvelope(
                request,
                targetUrl,
                stopwatch.ElapsedMilliseconds,
                responded: false,
                status: "not_connected",
                detail: "Coastal Experience & Biodiversity Agent runtime is not currently reachable; safe fallback active.");
        }
    }

    private ExperienceAgentContextReportEnvelopeDto FallbackReportEnvelope(
        ExperienceAgentContextRequestDto request,
        string targetUrl,
        long elapsedMs,
        bool responded,
        string status,
        string detail) =>
        new(
            AgentId: "v1.agent.experience-biodiversity",
            AgentName: "Coastal Experience & Biodiversity Agent",
            Status: status,
            WorkflowId: request.WorkflowId,
            StepId: request.StepId.Trim(),
            Objective: request.Objective.Trim(),
            CompletionOutcome: null,
            ModelExecution: new ExperienceAgentModelExecutionDto(
                Provider: "google-ai-studio",
                Model: _defaultModelName,
                Status: status,
                LatencyMs: 0,
                PromptTokens: null,
                CandidateTokens: null,
                SynthesisSummary: null,
                ErrorSummary: detail),
            Report: null,
            Responded: responded,
            TargetEndpoint: targetUrl,
            LatencyMs: elapsedMs,
            Detail: detail,
            CheckedAt: DateTimeOffset.UtcNow);

    private static ExperienceAgentModelExecutionDto? TryParseModelExecution(JsonElement root, string defaultModel)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("model_execution", out var modelElement)
            || modelElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var provider = ReadString(modelElement, "provider") ?? "google-ai-studio";
        var model = ReadString(modelElement, "model") ?? defaultModel;
        var status = ReadString(modelElement, "status") ?? "skipped_no_api_key";
        var latencyMs = modelElement.TryGetProperty("latency_ms", out var latProp) && latProp.TryGetDouble(out var dVal)
            ? dVal
            : 0.0;
        int? promptTokens = modelElement.TryGetProperty("prompt_tokens", out var ptProp) && ptProp.TryGetInt32(out var ptVal)
            ? ptVal
            : null;
        int? candidateTokens = modelElement.TryGetProperty("candidate_tokens", out var ctProp) && ctProp.TryGetInt32(out var ctVal)
            ? ctVal
            : null;
        var synthesisSummary = ReadString(modelElement, "synthesis_summary");
        var errorSummary = ReadString(modelElement, "error_summary");

        return new ExperienceAgentModelExecutionDto(
            Provider: provider,
            Model: model,
            Status: status,
            LatencyMs: latencyMs,
            PromptTokens: promptTokens,
            CandidateTokens: candidateTokens,
            SynthesisSummary: synthesisSummary,
            ErrorSummary: errorSummary);
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var prop)
        && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
