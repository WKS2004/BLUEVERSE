using System.Net;
using System.Text.Json;
using Blueverse.CoastalPlanner.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class HealthApiTests(PlannerApiFactory factory) : IClassFixture<PlannerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("TestId", "PLANNER-HEALTH-001")]
    public async Task Health_endpoint_returns_ok_with_healthy_status_and_database_connected()
    {
        using var response = await _client.GetAsync("/api/planner/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("coastal-planner", root.GetProperty("service").GetString());
        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("connected", root.GetProperty("database").GetString());
        Assert.Equal("applied", root.GetProperty("migrations").GetString());
        Assert.Equal("not_required_for_readiness", root.GetProperty("peerServices").GetString());
    }

    [Fact]
    [Trait("TestId", "PLANNER-HEALTH-002")]
    public async Task Health_endpoint_returns_503_when_database_is_unreachable()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoastalPlannerDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CoastalPlannerDbContext>>();
                services.RemoveAll<CoastalPlannerDbContext>();
                services.AddDbContext<CoastalPlannerDbContext>(options =>
                    options.UseNpgsql("Host=127.0.0.1;Port=59999;Database=unreachable;Timeout=1;CommandTimeout=1;"));
            });
        });
        using var client = customFactory.CreateClient();

        using var response = await client.GetAsync("/api/planner/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("coastal-planner", root.GetProperty("service").GetString());
        Assert.Equal("unhealthy", root.GetProperty("status").GetString());
        Assert.Equal("unavailable", root.GetProperty("database").GetString());
        Assert.Equal("unknown", root.GetProperty("migrations").GetString());
    }
}
