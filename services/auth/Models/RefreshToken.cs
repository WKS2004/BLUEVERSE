namespace Blueverse.Auth.Models;

/// <summary>
/// A one-time refresh-token record. Only the hash is persisted.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserSessionId { get; set; }
    public UserSession? UserSession { get; set; }
    public Guid? UserSessionLogId { get; set; }
    public UserSessionLog? UserSessionLog { get; set; }

    public Guid FamilyId { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? RevocationReason { get; set; }
}
