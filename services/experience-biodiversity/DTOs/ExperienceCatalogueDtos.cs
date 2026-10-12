namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record ExperienceCatalogueItemDto(
    Guid DestinationId,
    string DestinationName,
    string? Region,
    double Latitude,
    double Longitude,
    Guid ActivityId,
    string ActivityName,
    Guid OfferingId,
    string Title,
    string AvailabilityStatus,
    string PublicationState,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil,
    IReadOnlyList<string>? ExperienceLevels,
    DateTime? CheckedAt,
    string? TimeZone);

public sealed record ExperienceTargetAvailabilityDto(
    string TargetType,
    Guid TargetId,
    string AvailabilityStatus,
    string TargetVersion,
    DateTimeOffset EvaluatedAt,
    DateTimeOffset? ValidUntil);
