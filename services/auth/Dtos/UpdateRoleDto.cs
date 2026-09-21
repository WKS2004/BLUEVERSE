using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class UpdateRoleDto
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;
}
