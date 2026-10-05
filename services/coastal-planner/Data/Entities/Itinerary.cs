using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blueverse.CoastalPlanner.Data.Entities;

[Table("itineraries", Schema = "coastal_planner")]
public class Itinerary
{
    [Key]
    public Guid ItineraryId { get; set; }

    [Required]
    public Guid OwnerUserId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    [MaxLength(100)] public string TimeZone { get; set; } = "Asia/Colombo";

    public int ConcurrencyVersion { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ItineraryItem> Items { get; set; } = new();
}
