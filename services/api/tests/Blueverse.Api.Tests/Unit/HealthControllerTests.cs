using System.Text.Json;
using Blueverse.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Blueverse.Api.Tests.Unit;

public sealed class HealthControllerTests
{
    [Fact]
    [Trait("CaseId", "API-HEALTH-UNIT-001")]
    public void API_HEALTH_UNIT_001_Get_returns_the_api_liveness_contract()
    {
        var result = new HealthController().Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = document.RootElement;

        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal("api", root.GetProperty("service").GetString());
        Assert.Equal("healthy", root.GetProperty("status").GetString());
    }
}
