using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/nearby")]
public sealed class NearbyController : ControllerBase
{
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly ILogger<NearbyController> _logger;

    private readonly IMapProviderService _mapProviderService;

    public NearbyController(
        ExperienceBiodiversityDbContext dbContext,
        IMapProviderService mapProviderService,
        ILogger<NearbyController> logger)
    {
        _dbContext = dbContext;
        _mapProviderService = mapProviderService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNearby(
        [FromQuery] string? q,
        [FromQuery] string? location,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double radiusMeters = 50000,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        double lat;
        double lon;
        string? resolvedLocationName = null;

        var locationKeyword = !string.IsNullOrWhiteSpace(q) ? q.Trim() : (!string.IsNullOrWhiteSpace(location) ? location.Trim() : null);

        if (!string.IsNullOrWhiteSpace(locationKeyword))
        {
            // 1. Try matching database destinations first by name or region
            var keywordLower = locationKeyword.ToLower();
            var matchedDestination = await _dbContext.Destinations
                .AsNoTracking()
                .Where(d => d.Status == PublicationStatus.Published &&
                            (d.Name.ToLower().Contains(keywordLower) ||
                             (d.Region != null && d.Region.ToLower().Contains(keywordLower))))
                .OrderBy(d => d.Name.ToLower() == keywordLower ? 0 :
                              (d.Name.ToLower().StartsWith(keywordLower) ? 1 : 2))
                .FirstOrDefaultAsync(cancellationToken);

            if (matchedDestination != null)
            {
                lat = matchedDestination.Latitude;
                lon = matchedDestination.Longitude;
                resolvedLocationName = matchedDestination.Name;
            }
            else
            {
                // 2. Try resolving via map provider service (Photon geocoding)
                var searchResult = await _mapProviderService.SearchPlacesAsync(locationKeyword, cancellationToken);
                var firstPlace = searchResult.Results.FirstOrDefault();
                if (firstPlace != null)
                {
                    lat = firstPlace.Latitude;
                    lon = firstPlace.Longitude;
                    resolvedLocationName = firstPlace.DisplayName;
                }
                else
                {
                    return NotFound(new
                    {
                        type = "https://tools.ietf.org/html/rfc7807",
                        title = "Location Not Found",
                        status = 404,
                        detail = $"No coastal location or destination matching '{locationKeyword}' could be found."
                    });
                }
            }
        }
        else if (latitude.HasValue && longitude.HasValue)
        {
            lat = latitude.Value;
            lon = longitude.Value;
        }
        else
        {
            return BadRequest(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Missing Location Coordinates or Search Term",
                status = 400,
                detail = "Either a location search query ('q') or both 'latitude' and 'longitude' coordinates are required."
            });
        }

        if (lat < -90 || lat > 90)
        {
            return BadRequest(new { type = "https://tools.ietf.org/html/rfc7807", title = "Invalid Latitude", status = 400, detail = "Latitude must be between -90 and 90 degrees." });
        }

        if (lon < -180 || lon > 180)
        {
            return BadRequest(new { type = "https://tools.ietf.org/html/rfc7807", title = "Invalid Longitude", status = 400, detail = "Longitude must be between -180 and 180 degrees." });
        }

        if (radiusMeters <= 0 || radiusMeters > 500000)
        {
            radiusMeters = 50000;
        }

        if (limit < 1 || limit > 50)
        {
            limit = 10;
        }

        // Fetch published destinations and active offerings count
        var publishedDestinations = await _dbContext.Destinations
            .AsNoTracking()
            .Where(d => d.Status == PublicationStatus.Published)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Slug,
                d.Description,
                d.Region,
                d.Latitude,
                d.Longitude,
                ActiveOfferingsCount = d.Offerings.Count(o => o.Status == PublicationStatus.Published)
            })
            .ToListAsync(cancellationToken);

        var nearby = publishedDestinations
            .Select(d =>
            {
                var dist = CalculateHaversineDistanceMeters(lat, lon, d.Latitude, d.Longitude);
                return new
                {
                    Destination = d,
                    DistanceMeters = dist
                };
            })
            .Where(x => x.DistanceMeters <= radiusMeters)
            .OrderBy(x => x.DistanceMeters)
            .Take(limit)
            .Select(x => new NearbyDestinationDto(
                DestinationId: x.Destination.Id,
                Name: x.Destination.Name,
                Slug: x.Destination.Slug,
                Description: x.Destination.Description,
                Region: x.Destination.Region,
                Latitude: x.Destination.Latitude,
                Longitude: x.Destination.Longitude,
                DistanceMeters: Math.Round(x.DistanceMeters, 1),
                ActiveOfferingsCount: x.Destination.ActiveOfferingsCount))
            .ToList();

        return Ok(new
        {
            query = new
            {
                location = locationKeyword,
                resolvedLocation = resolvedLocationName,
                latitude = lat,
                longitude = lon,
                radiusMeters,
                limit
            },
            count = nearby.Count,
            results = nearby
        });
    }

    private static double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var rLat1 = lat1 * Math.PI / 180.0;
        var rLat2 = lat2 * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(rLat1) * Math.Cos(rLat2) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return earthRadiusMeters * c;
    }
}
