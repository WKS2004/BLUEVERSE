using Microsoft.AspNetCore.Mvc;

namespace Blueverse.Auth.Tests.Fixtures;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/test-only/auth-errors")]
public sealed class TestOnlyAuthErrorController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        throw new InvalidOperationException("synthetic auth exception that must not be exposed");
    }
}
