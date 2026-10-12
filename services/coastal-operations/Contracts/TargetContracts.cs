namespace Blueverse.CoastalOperations.Contracts;

public sealed record OperationalStatusResponse(
    string TargetType,
    Guid TargetId,
    string OperationalState,
    int StateVersion,
    DateTimeOffset UpdatedAt);

public sealed record OperationalHistoryItem(
    Guid HistoryId,
    string PreviousState,
    string NewState,
    Guid AssessmentId,
    Guid DecisionId,
    Guid ActorId,
    DateTimeOffset CreatedAt);

public sealed record OperationalHistoryResponse(
    string TargetType,
    Guid TargetId,
    IReadOnlyList<OperationalHistoryItem> Items,
    string? NextCursor);
