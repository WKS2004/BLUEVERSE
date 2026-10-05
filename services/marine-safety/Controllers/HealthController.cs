using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        var schemaCurrent = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            dbConnected = await _dbContext.Database.CanConnectAsync(timeout.Token);
            schemaCurrent = dbConnected && (!_dbContext.Database.IsRelational() ||
                !(await _dbContext.Database.GetPendingMigrationsAsync(timeout.Token)).Any());
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            dbConnected = false;
            schemaCurrent = false;
        }
        catch (Exception)
        {
            dbConnected = false;
            schemaCurrent = false;
        }

        var ready = dbConnected && schemaCurrent;
        var statusCode = ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return StatusCode(statusCode, new
        {
            service = "marine-safety",
            status = ready ? "healthy" : "unhealthy",
            database = dbConnected ? "connected" : "unavailable",
            schema = !dbConnected ? "unavailable" : schemaCurrent ? "current" : "pending"
        });
    }
}
