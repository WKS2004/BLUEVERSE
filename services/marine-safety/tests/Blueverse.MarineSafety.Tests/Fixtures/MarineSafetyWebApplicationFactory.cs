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
    private readonly TestIdentityTokenValidator _identityTokenValidator = new();

    /// <summary>
    /// Rotates the in-memory database so each test method starts from an
    /// empty store; xUnit class fixtures would otherwise share snapshots and
    /// identity grants across tests.
    /// </summary>
    public void ResetDatabase()
    {
        _databaseName = $"marine-safety-tests-{Guid.NewGuid():N}";
        _permissionResolver.Reset();
        _identityTokenValidator.Reset();
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

    /// <summary>Account/session state double used by the JWT validation event.</summary>
    public TestIdentityTokenValidator IdentityValidator => _identityTokenValidator;

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
            services.RemoveAll<IIdentityTokenValidator>();
            services.AddSingleton<IIdentityTokenValidator>(_identityTokenValidator);
        });
    }

    public async Task SeedAsync(Func<MarineSafetyDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarineSafetyDbContext>();
        await seed(db);
    }

    /// <summary>
    /// Issues a test JWT in the same shape Auth issues production tokens and
    /// registers its account/session state with the deterministic validator.
    /// </summary>
    public string CreateToken(
        Guid userId,
        IEnumerable<string>? permissionClaims = null,
        DateTime? expires = null,
        int tokenVersion = 0,
        Guid? sessionId = null,
        int sessionVersion = 1)
    {
        return CreateTokenCore(userId, permissionClaims, expires, tokenVersion, sessionId, sessionVersion);
    }

    private string CreateTokenCore(
        Guid userId,
        IEnumerable<string>? permissionClaims,
        DateTime? expires,
        int tokenVersion,
        Guid? sessionId,
        int sessionVersion)
    {
        var actualSessionId = sessionId ?? Guid.NewGuid();
        var actualTokenExpiry = expires ?? DateTime.UtcNow.AddMinutes(15);
        _identityTokenValidator.RegisterSession(
            userId,
            tokenVersion,
            actualSessionId,
            sessionVersion,
            DateTime.UtcNow.AddDays(1));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("session_id", actualSessionId.ToString()),
            new("session_version", sessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("token_version", tokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
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
            Expires = actualTokenExpiry,
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
/// production IdentityPermissionResolver and IdentityTokenValidator queries
/// are not exercised by this in-memory test host.
/// </summary>
public sealed class TestPermissionResolver : IPermissionResolver
{
    private readonly Dictionary<Guid, HashSet<string>> _grants = new();
    private int _callCount;

    public int CallCount => Volatile.Read(ref _callCount);

    public void Reset()
    {
        _grants.Clear();
        Interlocked.Exchange(ref _callCount, 0);
    }

    public void Grant(Guid userId, string permissionCode)
    {
        if (!_grants.TryGetValue(userId, out var codes))
        {
            codes = [];
            _grants[userId] = codes;
        }

        codes.Add(permissionCode);
    }

    public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_grants.TryGetValue(userId, out var codes) && codes.Contains(permissionCode));
    }
}
