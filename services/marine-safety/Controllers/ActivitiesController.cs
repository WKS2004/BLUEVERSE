using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.MarineSafety.Authorization;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Services;

namespace Blueverse.MarineSafety.Controllers;

/// <summary>
/// Marine activity reference-table surface. Reads require the marine
/// profile-read grant; create/update/deactivate require the manage grant in
/// addition, exactly like the safety-profile surface. Deletion deactivates —
/// activity rows are never hard-deleted because assessment history keeps a
/// restrict reference to its activity.
/// </summary>
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
    public async Task<IActionResult> GetActivities(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        return Ok(await _activities.GetActivitiesAsync(isActive, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MarineActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActivity(Guid id, CancellationToken cancellationToken)
    {
        var activity = await _activities.GetActivityAsync(id, cancellationToken);
        return activity is null ? NotFound() : Ok(activity);
    }

    [HasPermission("marine.profile.manage")]
    [HttpPost]
    [ProducesResponseType(typeof(MarineActivityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateActivity([FromBody] CreateActivityDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var activity = await _activities.CreateActivityAsync(dto, cancellationToken);
            return CreatedAtAction(
                nameof(GetActivity),
                new { id = activity.Id },
                activity);
        }
        catch (ActivityNameConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Activity Name Already In Use",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (ActivityValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid activity",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HasPermission("marine.profile.manage")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MarineActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateActivity(Guid id, [FromBody] UpdateActivityDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var activity = await _activities.UpdateActivityAsync(id, dto, cancellationToken);
            return activity is null ? NotFound() : Ok(activity);
        }
        catch (ActivityNameConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Activity Name Already In Use",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (ActivityValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid activity",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HasPermission("marine.profile.manage")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateActivity(Guid id, CancellationToken cancellationToken)
    {
        var deactivated = await _activities.DeactivateActivityAsync(id, cancellationToken);
        return deactivated ? NoContent() : NotFound();
    }
}
