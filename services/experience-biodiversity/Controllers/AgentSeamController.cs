using Microsoft.AspNetCore.Mvc;
using Blueverse.ExperienceBiodiversity.DTOs;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/agent")]
public sealed class AgentSeamController : ControllerBase
{
    [HttpGet("context")]
    public IActionResult GetAgentContext()
    {
        // Pre-G07 typed seam: returns explicit not_connected status as dictated by ADR-0007 and ADR-0020
        var seam = new AgentContextResponseDto(
            AgentName: "Coastal Experience & Biodiversity Agent",
            Status: "not_connected",
            Detail: "Member component business workflows are active. Executable Agentic AI orchestration is scheduled for post-G07 integration. Read-only context Seam is active.",
            PlannedTools: new[]
            {
                "destination_lookup",
                "activity_lookup",
                "offering_lookup",
                "schedule_lookup",
                "biodiversity_prediction_lookup"
            },
            CheckedAt: DateTimeOffset.UtcNow);

        return Ok(seam);
    }
}
