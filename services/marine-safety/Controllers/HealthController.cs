using Microsoft.AspNetCore.Mvc;
using Blueverse.MarineSafety.Data;

namespace Blueverse.MarineSafety.Controllers;

/// <summary>
/// Liveness and database-readiness probe, mirroring the Auth service's
/// health contract. Anonymous by design: dependency failures must be
/// observable without credentials, and the endpoint reports status only.
/// </summary>
[ApiController]
[Route("api/marine/health")]
public sealed class HealthController : ControllerBase
{
    private readonly MarineSafetyDbContext _dbContext;

    public HealthController(MarineSafetyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

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

        var statusCode = dbConnected ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return StatusCode(statusCode, new
        {
            service = "marine-safety",
            status = dbConnected ? "healthy" : "unhealthy",
            database = dbConnected ? "connected" : "unavailable"
        });
    }
}
