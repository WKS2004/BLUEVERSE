using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blueverse.CoastalPlanner.Data.Entities;

[Table("itinerary_items", Schema = "coastal_planner")]
public class ItineraryItem
{
    [Key]
    public Guid ItemId { get; set; }

    [Required]
    public Guid ItineraryId { get; set; }

    public Itinerary? Itinerary { get; set; }

    [Required]
    public Guid DestinationId { get; set; }

    [Required]
    public Guid ActivityId { get; set; }

    public Guid? OfferingId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    public DateTime ScheduledStartUtc { get; set; }
    public DateTime ScheduledEndUtc { get; set; }

    [MaxLength(64)]
    public string LastSuitabilityStatus { get; set; } = "UNKNOWN";

    [MaxLength(64)]
    public string LastAvailabilityStatus { get; set; } = "UNKNOWN";

    [MaxLength(64)]
    public string LastOperationalStatus { get; set; } = "OPEN";

    [MaxLength(500)]
    public string? AdvisoryNote { get; set; }
}
