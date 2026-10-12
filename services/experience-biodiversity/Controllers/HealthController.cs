using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.ExperienceBiodiversity.Data;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/health")]
public sealed class HealthController : ControllerBase
{
    private readonly ExperienceBiodiversityDbContext _dbContext;

    public HealthController(ExperienceBiodiversityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var dbConnected = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            dbConnected = await _dbContext.Database.CanConnectAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            dbConnected = false;
        }
        catch (Exception)
        {
            dbConnected = false;
        }

        var statusCode = dbConnected
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return StatusCode(statusCode, new
        {
            service = "experience-biodiversity",
            status = dbConnected ? "healthy" : "unhealthy",
            database = dbConnected ? "connected" : "unavailable"
        });
    }
}
