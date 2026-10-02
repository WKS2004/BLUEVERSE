using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Blueverse.ExperienceBiodiversity.Data;

namespace Blueverse.ExperienceBiodiversity.Tests.Fixtures;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestSigningKey = "experience-biodiversity-tests-signing-key-32bytes-minimum";
    private const string TestIssuer = "Blueverse.Auth";
    private const string TestAudience = "Blueverse.Client";
    private const string CatalogueRead = "experiences.catalogue.read";
    private const string CatalogueManage = "experiences.catalogue.manage";
    private const string SystemRoleManage = "auth.role.system.manage";

    private static readonly Guid DefaultAdminId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    private readonly string _databaseName = $"blueverse-exp-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=testing;Database=testing");
        builder.UseSetting("JWT_SIGNING_KEY", TestSigningKey);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=testing;Database=testing",
                ["JWT_SIGNING_KEY"] = TestSigningKey,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ExperienceBiodiversityDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<ExperienceBiodiversityDbContext>();

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<ExperienceBiodiversityDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
    }

    public HttpClient CreateAdminClient(Guid? userId = null)
    {
        var client = CreateClient();
        SetBearerToken(client, CreateToken(
            userId ?? DefaultAdminId,
            [CatalogueRead, CatalogueManage, SystemRoleManage],
            ["Admin"]));
        return client;
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, params string[] permissionCodes)
    {
        var client = CreateClient();
        SetBearerToken(client, CreateToken(userId, permissionCodes, ["tourist"]));
        return client;
    }

    public HttpClient CreateRoleOnlyClient(Guid userId, params string[] roles)
    {
        var client = CreateClient();
        SetBearerToken(client, CreateToken(userId, [], roles));
        return client;
    }

    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    public async Task<bool> DestinationExistsAsync(string name)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperienceBiodiversityDbContext>();
        return await dbContext.Destinations.AnyAsync(destination => destination.Name == name);
    }

    public async Task<(int Destinations, int Activities, int Offerings, int Schedules)> GetCatalogueCountsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperienceBiodiversityDbContext>();
        return (
            await dbContext.Destinations.CountAsync(),
            await dbContext.Activities.CountAsync(),
            await dbContext.Offerings.CountAsync(),
            await dbContext.Schedules.CountAsync());
    }

    private static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static string CreateToken(Guid userId, IEnumerable<string> permissionCodes, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissionCodes.Select(permission => new Claim("permission", permission)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
