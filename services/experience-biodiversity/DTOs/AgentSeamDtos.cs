namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record AgentContextResponseDto(
    string AgentName,
    string Status, // "not_connected"
    string Detail,
    IReadOnlyList<string> PlannedTools,
    DateTimeOffset CheckedAt);
