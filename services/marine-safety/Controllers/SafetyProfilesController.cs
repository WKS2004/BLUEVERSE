using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Blueverse.MarineSafety.Authorization;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Services;

namespace Blueverse.MarineSafety.Controllers;

/// <summary>
/// Safety profile management surface. Reads require the marine profile-read
/// grant; create/update/deactivate require the manage grant in addition.
/// Authorization resolves from the caller's current role assignments —
/// never from a hard-coded role name.
/// </summary>
[ApiController]
[Route("api/marine/safety-profiles")]    [HasPermission("marine.profile.read")]
public sealed class SafetyProfilesController : ControllerBase
{
    private readonly ISafetyProfileService _profiles;

    public SafetyProfilesController(ISafetyProfileService profiles)
    {
        _profiles = profiles;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SafetyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfiles(CancellationToken cancellationToken)
    {
        return Ok(await _profiles.GetProfilesAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SafetyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetProfileAsync(id, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("by-activity/{activityId:guid}")]
    [ProducesResponseType(typeof(SafetyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileForActivity(Guid activityId, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetProfileForActivityAsync(activityId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HasPermission("marine.profile.manage")]
    [HttpPost]
    [ProducesResponseType(typeof(SafetyProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateSafetyProfileDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profiles.CreateProfileAsync(dto, GetActorUserId(), cancellationToken);
            return CreatedAtAction(
                nameof(GetProfile),
                new { id = profile.Id },
                profile);
        }
        catch (InvalidOperationException exception)
        {
            return ToBadRequest("Profile Rejected", exception);
        }
        catch (SafetyProfileVersionConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Profile version conflict",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HasPermission("marine.profile.manage")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SafetyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdateSafetyProfileDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profiles.UpdateProfileAsync(id, dto, GetActorUserId(), cancellationToken);
            return profile is null ? NotFound() : Ok(profile);
        }
        catch (InvalidOperationException exception)
        {
            return ToBadRequest("Profile Update Rejected", exception);
        }
        catch (SafetyProfileVersionConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Profile version conflict",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    /// <summary>Approves a pending immutable profile version and makes it effective.</summary>
    [HasPermission("marine.profile.manage")]
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType(typeof(SafetyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewProfile(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profiles.ReviewProfileAsync(id, GetActorUserId(), cancellationToken);
            return profile is null ? NotFound() : Ok(profile);
        }
        catch (SafetyProfileReviewConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Profile review rejected",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (InvalidOperationException exception)
        {
            return ToBadRequest("Profile review rejected", exception);
        }
    }

    [HasPermission("marine.profile.manage")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateProfile(Guid id, CancellationToken cancellationToken)
    {
        var deactivated = await _profiles.DeactivateProfileAsync(id, cancellationToken);
        return deactivated ? NoContent() : NotFound();
    }

    private IActionResult ToBadRequest(string title, InvalidOperationException exception)
    {
        // A nonexistent or inactive activity is a state conflict, not invalid
        // input (G00: 400 is reserved for malformed coordinates/ranges/fields),
        // so both map to 409 like every other activity-state rejection.
        var conflict =
            exception.Message.Contains("does not exist", StringComparison.Ordinal) ||
            exception.Message.Contains("is not active", StringComparison.Ordinal);
        return StatusCode(
            conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
            new ProblemDetails
            {
                Title = title,
                Detail = exception.Message,
                Status = conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest
            });
    }

    private Guid GetActorUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(value, out var actorId))
        {
            return actorId;
        }

        throw new InvalidOperationException("The authenticated caller has no valid user identifier.");
    }
}
