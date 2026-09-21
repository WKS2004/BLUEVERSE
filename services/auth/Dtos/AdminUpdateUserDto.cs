using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class AdminUpdateUserDto
{
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [MinLength(8, ErrorMessage = "New password must be at least 8 characters if provided.")]
    public string? NewPassword { get; set; }
}
