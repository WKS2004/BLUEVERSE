using Microsoft.AspNetCore.Mvc;
using Blueverse.Auth.Data;

namespace Blueverse.Auth.Controllers;

[ApiController]
[Route("api/auth/health")]
public sealed class HealthController : ControllerBase
{
    private readonly AuthDbContext _dbContext;

    public HealthController(AuthDbContext dbContext)
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
            service = "auth",
            status = dbConnected ? "healthy" : "unhealthy",
            database = dbConnected ? "connected" : "unavailable"
        });
    }
}
