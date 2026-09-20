using Microsoft.AspNetCore.Mvc;

namespace Blueverse.Api.Tests.Fixtures;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/test-only/errors")]
public sealed class TestOnlyErrorController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        throw new InvalidOperationException("synthetic test exception that must not be exposed");
    }
}

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/test-only/request-context")]
public sealed class TestOnlyRequestContextController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        scheme = Request.Scheme,
        remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString()
    });
}
