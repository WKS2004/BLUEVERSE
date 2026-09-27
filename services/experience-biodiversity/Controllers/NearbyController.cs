using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/nearby")]
public sealed class NearbyController : ControllerBase
{
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly ILogger<NearbyController> _logger;

    public NearbyController(
        ExperienceBiodiversityDbContext dbContext,
        ILogger<NearbyController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNearby(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double radiusMeters = 50000,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return BadRequest(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Missing Location Coordinates",
                status = 400,
                detail = "Both 'latitude' and 'longitude' query parameters are required for nearby discovery."
            });
        }

        var lat = latitude.Value;
        var lon = longitude.Value;

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
            query = new { latitude = lat, longitude = lon, radiusMeters, limit },
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
