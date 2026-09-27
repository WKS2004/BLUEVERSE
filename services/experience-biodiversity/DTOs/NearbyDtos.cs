namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record NearbyDestinationDto(
    Guid DestinationId,
    string Name,
    string Slug,
    string? Description,
    string? Region,
    double Latitude,
    double Longitude,
    double DistanceMeters,
    int ActiveOfferingsCount);
