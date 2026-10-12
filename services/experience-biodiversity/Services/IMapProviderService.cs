using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;

namespace Blueverse.ExperienceBiodiversity.Services;

public interface IMapProviderService
{
    MapConfigDto GetMapConfiguration();
    Task<MapSearchResponseDto> SearchPlacesAsync(string query, CancellationToken cancellationToken = default);
}

public sealed class MapProviderService : IMapProviderService
{
    private readonly HttpClient _httpClient;
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly ILogger<MapProviderService> _logger;

    public MapProviderService(
        HttpClient httpClient,
        ExperienceBiodiversityDbContext dbContext,
        ILogger<MapProviderService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public MapConfigDto GetMapConfiguration()
    {
        return new MapConfigDto(
            Provider: "OpenFreeMap",
            TileServiceType: "VectorTiles",
            VectorTileUrl: "https://tiles.openfreemap.org/styles/{style}",
            AvailableStyles: new Dictionary<string, string>
            {
                ["liberty"] = "https://tiles.openfreemap.org/styles/liberty",
                ["bright"] = "https://tiles.openfreemap.org/styles/bright",
                ["positron"] = "https://tiles.openfreemap.org/styles/positron",
                ["dark"] = "https://tiles.openfreemap.org/styles/dark",
                ["fiord"] = "https://tiles.openfreemap.org/styles/fiord"
            },
            DefaultStyle: "liberty",
            Attribution: "© OpenFreeMap contributors, © OpenStreetMap contributors",
            DocumentationUrl: "https://openfreemap.org/");
    }

    public async Task<MapSearchResponseDto> SearchPlacesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return new MapSearchResponseDto(query ?? "", Array.Empty<MapSearchResultItemDto>(), "Validation", false, DateTimeOffset.UtcNow);
        }

        var trimmed = query.Trim();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            // Use Photon geocoding API (OpenStreetMap data, multilingual, keyless)
            var photonUrl = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(trimmed)}&limit=5";
            using var request = new HttpRequestMessage(HttpMethod.Get, photonUrl);
            request.Headers.Add("User-Agent", "BLUEVERSE-Coastal-Platform/1.0");

            var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                using var doc = JsonDocument.Parse(content);
                var features = doc.RootElement.GetProperty("features");

                var items = new List<MapSearchResultItemDto>();
                foreach (var feature in features.EnumerateArray())
                {
                    var geom = feature.GetProperty("geometry");
                    var coords = geom.GetProperty("coordinates");
                    var lon = coords[0].GetDouble();
                    var lat = coords[1].GetDouble();

                    var props = feature.GetProperty("properties");
                    var name = props.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var country = props.TryGetProperty("country", out var c) ? c.GetString() : null;
                    var state = props.TryGetProperty("state", out var s) ? s.GetString() : null;
                    var city = props.TryGetProperty("city", out var ci) ? ci.GetString() : null;
                    var type = props.TryGetProperty("type", out var t) ? t.GetString() : null;

                    var display = string.Join(", ", new[] { name, city, state, country }.Where(x => !string.IsNullOrWhiteSpace(x)));

                    items.Add(new MapSearchResultItemDto(
                        DisplayName: string.IsNullOrWhiteSpace(display) ? name : display,
                        Latitude: lat,
                        Longitude: lon,
                        Type: type,
                        Category: "place",
                        Region: state ?? city,
                        Country: country));
                }

                if (items.Count > 0)
                {
                    return new MapSearchResponseDto(trimmed, items, "Photon-OSM", false, DateTimeOffset.UtcNow);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External place search provider timed out or failed. Falling back to local database search.");
        }

        var lowerTrimmed = trimmed.ToLower();
        var localMatches = await _dbContext.Destinations
            .AsNoTracking()
            .Where(d => d.Name.ToLower().Contains(lowerTrimmed) ||
                        (d.Region != null && d.Region.ToLower().Contains(lowerTrimmed)))
            .Take(5)
            .Select(d => new MapSearchResultItemDto(
                $"{d.Name}, {d.Region ?? "Sri Lanka"}",
                d.Latitude,
                d.Longitude,
                "coastal_destination",
                "destination",
                d.Region,
                "Sri Lanka"))
            .ToListAsync(cancellationToken);

        if (localMatches.Count > 0)
        {
            return new MapSearchResponseDto(trimmed, localMatches, "Local-Fallback", true, DateTimeOffset.UtcNow);
        }

        // Curated Sri Lankan coastal towns and hotspots fallback
        var curatedPlaces = new (string Name, string Region, double Lat, double Lon)[]
        {
            ("Galle", "Southern Province", 6.0535, 80.2210),
            ("Galle Fort", "Southern Province", 6.0274, 80.2170),
            ("Colombo", "Western Province", 6.9271, 79.8612),
            ("Trincomalee", "Eastern Province", 8.5874, 81.2152),
            ("Bentota", "Southern Province", 6.4259, 79.9965),
            ("Tangalle", "Southern Province", 6.0242, 80.7941),
            ("Negombo", "Western Province", 7.2008, 79.8736),
            ("Jaffna", "Northern Province", 9.6615, 80.0255),
            ("Matara", "Southern Province", 5.9549, 80.5550),
            ("Hambantota", "Southern Province", 6.1246, 81.1185),
            ("Weligama", "Southern Province", 5.9722, 80.4289),
            ("Unawatuna", "Southern Province", 6.0104, 80.2483),
            ("Beruwala", "Western Province", 6.4788, 79.9828),
            ("Kalutara", "Western Province", 6.5854, 79.9607),
            ("Mount Lavinia", "Western Province", 6.8378, 79.8647),
            ("Pasikuda", "Eastern Province", 7.9255, 81.5645),
            ("Batticaloa", "Eastern Province", 7.7102, 81.6924),
            ("Mannar", "Northern Province", 8.9810, 79.9044)
        };

        var matchedCurated = curatedPlaces
            .Where(p => p.Name.ToLower().Contains(lowerTrimmed) || p.Region.ToLower().Contains(lowerTrimmed))
            .Take(5)
            .Select(p => new MapSearchResultItemDto(
                $"{p.Name}, {p.Region}, Sri Lanka",
                p.Lat,
                p.Lon,
                "coastal_city",
                "place",
                p.Region,
                "Sri Lanka"))
            .ToList();

        return new MapSearchResponseDto(trimmed, matchedCurated, "Curated-Coastal-Fallback", true, DateTimeOffset.UtcNow);
    }
}
