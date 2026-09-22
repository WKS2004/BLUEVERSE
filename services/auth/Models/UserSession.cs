namespace Blueverse.Auth.Models;

public class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // This is an opaque server-issued installation identifier. Legacy clients
    // may still provide their old value while they migrate to DeviceKey.
    public string DeviceId { get; set; } = string.Empty;
    public DeviceInstallation? DeviceInstallation { get; set; }
    public int SessionVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool RememberMe { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
