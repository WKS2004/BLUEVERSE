using System.ComponentModel.DataAnnotations;

namespace Blueverse.CoastalOperations.Contracts;

public sealed class AuditListQuery
{
    [MaxLength(256)] public string? Cursor { get; init; }
    [Range(1, 100)] public int PageSize { get; init; } = 25;
}

public sealed record OperationsAuditResponse(
    Guid AuditId, string ResourceType, Guid ResourceId, string Action,
    Guid ActorId, string CorrelationId, DateTimeOffset CreatedAt,
    string? ActorName = null, IReadOnlyList<string>? ActorRoles = null,
    string? RecordTitle = null, string? Summary = null, IReadOnlyList<AuditFieldChange>? Changes = null);

public sealed record AuditFieldChange(string Field, string? Before, string? After);

public sealed record OperationsAuditPage(IReadOnlyList<OperationsAuditResponse> Items, string? NextCursor);
