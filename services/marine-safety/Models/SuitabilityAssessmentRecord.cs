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

    /// <summary>Version of the safety profile applied, for later interpretability.</summary>
    public int ProfileVersion { get; set; }

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    /// <summary>Requested assessment time (UTC) — the forecast time the evidence applies to.</summary>
    public DateTime RequestedTime { get; set; }

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
    public DateTime CreatedAt { get; set; }
}
