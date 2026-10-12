using System.ComponentModel.DataAnnotations;

namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record DestinationDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? Region,
    double Latitude,
    double Longitude,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateDestinationRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [StringLength(200)] string? Slug,
    [StringLength(2000)] string? Description,
    [StringLength(100)] string? Region,
    [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude);

public sealed record UpdateDestinationRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [StringLength(200)] string? Slug,
    [StringLength(2000)] string? Description,
    [StringLength(100)] string? Region,
    [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude);

public sealed record UpdatePublicationRequest(
    [Required] string Status);

public sealed record PublicationEvaluationResponse(
    Guid TargetId,
    string TargetType,
    string CurrentStatus,
    string RequestedStatus,
    bool CanTransition,
    IReadOnlyList<string> Reasons);
