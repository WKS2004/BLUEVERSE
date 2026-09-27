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

/// <summary>Deterministic suitability evaluation.</summary>
public interface ISuitabilityService
{
    Task<SuitabilityResultDto> EvaluateAsync(EvaluateSuitabilityDto request, CancellationToken cancellationToken);
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
