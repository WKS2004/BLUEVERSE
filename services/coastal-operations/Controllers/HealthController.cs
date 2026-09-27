using Microsoft.AspNetCore.Mvc;
using Blueverse.CoastalOperations.Data;

namespace Blueverse.CoastalOperations.Controllers;

[ApiController]
[Route("api/operations/health")]
public sealed class HealthController : ControllerBase
{
    private readonly CoastalOperationsDbContext _dbContext;

    public HealthController(CoastalOperationsDbContext dbContext)
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
            service = "coastal-operations",
            status = dbConnected ? "healthy" : "unhealthy",
            database = dbConnected ? "connected" : "unavailable"
        });
    }
}
