using System.ComponentModel.DataAnnotations;

namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record OfferingDto(
    Guid Id,
    Guid DestinationId,
    string DestinationName,
    Guid ActivityId,
    string ActivityName,
    string ActivityCode,
    string Title,
    string? Description,
    decimal? Price,
    string? Currency,
    int? DurationMinutes,
    int? MaxCapacity,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateOfferingRequest(
    [Required] Guid DestinationId,
    [Required] Guid ActivityId,
    [Required, StringLength(200, MinimumLength = 2)] string Title,
    [StringLength(2000)] string? Description,
    [Range(0, 1000000)] decimal? Price,
    [StringLength(10)] string? Currency,
    [Range(1, 1440)] int? DurationMinutes,
    [Range(1, 1000)] int? MaxCapacity);

public sealed record UpdateOfferingRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Title,
    [StringLength(2000)] string? Description,
    [Range(0, 1000000)] decimal? Price,
    [StringLength(10)] string? Currency,
    [Range(1, 1440)] int? DurationMinutes,
    [Range(1, 1000)] int? MaxCapacity);
