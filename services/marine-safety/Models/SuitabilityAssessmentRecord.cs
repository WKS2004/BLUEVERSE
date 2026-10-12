namespace Blueverse.MarineSafety.Models;

/// <summary>
/// Deterministic suitability classification produced by the backend rule
/// engine. CAUTION is reserved for profiles that configure explicit caution
/// limits; a strict three-state profile never yields it.
/// </summary>
public static class SuitabilityResults
{
    public const string Suitable = "SUITABLE";
    public const string Caution = "CAUTION";
    public const string Unsuitable = "UNSUITABLE";
    public const string Unknown = "UNKNOWN";
}

/// <summary>
/// Persisted history of one deterministic suitability evaluation: the request
/// context, the profile version that was applied, the condition evidence and
/// its freshness, and the structured result. Retained so a past result stays
/// explainable after the profile or source data changes.
/// </summary>
public class SuitabilityAssessmentRecord
{
    public Guid Id { get; set; }

    public Guid ActivityId { get; set; }
    public MarineActivity? Activity { get; set; }

    /// <summary>Activity label captured when the assessment ran.</summary>
    public string? ActivityName { get; set; }

    /// <summary>Version of the safety profile applied, for later interpretability.</summary>
    public int ProfileVersion { get; set; }

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    /// <summary>Requested assessment time (UTC) — the forecast time the evidence applies to.</summary>
    public DateTime RequestedTime { get; set; }

    /// <summary>Forecast time represented by the stored condition evidence.</summary>
    public DateTime? ForecastTime { get; set; }

    /// <summary>When the backend performed the evaluation (UTC).</summary>
    public DateTime EvaluatedAt { get; set; }

    /// <summary>One of <see cref="SuitabilityResults"/>.</summary>
    public string Result { get; set; } = SuitabilityResults.Unknown;

    /// <summary>JSON array of rule failures that produced the result (empty for SUITABLE).</summary>
    public string[] Violations { get; set; } = [];

    /// <summary>JSON array of factor names that sat in their caution band (empty when strict).</summary>
    public string[] CautionFactors { get; set; } = [];

    /// <summary>JSON array of expected condition fields that were unavailable.</summary>
    public string[] MissingFields { get; set; } = [];

    public string Source { get; set; } = ConditionSources.OpenMeteo;
    public string FreshnessStatus { get; set; } = FreshnessStatuses.Unavailable;
    public Guid ConditionSnapshotId { get; set; }
    public Guid SafetyProfileId { get; set; }

    // Copy condition evidence and the exact reviewed criteria into the
    // immutable assessment row. The source rows may later be retained or
    // archived independently without making this decision uninterpretable.
    public DateTime? ConditionRetrievedAt { get; set; }
    public decimal? WindSpeed { get; set; }
    public decimal? WaveHeight { get; set; }
    public decimal? SwellHeight { get; set; }
    public decimal? Rain { get; set; }
    public int? WeatherCode { get; set; }
    public string[] ConditionMissingFields { get; set; } = [];
    public decimal? MaxWindSpeed { get; set; }
    public decimal? MaxWaveHeight { get; set; }
    public decimal? MaxSwellHeight { get; set; }
    public decimal? CautionWindSpeed { get; set; }
    public decimal? CautionWaveHeight { get; set; }
    public decimal? CautionSwellHeight { get; set; }
    public string? WindCriteriaSource { get; set; }
    public string? WindCriteriaRationale { get; set; }
    public string? WaveCriteriaSource { get; set; }
    public string? WaveCriteriaRationale { get; set; }
    public string? SwellCriteriaSource { get; set; }
    public string? SwellCriteriaRationale { get; set; }
    public string EvidenceCompleteness { get; set; } = "LEGACY_INCOMPLETE";
    public DateTime CreatedAt { get; set; }
}
