namespace Blueverse.Auth.Models;

/// <summary>
/// Immutable lifecycle record for a session that is no longer active.
/// </summary>
public class UserSessionLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string DeviceId { get; set; } = string.Empty;
    public DeviceInstallation? DeviceInstallation { get; set; }
    public int SessionVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool RememberMe { get; set; }
    public DateTime EndedAt { get; set; }
    public string EndReason { get; set; } = string.Empty;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
