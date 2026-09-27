namespace Blueverse.ExperienceBiodiversity.Models;

public sealed class Schedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferingId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Colombo";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Offering Offering { get; set; } = null!;
}
