using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class AssignRolesDto
{
    [Required]
    public List<string> RoleNames { get; set; } = new();
}
