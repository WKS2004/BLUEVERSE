using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class AssignPermissionsDto
{
    [Required]
    public List<string> PermissionCodes { get; set; } = new();
}
