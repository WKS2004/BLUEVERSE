using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class RefreshTokenRequestDto
{
    /// <summary>
    /// Native clients send the refresh token here. Cookie clients leave it empty.
    /// </summary>
    public string? RefreshToken { get; set; }

    [MaxLength(128)]
    public string? DeviceId { get; set; }

    [MaxLength(256)]
    public string? DeviceKey { get; set; }

    /// <summary>
    /// Optional browser account selection. Cookie clients use it to activate a
    /// previously signed-in account without replacing its protected cookie set.
    /// Native clients continue to send their refresh token directly.
    /// </summary>
    public Guid? AccountId { get; set; }

    public bool UseCookies { get; set; }
}
