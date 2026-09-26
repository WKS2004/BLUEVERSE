using System.ComponentModel.DataAnnotations;

namespace Blueverse.Auth.Dtos;

public class LogoutAccountRequestDto
{
    [Required]
    public Guid UserId { get; set; }
}
