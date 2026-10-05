using Blueverse.Auth.Data;
using Blueverse.Auth.Services;
using Blueverse.Auth.Tests.Fixtures;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace Blueverse.Auth.Tests.Integration;

public sealed class PlannerRegistrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("TestId", "AUTH-PLANNER-REGISTER-001")]
    public async Task Registration_assigns_only_configured_planning_permissions(bool enabled)
    {
        await using var factory = new AuthWebApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<AuthSessionOptions>(o => o.SelfServicePlannerAccess = enabled)));
        using var client = configured.CreateClient();
        var email = AuthTestSupport.UniqueEmail("planner-registration");
        var identity = await AuthTestSupport.RegisterAsync(client, email: email);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(identity.Token);
        var codes = token.Claims.Where(c => c.Type == "permission").Select(c => c.Value).Order().ToArray();
        Assert.Equal(enabled ? PlannerPermissionsSeeder.Codes.Order().ToArray() : [], codes);
        Assert.DoesNotContain(token.Claims, c => c.Value.StartsWith("auth."));
        using var scope = configured.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var roles = await db.UserRoles.Where(r => r.UserId == user.Id).Include(r => r.Role).ToListAsync();
        Assert.Equal(enabled ? 1 : 0, roles.Count);
        Assert.All(roles, r => { Assert.Equal(PlannerPermissionsSeeder.TravellerRole, r.Role.Name); Assert.False(r.Role.IsSystemRole); });
        Assert.Single(await db.ActiveSessions.Where(s => s.UserId == user.Id).ToListAsync());
    }
}
