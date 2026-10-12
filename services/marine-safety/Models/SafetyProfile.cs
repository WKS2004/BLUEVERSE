namespace Blueverse.MarineSafety.Models;

/// <summary>
/// Deterministic safety configuration for one activity. Every limit is an
/// absolute ceiling in a fixed unit; when a required factor exceeds its limit
/// the suitability result is UNSUITABLE. Optional caution limits define a
/// second, softer ceiling: CAUTION is returned only when the configured
/// caution limits exist (a profile without caution limits yields a strict
/// three-state result: SUITABLE, UNSUITABLE or UNKNOWN).
///
/// Numeric thresholds are configuration data owned by an authorized manager,
/// never invented at runtime by a provider, a client or a future AI component.
/// </summary>
public class SafetyProfile
{
    public Guid Id { get; set; }

    /// <summary>Activity this profile configures. One active profile per activity.</summary>
    public Guid ActivityId { get; set; }
    public MarineActivity? Activity { get; set; }

    /// <summary>Maximum wind speed in km/h. Required.</summary>
    public decimal MaxWindSpeed { get; set; }

    /// <summary>Maximum significant wave height in metres. Required.</summary>
    public decimal MaxWaveHeight { get; set; }

    /// <summary>Maximum swell height in metres. Required.</summary>
    public decimal MaxSwellHeight { get; set; }

    /// <summary>
    /// Optional soft wind ceiling in km/h; when present, wind at or below
    /// <see cref="MaxWindSpeed"/> but above this value yields CAUTION.
    /// </summary>
    public decimal? CautionWindSpeed { get; set; }

    /// <summary>Optional soft wave-height ceiling in metres.</summary>
    public decimal? CautionWaveHeight { get; set; }

    /// <summary>Optional soft swell-height ceiling in metres.</summary>
    public decimal? CautionSwellHeight { get; set; }

    // Threshold provenance is required before a profile can be reviewed.
    // Nullable storage keeps profiles created before the review workflow
    // readable while ensuring they cannot silently become approved.
    public string? WindCriteriaSource { get; set; }
    public string? WindCriteriaRationale { get; set; }
    public string? WaveCriteriaSource { get; set; }
    public string? WaveCriteriaRationale { get; set; }
    public string? SwellCriteriaSource { get; set; }
    public string? SwellCriteriaRationale { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    /// <summary>True while the profile is the applicable configuration for its activity.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Monotonic immutable profile version. Editing creates a new draft row;
    /// reviewed versions remain unchanged for assessment history.
    /// </summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
