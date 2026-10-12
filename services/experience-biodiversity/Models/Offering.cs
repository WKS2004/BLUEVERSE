namespace Blueverse.ExperienceBiodiversity.Models;

public sealed class Offering
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DestinationId { get; set; }
    public Guid ActivityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public int? DurationMinutes { get; set; }
    public int? MaxCapacity { get; set; }
    public string Status { get; set; } = PublicationStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Destination Destination { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
