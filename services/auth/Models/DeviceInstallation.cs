namespace Blueverse.Auth.Models;

/// <summary>
/// A server-issued installation identifier and its proof-of-possession key.
/// Legacy rows may omit the key while older clients migrate to the new flow.
/// </summary>
public class DeviceInstallation
{
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceKeyHash { get; set; }
    public bool IsLegacy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public ICollection<UserSessionLog> SessionLogs { get; set; } = new List<UserSessionLog>();
}
