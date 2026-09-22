using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Blueverse.Auth.Tests.Fixtures;

public sealed class PostgresAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtSigningKey = "auth-postgres-test-only-signing-key-with-at-least-32-bytes";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BLUEVERSE_AUTH_POSTGRES_TEST_CONNECTION");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "BLUEVERSE_AUTH_POSTGRES_TEST_CONNECTION must be set for PostgreSQL integration tests.");
        }

        builder.UseEnvironment("Testing");
        builder.UseSetting("JWT_SIGNING_KEY", JwtSigningKey);
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("ADMIN_EMAIL", string.Empty);
        builder.UseSetting("ADMIN_PASSWORD", string.Empty);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = JwtSigningKey,
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ADMIN_EMAIL"] = string.Empty,
                ["ADMIN_PASSWORD"] = string.Empty,
                ["Jwt:ExpiryHours"] = "1"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<AuthDbContext>>();
            services.RemoveAll<AuthDbContext>();
            services.AddDbContext<AuthDbContext>(options => options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));
        });
    }
}
