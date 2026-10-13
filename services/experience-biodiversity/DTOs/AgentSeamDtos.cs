using System.Text.Json;

namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record AgentContextResponseDto(
    string AgentName,
    string Status, // "not_connected"
    string Detail,
    IReadOnlyList<string> PlannedTools,
    DateTimeOffset CheckedAt);

public sealed record ExperienceAgentInfoResponseDto(
    string AgentId,
    string AgentName,
    string Owner,
    string Status, // "connected" | "not_connected"
    string EndpointPrefix,
    string ModelProvider,
    string ModelName,
    bool GeminiApiKeyConfigured,
    bool ReadOnly,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> AgentEndpoints,
    bool Responded,
    string TargetEndpoint,
    long LatencyMs,
    string Detail,
    DateTimeOffset CheckedAt);

public sealed record ExperienceAgentContextRequestDto(
    Guid WorkflowId,
    string StepId,
    string Objective,
    Guid? DestinationId = null,
    Guid? ActivityId = null,
    Guid? OfferingId = null,
    string? TargetDate = null,
    DateTimeOffset? PeriodStartsAt = null,
    DateTimeOffset? PeriodEndsAt = null,
    bool IncludeBiodiversity = false);

public sealed record ExperienceAgentModelExecutionDto(
    string Provider,
    string Model,
    string Status,
    double LatencyMs,
    int? PromptTokens,
    int? CandidateTokens,
    string? SynthesisSummary,
    string? ErrorSummary);

public sealed record ExperienceAgentContextReportEnvelopeDto(
    string AgentId,
    string AgentName,
    string Status, // "connected" | "not_connected" | "unavailable"
    Guid WorkflowId,
    string StepId,
    string Objective,
    string? CompletionOutcome,
    ExperienceAgentModelExecutionDto? ModelExecution,
    JsonElement? Report,
    bool Responded,
    string TargetEndpoint,
    long LatencyMs,
    string Detail,
    DateTimeOffset CheckedAt);
