namespace Blueverse.MarineSafety.Models;

/// <summary>
/// Local reference row for a BLUEVERSE coastal activity that a safety profile
/// belongs to. The v1 relationship map assigns canonical activity identity to
/// Member 1's experience catalogue; that service does not exist yet, so this
/// service keeps a minimal locally-owned reference table until the G00-agreed
/// canonical IDs are available. Rows seeded here must track the agreed
/// taxonomy and are replaced by Member 1's canonical records when that
/// component merges — this table deliberately does not add catalogue features
/// (no descriptions, no publication state, no scheduling).
/// </summary>
public class MarineActivity
{
    public Guid Id { get; set; }

    /// <summary>Stable, human-readable activity name, unique within the reference table.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Coarse activity type used for grouping and seeding (e.g. Surfing, Snorkeling, Diving, BoatTour).</summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>Inactive activities cannot receive new safety profiles or evaluations.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SafetyProfile> SafetyProfiles { get; set; } = new List<SafetyProfile>();
}
