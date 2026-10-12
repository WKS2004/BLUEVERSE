using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Integration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class PlannerApiFactory : WebApplicationFactory<Program>
{
    private const string SigningKey = "planner-integration-test-signing-key-32-bytes";
    private const string TestConnectionString = "Host=localhost;Database=planner-tests;Username=planner;Password=test-only";
    private readonly string _databaseName = $"planner-api-{Guid.NewGuid():N}";
    private readonly string? _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    private readonly string? _previousSigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");

    public PlannerApiFactory()
    {
        // Minimal hosting reads these before WebApplicationFactory's service
        // overrides are applied. The database registration is replaced below.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestConnectionString);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", SigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["JWT_SIGNING_KEY"] = SigningKey,
                ["Jwt:Issuer"] = "Blueverse.Auth",
                ["Jwt:Audience"] = "Blueverse.Client"
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CoastalPlannerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CoastalPlannerDbContext>>();
            services.RemoveAll<CoastalPlannerDbContext>();
            services.AddDbContext<CoastalPlannerDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.AddDataProtection().PersistKeysToFileSystem(
                new DirectoryInfo(Path.Combine(Path.GetTempPath(), _databaseName, "data-protection")));
            services.RemoveAll<IPeerServicesClient>();
            services.AddSingleton<IPeerServicesClient>(new TestPeerServicesClient());
        });
    }

    public string CreateToken(Guid actorId, params string[] permissions)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, actorId.ToString()) };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "Blueverse.Auth",
            audience: "Blueverse.Client",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: signingCredentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _previousConnectionString);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", _previousSigningKey);
    }
}
