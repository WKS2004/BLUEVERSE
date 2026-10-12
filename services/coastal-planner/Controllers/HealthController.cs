using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.CoastalPlanner.Data;

namespace Blueverse.CoastalPlanner.Controllers;

[ApiController]
[Route("api/planner/health")]
public sealed class HealthController : ControllerBase
{
    private readonly CoastalPlannerDbContext _dbContext;

    public HealthController(CoastalPlannerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var dbConnected = false;
        var migrationsPending = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            dbConnected = await _dbContext.Database.CanConnectAsync(timeout.Token);
            if (dbConnected && _dbContext.Database.IsRelational())
            {
                migrationsPending = (await _dbContext.Database.GetPendingMigrationsAsync(timeout.Token)).Any();
            }
            else if (dbConnected)
            {
                migrationsPending = false;
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            dbConnected = false;
        }
        catch (Exception)
        {
            dbConnected = false;
        }

        var ready = dbConnected && !migrationsPending;
        var statusCode = ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return StatusCode(statusCode, new
        {
            service = "coastal-planner",
            status = ready ? "healthy" : "unhealthy",
            database = dbConnected ? "connected" : "unavailable",
            migrations = !dbConnected ? "unknown" : migrationsPending ? "pending" : "applied",
            peerServices = "not_required_for_readiness"
        });
    }
}
