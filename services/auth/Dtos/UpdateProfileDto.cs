using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class UpdateProfileDto
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
}
