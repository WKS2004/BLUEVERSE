using System.ComponentModel.DataAnnotations;

namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record FavouriteDto(
    Guid Id,
    Guid UserId,
    string TargetType,
    Guid TargetId,
    string? TargetTitle,
    string? TargetStatus,
    DateTimeOffset CreatedAt);

public sealed record CreateFavouriteRequest(
    [Required] string TargetType,
    [Required] Guid TargetId);
