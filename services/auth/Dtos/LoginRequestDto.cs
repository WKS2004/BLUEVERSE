using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? DeviceId { get; set; }

    [MaxLength(256)]
    public string? DeviceKey { get; set; }

    public bool RememberMe { get; set; }

    /// <summary>
    /// Browser clients use protected cookies; native clients receive token values.
    /// </summary>
    public bool UseCookies { get; set; }
}
