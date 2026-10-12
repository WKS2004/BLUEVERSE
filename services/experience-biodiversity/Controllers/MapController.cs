using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/experiences/map")]
public sealed class MapController : ControllerBase
{
    private readonly IMapProviderService _mapProviderService;
    private readonly ILogger<MapController> _logger;

    public MapController(
        IMapProviderService mapProviderService,
        ILogger<MapController> logger)
    {
        _mapProviderService = mapProviderService;
        _logger = logger;
    }

    [HttpGet("config")]
    public IActionResult GetConfiguration()
    {
        var config = _mapProviderService.GetMapConfiguration();
        return Ok(config);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchPlaces(
        [FromQuery] string? q,
        [FromQuery] string? query,
        CancellationToken cancellationToken = default)
    {
        var searchTerm = !string.IsNullOrWhiteSpace(q) ? q : query;
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Trim().Length < 2)
        {
            return BadRequest(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Invalid Query",
                status = 400,
                detail = "Search query parameter 'q' must be at least 2 characters."
            });
        }

        var results = await _mapProviderService.SearchPlacesAsync(searchTerm, cancellationToken);
        return Ok(results);
    }
}
