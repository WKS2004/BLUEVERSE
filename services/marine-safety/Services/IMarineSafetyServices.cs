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

/// <summary>Read-only access to Member 1's published activity catalogue.</summary>
public interface IActivityService
{
    Task<IReadOnlyList<MarineActivityDto>> GetActivitiesAsync(bool? isActive, CancellationToken cancellationToken);
    Task<MarineActivityDto?> GetActivityAsync(Guid id, CancellationToken cancellationToken);
    Task<MarineActivity?> ResolvePublishedAsync(Guid id, CancellationToken cancellationToken);
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
    Task<SafetyProfileDto> CreateProfileAsync(CreateSafetyProfileDto dto, Guid actorUserId, CancellationToken cancellationToken);
    Task<SafetyProfileDto?> UpdateProfileAsync(Guid id, UpdateSafetyProfileDto dto, Guid actorUserId, CancellationToken cancellationToken);
    Task<SafetyProfileDto?> ReviewProfileAsync(Guid id, Guid reviewerUserId, CancellationToken cancellationToken);
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
    // One hour of headroom permits the provider query and forecast window.
    public static readonly DateTime MinimumRequestedUtc = DateTime.SpecifyKind(DateTime.MinValue.AddHours(1), DateTimeKind.Utc);
    public static readonly DateTime MaximumRequestedUtc = DateTime.SpecifyKind(DateTime.MaxValue.AddHours(-1), DateTimeKind.Utc);

    public static DateTime RequireRequested(DateTime value)
    {
        var utc = ToUtc(value);
        if (utc < MinimumRequestedUtc || utc > MaximumRequestedUtc)
            throw new MarineTimeValidationException();
        return utc;
    }

    public static (DateTime Earliest, DateTime Latest) ForecastWindow(DateTime requested)
    {
        var utc = RequireRequested(requested);
        return (utc.AddMinutes(-30), utc.AddHours(1));
    }

    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed class MarineTimeValidationException : Exception
{
    public MarineTimeValidationException() : base("Requested time must be between 0001-01-01T01:00:00Z and 9999-12-31T22:59:59.9999999Z.") { }
}
