namespace Blueverse.Auth.Dtos;

public class AuthResponseDto
{
    public string? Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceKey { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime SessionExpiresAt { get; set; }
    public bool RememberMe { get; set; }
    public UserDto User { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}
