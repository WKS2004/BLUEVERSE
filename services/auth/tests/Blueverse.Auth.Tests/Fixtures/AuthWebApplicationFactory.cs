using Microsoft.Extensions.Logging.Abstractions;

namespace Blueverse.Auth.Tests.Fixtures;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "test-admin@blueverse.local";
    public const string AdminPassword = "TestAdminPassword-123!";
    public const string JwtSigningKey = "auth-test-only-signing-key-with-at-least-32-bytes";

    private readonly string _databaseName = $"blueverse-auth-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("JWT_SIGNING_KEY", JwtSigningKey);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=testing;Database=testing");
        builder.UseSetting("ADMIN_EMAIL", string.Empty);
        builder.UseSetting("ADMIN_PASSWORD", string.Empty);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = JwtSigningKey,
                ["ConnectionStrings:DefaultConnection"] = "Host=testing;Database=testing",
                ["ADMIN_EMAIL"] = string.Empty,
                ["ADMIN_PASSWORD"] = string.Empty,
                ["Jwt:ExpiryHours"] = "1"
            });
        });

        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(TestOnlyAuthErrorController).Assembly);
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<AuthDbContext>>();
            services.RemoveAll<AuthDbContext>();
            services.AddDbContext<AuthDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task SeedAdminAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ADMIN_EMAIL"] = AdminEmail,
            ["ADMIN_PASSWORD"] = AdminPassword
        }).Build();
        await AuthDataSeeder.SeedAdminAsync(
            db,
            configuration,
            hasher,
            NullLogger.Instance);
    }
}
