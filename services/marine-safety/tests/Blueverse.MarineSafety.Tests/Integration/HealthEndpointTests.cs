using System.Text.Json;
using Blueverse.MarineSafety.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.MarineSafety.Tests.Integration;

/// <summary>
/// Health endpoint tests (M2-HEALTH-*), mirroring the Auth service's health
/// contract: anonymous liveness plus a real database connectivity probe that
/// degrades to 503 when the database is unreachable.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<MarineSafetyWebApplicationFactory>
{
    private readonly MarineSafetyWebApplicationFactory _factory;

    public HealthEndpointTests(MarineSafetyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "M2-HEALTH-001")]
    public async Task HealthReportsTheMarineServiceAndDatabaseAsHealthy()
    {
        _factory.ResetDatabase();
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/marine/health");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("marine-safety", root.GetProperty("service").GetString());
        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("connected", root.GetProperty("database").GetString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Trait("CaseId", "M2-HEALTH-002")]
    public async Task HealthRemainsReachableWithoutCredentials()
    {
        _factory.ResetDatabase();
        using var client = _factory.CreateClient();

        // Health is a liveness surface: no Authorization header is sent and
        // the endpoint must not challenge for one.
        using var response = await client.GetAsync("/api/marine/health");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "M2-HEALTH-003")]
    public async Task HealthReturnsServiceUnavailableWhenTheDatabaseCannotBeReached()
    {
        // Mirrors the Auth health test: a controller wired to an unreachable
        // PostgreSQL endpoint must degrade to a structured 503, not throw.
        var options = new DbContextOptionsBuilder<MarineSafetyDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Password=unavailable;Timeout=1;Command Timeout=1")
            .Options;
        await using var db = new MarineSafetyDbContext(options);
        var controller = new HealthController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.Get();
        var result = Assert.IsType<ObjectResult>(actionResult);
        var body = JsonSerializer.SerializeToElement(result.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("marine-safety", body.GetProperty("service").GetString());
        Assert.Equal("unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("unavailable", body.GetProperty("database").GetString());
    }
}
