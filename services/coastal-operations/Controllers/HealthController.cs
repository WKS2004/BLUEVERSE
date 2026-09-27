using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Data;

namespace Blueverse.CoastalOperations.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/operations/health")]
public sealed class HealthController : ControllerBase
{
    private readonly CoastalOperationsDbContext _dbContext;
    private readonly ComponentDependencyHealthRegistry _dependencyHealth;

    public HealthController(CoastalOperationsDbContext dbContext, ComponentDependencyHealthRegistry dependencyHealth)
    {
        _dbContext = dbContext;
        _dependencyHealth = dependencyHealth;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
        => await GetReadinessAsync();

    [HttpGet("live")]
    public IActionResult Live() => Ok(new { service = "coastal-operations", status = "live" });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready() => await GetReadinessAsync();

    private async Task<IActionResult> GetReadinessAsync()
    {
        var dbConnected = false;
        var schemaReady = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            dbConnected = await _dbContext.Database.CanConnectAsync(timeout.Token);
            if (dbConnected)
            {
                schemaReady = !(await _dbContext.Database.GetPendingMigrationsAsync(timeout.Token)).Any();
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

        var ready = dbConnected && schemaReady;
        var statusCode = ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return StatusCode(statusCode, new
        {
            service = "coastal-operations",
            status = ready ? "healthy" : "unhealthy",
            database = dbConnected ? "connected" : "unavailable",
            schema = schemaReady ? "ready" : "pending",
            componentDependencies = _dependencyHealth.GetSnapshot()
        });
    }
}
