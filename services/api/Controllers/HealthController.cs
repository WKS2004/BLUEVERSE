using Microsoft.AspNetCore.Mvc;

namespace Blueverse.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        service = "api",
        status = "healthy"
    });
}
