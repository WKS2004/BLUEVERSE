using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.ExperienceBiodiversity.Authorization;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Authorize]
[Route("api/experiences/availability")]
public sealed class AvailabilityController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;
    private readonly ILogger<AvailabilityController> _logger;
    private readonly IUserContext _userContext;

    public AvailabilityController(
        IEvaluationService evaluationService,
        ILogger<AvailabilityController> logger,
        IUserContext userContext)
    {
        _evaluationService = evaluationService;
        _logger = logger;
        _userContext = userContext;
    }

    [HasPermission("experiences.catalogue.read")]
    [HttpPost("evaluations")]
    public async Task<IActionResult> EvaluateAvailability(
        [FromBody] AvailabilityEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = 401,
                detail = "Authentication is required to evaluate availability."
            });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.read", "experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Forbidden",
                status = 403,
                detail = "You do not have permission to evaluate availability."
            });
        }

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
