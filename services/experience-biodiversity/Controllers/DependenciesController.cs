using Microsoft.AspNetCore.Mvc;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/dependencies")]
public sealed class DependenciesController : ControllerBase
{
    private readonly IDependenciesDiagnosticsService _diagnosticsService;
    private readonly ILogger<DependenciesController> _logger;

    public DependenciesController(
        IDependenciesDiagnosticsService diagnosticsService,
        ILogger<DependenciesController> logger)
    {
        _diagnosticsService = diagnosticsService;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetDependenciesStatus(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Inspecting status of external microservice dependencies for experience-biodiversity.");
        var status = await _diagnosticsService.CheckDependenciesAsync(cancellationToken);
        return Ok(status);
    }
}
