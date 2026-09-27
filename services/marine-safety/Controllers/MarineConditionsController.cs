using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.MarineSafety.Authorization;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Models;
using Blueverse.MarineSafety.Providers;
using Blueverse.MarineSafety.Services;

namespace Blueverse.MarineSafety.Controllers;

/// <summary>
/// Marine condition read surface. Condition snapshots are provider-derived:
/// there is no client-facing create/update/delete, only read and history.
/// </summary>
[ApiController]
[Route("api/marine")]
public sealed class MarineConditionsController : ControllerBase
{
    private readonly IConditionService _conditions;

    public MarineConditionsController(IConditionService conditions)
    {
        _conditions = conditions;
    }

    /// <summary>Current or forecast conditions for a location and optional time (UTC).</summary>
    [HttpGet("current")]
    [HasPermission("marine.profile.read")]
    [ProducesResponseType(typeof(ConditionSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCurrent(
        [FromQuery, Required] decimal? latitude,
        [FromQuery, Required] decimal? longitude,
        [FromQuery] DateTime? time,
        CancellationToken cancellationToken)
    {
        if (latitude is null || longitude is null ||
            latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Location",
                Detail = "latitude and longitude are required and must be within valid WGS84 ranges.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var snapshot = await _conditions.GetConditionsAsync(latitude.Value, longitude.Value, time, cancellationToken);
            return Ok(ToDto(snapshot));
        }
        catch (ConditionsUnavailableException exception)
        {
            return ProviderUnavailable(exception);
        }
    }

    /// <summary>One stored condition snapshot with provenance and missing fields.</summary>
    [HttpGet("snapshots/{id:guid}")]
    [HasPermission("marine.profile.read")]
    [ProducesResponseType(typeof(ConditionSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshot(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await _conditions.GetSnapshotAsync(id, cancellationToken);
        return snapshot is null ? NotFound() : Ok(ToDto(snapshot));
    }

    /// <summary>Stored condition history filtered by location and/or time window.</summary>
    [HttpGet("history")]
    [HasPermission("marine.profile.read")]
    [ProducesResponseType(typeof(IReadOnlyList<ConditionSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] decimal? latitude,
        [FromQuery] decimal? longitude,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var history = await _conditions.GetHistoryAsync(latitude, longitude, from, to, cancellationToken);
        return Ok(history.Select(ToDto).ToList());
    }

    private static ConditionSnapshotDto ToDto(ConditionSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.Latitude,
        snapshot.Longitude,
        snapshot.ForecastTime,
        snapshot.RetrievedAt,
        snapshot.WindSpeed,
        snapshot.WaveHeight,
        snapshot.SwellHeight,
        snapshot.Rain,
        snapshot.WeatherCode,
        snapshot.Source,
        snapshot.FreshnessStatus,
        snapshot.MissingFields);

    private IActionResult ProviderUnavailable(ConditionsUnavailableException exception)
    {
        Response.Headers.RetryAfter = "60";
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Title = "Marine Conditions Unavailable",
            Detail = "Condition information cannot be obtained right now. The suitability result would be UNKNOWN; retry later.",
            Status = StatusCodes.Status503ServiceUnavailable
        });
    }
}

/// <summary>
/// The Member 2 non-CRUD business operation: deterministic activity/location/
/// time suitability assessment. Available to any caller holding the read
/// grant; the result always carries its evidence, source and freshness.
/// </summary>
[ApiController]
[Route("api/marine")]
public sealed class SuitabilityController : ControllerBase
{
    private readonly ISuitabilityService _suitability;

    public SuitabilityController(ISuitabilityService suitability)
    {
        _suitability = suitability;
    }

    [HttpPost("evaluate")]
    [HasPermission("marine.profile.read")]
    [ProducesResponseType(typeof(SuitabilityResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateSuitabilityDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _suitability.EvaluateAsync(dto, cancellationToken);
            return Ok(result);
        }
        catch (SuitabilityValidationException exception)
        {
            var isProfileProblem = exception.Field == "activityId";
            return StatusCode(
                isProfileProblem ? StatusCodes.Status409Conflict : StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Title = isProfileProblem ? "No Safety Profile Configured" : "Activity Not Found",
                    Detail = exception.Message,
                    Status = isProfileProblem ? StatusCodes.Status409Conflict : StatusCodes.Status404NotFound
                });
        }
        catch (ConditionsUnavailableException)
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Marine Conditions Unavailable",
                Detail = "Condition evidence could not be obtained; no suitability decision can be made.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }
}
