namespace Blueverse.Auth.Dtos;

public class SessionDto
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool RememberMe { get; set; }
    public bool IsCurrent { get; set; }
}
