using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Blueverse.MarineSafety.Tests.Fixtures;

public sealed class MarineSafetyWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtSigningKey = "marine-test-only-signing-key-with-at-least-32-bytes";

    private string _databaseName = $"marine-safety-tests-{Guid.NewGuid():N}";
    private StubOpenMeteoClient? _stubClient;
    private readonly TestPermissionResolver _permissionResolver = new();

    /// <summary>
    /// Rotates the in-memory database so each test method starts from an
    /// empty store; xUnit class fixtures would otherwise share snapshots and
    /// identity grants across tests.
    /// </summary>
    public void ResetDatabase()
    {
        _databaseName = $"marine-safety-tests-{Guid.NewGuid():N}";
        using var scope = Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<MarineSafetyDbContext>>();
        using var oldContext = new MarineSafetyDbContext(options);
        oldContext.Database.EnsureDeletedAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Forwards to whichever provider double the running test installed, so
    /// per-test configurations cannot leak into or from another test.
    /// </summary>
    private sealed class ForwardingOpenMeteoClient(MarineSafetyWebApplicationFactory owner) : IOpenMeteoClient
    {
        public Task<MarineConditionsResult> GetConditionsAsync(
            decimal latitude,
            decimal longitude,
            DateTime timeUtc,
            CancellationToken cancellationToken) =>
            owner.Provider.GetConditionsAsync(latitude, longitude, timeUtc, cancellationToken);
    }

    /// <summary>Replaces the provider double before the next request is handled.</summary>
    public StubOpenMeteoClient Provider
    {
        get => _stubClient ??= StubOpenMeteoClient.Unavailable("provider not configured by the test");
        set => _stubClient = value;
    }

    /// <summary>Permission resolver double carrying the seeded test grants.</summary>
    public TestPermissionResolver Resolver => _permissionResolver;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("JWT_SIGNING_KEY", JwtSigningKey);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=testing;Database=testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = JwtSigningKey,
                ["ConnectionStrings:DefaultConnection"] = "Host=testing;Database=testing",
                ["OpenMeteo:FreshnessMaxAgeMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MarineSafetyDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<MarineSafetyDbContext>>();
            services.RemoveAll<MarineSafetyDbContext>();
            services.AddDbContext<MarineSafetyDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Replace the real provider adapter with the forwarding double and
            // identity resolution with a deterministic test double.
            services.RemoveAll<IOpenMeteoClient>();
            services.AddSingleton<IOpenMeteoClient>(new ForwardingOpenMeteoClient(this));
            services.RemoveAll<IPermissionResolver>();
            services.AddSingleton<IPermissionResolver>(_permissionResolver);
        });
    }

    public async Task SeedAsync(Func<MarineSafetyDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarineSafetyDbContext>();
        await seed(db);
    }

    /// <summary>
    /// Issues a test JWT in the same shape Auth issues production tokens. The
    /// permission handler resolves the granted code from the seeded test user,
    /// so tests exercise the full role-to-permission path.
    /// </summary>
    public string CreateToken(Guid userId, IEnumerable<string>? permissionClaims = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("session_id", Guid.NewGuid().ToString()),
            new("session_version", "1"),
            new("token_version", "0")
        };

        foreach (var permission in permissionClaims ?? [])
        {
            claims.Add(new Claim("permission", permission));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "Blueverse.Auth",
            Audience = "Blueverse.Client",
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}

/// <summary>
/// Deterministic permission resolver double mirroring the seeded test
/// identities. It verifies how endpoints authorize, not how SQL resolves; the
/// production resolver is exercised by the repository's PostgreSQL evidence
/// path and by Auth's own permission tests.
/// </summary>
public sealed class TestPermissionResolver : IPermissionResolver
{
    private readonly Dictionary<Guid, HashSet<string>> _grants = new();

    public void Grant(Guid userId, string permissionCode)
    {
        if (!_grants.TryGetValue(userId, out var codes))
        {
            codes = [];
            _grants[userId] = codes;
        }

        codes.Add(permissionCode);
    }

    public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken) =>
        Task.FromResult(_grants.TryGetValue(userId, out var codes) && codes.Contains(permissionCode));
}
