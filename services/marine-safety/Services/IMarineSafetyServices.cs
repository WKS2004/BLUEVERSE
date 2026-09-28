using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Models;

namespace Blueverse.MarineSafety.Services;

/// <summary>Condition acquisition, snapshot persistence and history queries.</summary>
public interface IConditionService
{
    /// <summary>
    /// Returns the current/relevant conditions for a location and time,
    /// reusing a fresh persisted snapshot when one exists and otherwise
    /// acquiring from Open-Meteo and persisting a new snapshot.
    /// </summary>
    Task<ConditionSnapshot> GetConditionsAsync(decimal latitude, decimal longitude, DateTime? timeUtc, CancellationToken cancellationToken);

    /// <summary>Returns one stored snapshot by ID.</summary>
    Task<ConditionSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns stored snapshots filtered by location and optional time window.</summary>
    Task<IReadOnlyList<ConditionSnapshot>> GetHistoryAsync(
        decimal? latitude,
        decimal? longitude,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);
}

/// <summary>Marine activity reference-table management (full CRUD; delete is a deactivation).</summary>
public interface IActivityService
{
    /// <summary>Returns activity reference rows ordered by name, optionally filtered by active state.</summary>
    Task<IReadOnlyList<MarineActivityDto>> GetActivitiesAsync(bool? isActive, CancellationToken cancellationToken);

    /// <summary>Returns one activity reference row by ID.</summary>
    Task<MarineActivityDto?> GetActivityAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Creates an active activity reference row with a unique name.</summary>
    Task<MarineActivityDto> CreateActivityAsync(CreateActivityDto dto, CancellationToken cancellationToken);

    /// <summary>Renames or retypes an activity; the name stays unique.</summary>
    Task<MarineActivityDto?> UpdateActivityAsync(Guid id, UpdateActivityDto dto, CancellationToken cancellationToken);

    /// <summary>Deactivates an activity without deleting it (profile and assessment references survive).</summary>
    Task<bool> DeactivateActivityAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Deterministic suitability evaluation.</summary>
public interface ISuitabilityService
{
    Task<SuitabilityResultDto> EvaluateAsync(EvaluateSuitabilityDto request, CancellationToken cancellationToken);

    /// <summary>Returns persisted assessment history, newest first, filtered by activity, result and time window.</summary>
    Task<IReadOnlyList<AssessmentHistoryDto>> GetAssessmentsAsync(
        Guid? activityId,
        string? result,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);

    /// <summary>Returns one persisted assessment by ID.</summary>
    Task<AssessmentHistoryDto?> GetAssessmentAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Safety profile management.</summary>
public interface ISafetyProfileService
{
    Task<IReadOnlyList<SafetyProfileDto>> GetProfilesAsync(CancellationToken cancellationToken);
    Task<SafetyProfileDto?> GetProfileAsync(Guid id, CancellationToken cancellationToken);
    Task<SafetyProfileDto?> GetProfileForActivityAsync(Guid activityId, CancellationToken cancellationToken);
    Task<SafetyProfileDto> CreateProfileAsync(CreateSafetyProfileDto dto, CancellationToken cancellationToken);
    Task<SafetyProfileDto?> UpdateProfileAsync(Guid id, UpdateSafetyProfileDto dto, CancellationToken cancellationToken);
    Task<bool> DeactivateProfileAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// API time normalization. The public contract exchanges UTC timestamps
/// (G00 time semantics); a timestamp supplied without an offset is UTC — never
/// the host machine's local time zone, which would make parsed request times
/// depend on where the service happens to run.
/// </summary>
public static class MarineTime
{
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
