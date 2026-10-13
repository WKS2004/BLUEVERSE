using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Blueverse.ExperienceBiodiversity.Authorization;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Authorize]
[Route("api/experiences/agent")]
public sealed class AgentSeamController : ControllerBase
{
    private readonly IExperienceAgentConsumerService _agentConsumerService;

    public AgentSeamController(IExperienceAgentConsumerService agentConsumerService)
    {
        _agentConsumerService = agentConsumerService;
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetAgentContext(CancellationToken cancellationToken)
    {
        var info = await _agentConsumerService.GetAgentInfoAsync(cancellationToken);
        var seam = new AgentContextResponseDto(
            AgentName: info.AgentName,
            Status: info.Status,
            Detail: info.Detail,
            PlannedTools: info.Tools,
            CheckedAt: info.CheckedAt);

        return Ok(seam);
    }

    [HasPermission("experiences.catalogue.read")]
    [HttpGet("info")]
    public async Task<IActionResult> GetExperienceAgentInfo(CancellationToken cancellationToken = default)
    {
        var info = await _agentConsumerService.GetAgentInfoAsync(cancellationToken);
        return Ok(info);
    }

    [HasPermission("experiences.catalogue.read")]
    [HttpPost("context-reports")]
    public async Task<IActionResult> GenerateExperienceAgentContextReport(ExperienceAgentContextRequestDto? request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request payload is required." });
        }

        if (request.WorkflowId == Guid.Empty)
        {
            return BadRequest(new { error = "WorkflowId must be a non-empty UUID." });
        }

        if (string.IsNullOrWhiteSpace(request.StepId) || request.StepId.Trim().Length > 128)
        {
            return BadRequest(new { error = "StepId must be a non-empty string up to 128 characters." });
        }

        if (string.IsNullOrWhiteSpace(request.Objective) || request.Objective.Trim().Length is < 3 or > 1000)
        {
            return BadRequest(new { error = "Objective must be between 3 and 1000 characters." });
        }

        var hasDestination = request.DestinationId.HasValue && request.DestinationId.Value != Guid.Empty;
        var hasActivity = request.ActivityId.HasValue && request.ActivityId.Value != Guid.Empty;
        var hasOffering = request.OfferingId.HasValue && request.OfferingId.Value != Guid.Empty;

        if ((request.DestinationId.HasValue && request.DestinationId.Value == Guid.Empty) ||
            (request.ActivityId.HasValue && request.ActivityId.Value == Guid.Empty) ||
            (request.OfferingId.HasValue && request.OfferingId.Value == Guid.Empty))
        {
            return BadRequest(new { error = "Provided selector UUIDs must not be empty." });
        }

        if (!hasDestination && !hasActivity && !hasOffering)
        {
            return BadRequest(new { error = "At least one selector (DestinationId, ActivityId, or OfferingId) must be provided." });
        }

        if (request.PeriodStartsAt.HasValue ^ request.PeriodEndsAt.HasValue)
        {
            return BadRequest(new { error = "PeriodStartsAt and PeriodEndsAt must both be provided when evaluating a time window." });
        }

        if (request.PeriodStartsAt.HasValue && request.PeriodEndsAt.HasValue && request.PeriodEndsAt.Value <= request.PeriodStartsAt.Value)
        {
            return BadRequest(new { error = "PeriodEndsAt must be strictly greater than PeriodStartsAt." });
        }

        var reportEnvelope = await _agentConsumerService.GenerateContextReportAsync(request, cancellationToken);
        return Ok(reportEnvelope);
    }
}
