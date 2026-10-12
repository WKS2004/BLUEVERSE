using System.ComponentModel.DataAnnotations;

namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record ScheduleDto(
    Guid Id,
    Guid OfferingId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string TimeZoneId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateScheduleRequest(
    [Required] DateTimeOffset StartsAt,
    [Required] DateTimeOffset EndsAt,
    [StringLength(100)] string? TimeZoneId,
    bool IsActive = true);

public sealed record UpdateScheduleRequest(
    [Required] DateTimeOffset StartsAt,
    [Required] DateTimeOffset EndsAt,
    [StringLength(100)] string? TimeZoneId,
    bool IsActive = true);
