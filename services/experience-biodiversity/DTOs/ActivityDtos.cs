using System.ComponentModel.DataAnnotations;

namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record ActivityDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateActivityRequest(
    [Required, StringLength(50, MinimumLength = 2)] string Code,
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [StringLength(1000)] string? Description,
    [StringLength(100)] string? Category);

public sealed record UpdateActivityRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [StringLength(1000)] string? Description,
    [StringLength(100)] string? Category);
