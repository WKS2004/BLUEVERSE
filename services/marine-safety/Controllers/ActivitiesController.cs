using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.MarineSafety.Authorization;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Services;

namespace Blueverse.MarineSafety.Controllers;

/// <summary>Read-only published activities owned by Member 1.</summary>
[ApiController]
[Route("api/marine/activities")]
[HasPermission("marine.profile.read")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activities;

    public ActivitiesController(IActivityService activities)
    {
        _activities = activities;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MarineActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DependencyUnavailableProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetActivities(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        return Ok(await _activities.GetActivitiesAsync(isActive, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MarineActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DependencyUnavailableProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetActivity(Guid id, CancellationToken cancellationToken)
    {
        var activity = await _activities.GetActivityAsync(id, cancellationToken);
        return activity is null ? NotFound() : Ok(activity);
    }

}
