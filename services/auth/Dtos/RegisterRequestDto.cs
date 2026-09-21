using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? DeviceId { get; set; }

    [MaxLength(256)]
    public string? DeviceKey { get; set; }

    public bool RememberMe { get; set; }

    public bool UseCookies { get; set; }
}
