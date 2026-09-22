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

    public bool UseCookies { get; set; }
}
