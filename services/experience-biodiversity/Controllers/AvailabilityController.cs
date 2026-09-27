using Microsoft.AspNetCore.Mvc;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/availability")]
public sealed class AvailabilityController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;
    private readonly ILogger<AvailabilityController> _logger;

    public AvailabilityController(
        IEvaluationService evaluationService,
        ILogger<AvailabilityController> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;
    }

    [HttpPost("evaluations")]
    public async Task<IActionResult> EvaluateAvailability(
        [FromBody] AvailabilityEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EndsAt <= request.StartsAt)
        {
            return BadRequest(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Invalid Evaluation Interval",
                status = 400,
                detail = "EndsAt must be strictly greater than StartsAt."
            });
        }

        var result = await _evaluationService.EvaluateAvailabilityAsync(request, cancellationToken);
        return Ok(result);
    }
}
