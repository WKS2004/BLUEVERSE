using System.ComponentModel.DataAnnotations;

namespace Blueverse.MarineSafety.Dtos;

// Validation attributes sit on the record primary-constructor parameters:
// ASP.NET Core MVC associates record validation metadata with the parameters,
// not the generated properties.
public sealed record CreateSafetyProfileDto(
    [Required] Guid? ActivityId,
    [Required, Range(0.01, 1000)] decimal MaxWindSpeed,
    [Required, Range(0.01, 50)] decimal MaxWaveHeight,
    [Required, Range(0.01, 50)] decimal MaxSwellHeight,
    [Required, StringLength(512, MinimumLength = 3)] string? WindCriteriaSource,
    [Required, StringLength(2000, MinimumLength = 10)] string? WindCriteriaRationale,
    [Required, StringLength(512, MinimumLength = 3)] string? WaveCriteriaSource,
    [Required, StringLength(2000, MinimumLength = 10)] string? WaveCriteriaRationale,
    [Required, StringLength(512, MinimumLength = 3)] string? SwellCriteriaSource,
    [Required, StringLength(2000, MinimumLength = 10)] string? SwellCriteriaRationale,
    [Range(0.01, 1000)] decimal? CautionWindSpeed = null,
    [Range(0.01, 50)] decimal? CautionWaveHeight = null,
    [Range(0.01, 50)] decimal? CautionSwellHeight = null);

public sealed record UpdateSafetyProfileDto(
    [Required, Range(0.01, 1000)] decimal MaxWindSpeed,
    [Required, Range(0.01, 50)] decimal MaxWaveHeight,
    [Required, Range(0.01, 50)] decimal MaxSwellHeight,
    [Required, StringLength(512, MinimumLength = 3)] string? WindCriteriaSource,
    [Required, StringLength(2000, MinimumLength = 10)] string? WindCriteriaRationale,
    [Required, StringLength(512, MinimumLength = 3)] string? WaveCriteriaSource,
    [Required, StringLength(2000, MinimumLength = 10)] string? WaveCriteriaRationale,
    [Required, StringLength(512, MinimumLength = 3)] string? SwellCriteriaSource,
    [Required, StringLength(2000, MinimumLength = 10)] string? SwellCriteriaRationale,
    [Range(0.01, 1000)] decimal? CautionWindSpeed = null,
    [Range(0.01, 50)] decimal? CautionWaveHeight = null,
    [Range(0.01, 50)] decimal? CautionSwellHeight = null);

public sealed record SafetyProfileDto(
    Guid Id,
    Guid ActivityId,
    string ActivityName,
    decimal MaxWindSpeed,
    decimal MaxWaveHeight,
    decimal MaxSwellHeight,
    decimal? CautionWindSpeed,
    decimal? CautionWaveHeight,
    decimal? CautionSwellHeight,
    string? WindCriteriaSource,
    string? WindCriteriaRationale,
    string? WaveCriteriaSource,
    string? WaveCriteriaRationale,
    string? SwellCriteriaSource,
    string? SwellCriteriaRationale,
    bool IsActive,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? CreatedByUserId,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo);

public sealed record ConditionSnapshotDto(
    Guid Id,
    decimal Latitude,
    decimal Longitude,
    DateTime ForecastTime,
    DateTime RetrievedAt,
    decimal? WindSpeed,
    decimal? WaveHeight,
    decimal? SwellHeight,
    decimal? Rain,
    int? WeatherCode,
    string Source,
    string FreshnessStatus,
    IReadOnlyList<string> MissingFields);

public sealed record EvaluateSuitabilityDto(
    [Required] Guid? ActivityId,
    [Required, Range(-90, 90)] decimal Latitude,
    [Required, Range(-180, 180)] decimal Longitude,
    DateTime? DateTime);

public sealed record SuitabilityResultDto(
    string Status,
    Guid ActivityId,
    string ActivityName,
    LocationDto Location,
    DateTime RequestedTime,
    DateTime ForecastTime,
    DateTime EvaluatedAt,
    ConditionsDto? Conditions,
    string? Source,
    DateTime? RetrievedAt,
    string? Freshness,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> CautionFactors,
    Guid AssessmentId,
    Guid SnapshotId);

public sealed record LocationDto(decimal Latitude, decimal Longitude);

public sealed record ConditionsDto(
    decimal? WindSpeed,
    decimal? WaveHeight,
    decimal? SwellHeight,
    decimal? Rain,
    int? WeatherCode);

/// <summary>Creates one marine activity reference row (locally-owned table).</summary>
public sealed record CreateActivityDto(
    [Required, StringLength(128, MinimumLength = 2)] string? Name,
    [Required, StringLength(64, MinimumLength = 2)] string? ActivityType);

/// <summary>Renames or retypes one marine activity; activity rows are never re-activated through this surface.</summary>
public sealed record UpdateActivityDto(
    [Required, StringLength(128, MinimumLength = 2)] string? Name,
    [Required, StringLength(64, MinimumLength = 2)] string? ActivityType);

/// <summary>One marine activity reference row with its active-state detail.</summary>
public sealed record MarineActivityDto(
    Guid Id,
    string Name,
    string ActivityType,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// One persisted suitability assessment: the request context, the profile
/// version that was applied, the condition evidence and its freshness, and the
/// deterministic result. History rows are immutable evidence (read-only API).
/// </summary>
public sealed record AssessmentHistoryDto(
    Guid Id,
    Guid ActivityId,
    string? ActivityName,
    Guid ConditionSnapshotId,
    Guid SafetyProfileId,
    int ProfileVersion,
    decimal Latitude,
    decimal Longitude,
    DateTime RequestedTime,
    DateTime? ForecastTime,
    DateTime EvaluatedAt,
    string Result,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> CautionFactors,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> ConditionMissingFields,
    ConditionsDto Conditions,
    DateTime? ConditionRetrievedAt,
    IReadOnlyList<SafetyCriterionSnapshotDto> Criteria,
    string EvidenceCompleteness,
    string Source,
    string FreshnessStatus,
    DateTime CreatedAt);

/// <summary>One reviewed activity limit and its cited basis captured at evaluation time.</summary>
public sealed record SafetyCriterionSnapshotDto(
    string Factor,
    decimal? Maximum,
    decimal? Caution,
    string? Source,
    string? Rationale);



