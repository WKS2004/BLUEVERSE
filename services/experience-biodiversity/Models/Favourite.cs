namespace Blueverse.ExperienceBiodiversity.Models;

public sealed class Favourite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TargetType { get; set; } = string.Empty; // "Destination", "Activity", "Offering"
    public Guid TargetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
