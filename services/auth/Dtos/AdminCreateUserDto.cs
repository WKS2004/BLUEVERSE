using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class AdminCreateUserDto
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

    public List<string> RoleNames { get; set; } = new();
}
