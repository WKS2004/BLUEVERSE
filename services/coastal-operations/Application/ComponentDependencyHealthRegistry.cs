using System.Collections.Concurrent;
using Blueverse.CoastalOperations.Contracts;

namespace Blueverse.CoastalOperations.Application;

public sealed class ComponentDependencyHealthRegistry
{
    private static readonly string[] Services =
    [
        "member-1-experience",
        "member-2-marine-safety",
        "member-3-coastal-planner"
    ];

    private readonly ConcurrentDictionary<string, ComponentDependencyHealth> _latest = new(StringComparer.Ordinal);

    public void Record(ComponentDependencyResult result)
    {
        var snapshot = new ComponentDependencyHealth(
            result.Service,
            result.Status,
            result.Attempts,
            result.Retries,
            result.Retryable,
            result.ErrorCode,
            result.Message,
            result.CheckedAt);
        _latest.AddOrUpdate(
            result.Service,
            snapshot,
            (_, current) => current.CheckedAt is null ||
                            snapshot.CheckedAt is null ||
                            snapshot.CheckedAt >= current.CheckedAt
                ? snapshot
                : current);
    }

    public IReadOnlyList<ComponentDependencyHealth> GetSnapshot() => Services
        .Select(service => _latest.TryGetValue(service, out var health)
            ? health
            : new ComponentDependencyHealth(
                service,
                "NOT_CHECKED",
                0,
                0,
                Retryable: false,
                ErrorCode: null,
                Message: "No assessment request has checked this dependency yet.",
                CheckedAt: null))
        .ToArray();
}
