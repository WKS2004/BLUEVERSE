using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/destinations/{id:guid}/biodiversity")]
public sealed class BiodiversityController : ControllerBase
{
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly IBiodiversityConsumerService _biodiversityConsumerService;
    private readonly ILogger<BiodiversityController> _logger;

    public BiodiversityController(
        ExperienceBiodiversityDbContext dbContext,
        IBiodiversityConsumerService biodiversityConsumerService,
        ILogger<BiodiversityController> logger)
    {
        _dbContext = dbContext;
        _biodiversityConsumerService = biodiversityConsumerService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetDestinationBiodiversity(Guid id, CancellationToken cancellationToken = default)
    {
        var destination = await _dbContext.Destinations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (destination == null)
        {
            return NotFound(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Destination Not Found",
                status = 404,
                detail = $"Destination with ID {id} does not exist."
            });
        }

        var result = await _biodiversityConsumerService.GetBiodiversityContextAsync(
            destination.Id,
            destination.Name,
            destination.Latitude,
            destination.Longitude,
            cancellationToken);

        return Ok(result);
    }
}
