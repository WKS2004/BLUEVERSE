namespace Blueverse.MarineSafety.Models;

/// <summary>Non-authoritative projection of Member 1's published activity IDs.
/// Legacy rows retain historic references; live availability is always checked
/// with the catalogue. No local activity CRUD is exposed.</summary>
public class MarineActivity
{
    public Guid Id { get; set; }

    /// <summary>Captured human-readable label; distinct canonical IDs may share a name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Coarse activity type used for grouping and seeding (e.g. Surfing, Snorkeling, Diving, BoatTour).</summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>Inactive activities cannot receive new safety profiles or evaluations.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SafetyProfile> SafetyProfiles { get; set; } = new List<SafetyProfile>();
}
