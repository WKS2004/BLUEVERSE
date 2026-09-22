using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blueverse.Api.Tests.Fixtures;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/test-only/auth")]
public sealed class TestOnlyAuthController : ControllerBase
{
    [Authorize]
    [HttpGet]
    public IActionResult Get() => Ok(new { authenticated = User.Identity?.IsAuthenticated == true });
}
