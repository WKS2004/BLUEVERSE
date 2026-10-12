namespace Blueverse.ExperienceBiodiversity.DTOs;

public sealed record MapConfigDto(
    string Provider,
    string TileServiceType,
    string VectorTileUrl,
    IReadOnlyDictionary<string, string> AvailableStyles,
    string DefaultStyle,
    string Attribution,
    string DocumentationUrl);

public sealed record MapSearchResultItemDto(
    string DisplayName,
    double Latitude,
    double Longitude,
    string? Type,
    string? Category,
    string? Region,
    string? Country);

public sealed record MapSearchResponseDto(
    string Query,
    IReadOnlyList<MapSearchResultItemDto> Results,
    string Source,
    bool Fallback,
    DateTimeOffset RetrievedAt);
