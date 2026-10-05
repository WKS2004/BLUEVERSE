using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.MarineSafety.Authorization;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Models;
using Blueverse.MarineSafety.Services;

namespace Blueverse.MarineSafety.Controllers;

/// <summary>
/// Read-only history over the persisted suitability assessments. Every
/// evaluate call stores an assessment row; this surface makes the stored
/// evidence queryable. All reads require the marine profile-read grant; there
/// is no client-facing write — assessments are created only by the evaluate
/// operation.
/// </summary>
[ApiController]
[Route("api/marine/assessments")]
[HasPermission("marine.profile.read")]
public sealed class AssessmentsController : ControllerBase
{
    private readonly ISuitabilityService _suitability;

    public AssessmentsController(ISuitabilityService suitability)
    {
        _suitability = suitability;
    }

    /// <summary>Stored assessments, newest first, filtered by activity, result and evaluation window.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AssessmentHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssessments(
        [FromQuery] Guid? activityId,
        [FromQuery] string? result,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(result) &&
            !new[] { SuitabilityResults.Suitable, SuitabilityResults.Caution, SuitabilityResults.Unsuitable, SuitabilityResults.Unknown }
                .Contains(result.Trim().ToUpperInvariant(), StringComparer.Ordinal))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid assessment result filter",
                Detail = "Result must be SUITABLE, CAUTION, UNSUITABLE or UNKNOWN.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (from.HasValue && to.HasValue && MarineTime.ToUtc(from.Value) > MarineTime.ToUtc(to.Value))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid assessment time window",
                Detail = "The from time must not be later than the to time.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(await _suitability.GetAssessmentsAsync(activityId, result, from, to, cancellationToken));
    }

    /// <summary>One stored assessment with its full evidence and profile-version references.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AssessmentHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssessment(Guid id, CancellationToken cancellationToken)
    {
        var assessment = await _suitability.GetAssessmentAsync(id, cancellationToken);
        return assessment is null ? NotFound() : Ok(assessment);
    }
}
