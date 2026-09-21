using Microsoft.Extensions.Logging.Abstractions;

namespace Blueverse.Auth.Tests.Unit;

public sealed class AuthDataSeederTests
{
    [Fact]
    [Trait("TestId", "AUTH-SEED-001")]
    public async Task BootstrapAdminSeedingIsNormalizedPasswordSafeAndIdempotent()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-seeder-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AuthDbContext(options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAIL"] = "  Bootstrap@BlueVerse.Local  ",
                ["ADMIN_PASSWORD"] = "BootstrapPassword-123!"
            })
            .Build();
        var hasher = new PasswordHasherService();
        await db.Database.EnsureCreatedAsync();

        await AuthDataSeeder.SeedAdminAsync(db, configuration, hasher, NullLogger.Instance);
        await AuthDataSeeder.SeedAdminAsync(db, configuration, hasher, NullLogger.Instance);

        var admin = await db.Users
            .Include(user => user.UserRoles)
            .SingleAsync(user => user.Email == "bootstrap@blueverse.local");
        var adminRole = await db.Roles.SingleAsync(role => role.Name == "Admin");

        Assert.Equal(1, await db.Users.CountAsync(user => user.Email == "bootstrap@blueverse.local"));
        Assert.Contains(admin.UserRoles, userRole => userRole.RoleId == adminRole.Id);
        Assert.Equal(6, await db.Permissions.CountAsync());
        Assert.Equal(6, await db.RolePermissions.CountAsync(item => item.RoleId == adminRole.Id));
        Assert.True(hasher.VerifyPassword("BootstrapPassword-123!", admin.PasswordHash));
        Assert.NotEqual("BootstrapPassword-123!", admin.PasswordHash);
    }

    [Fact]
    [Trait("TestId", "AUTH-SEED-002")]
    public async Task MissingBootstrapCredentialsLeaveTheDatabaseUnchanged()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-seeder-empty-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AuthDbContext(options);
        var configuration = new ConfigurationBuilder().Build();
        await db.Database.EnsureCreatedAsync();

        await AuthDataSeeder.SeedAdminAsync(db, configuration, new PasswordHasherService(), NullLogger.Instance);

        Assert.Empty(await db.Users.ToListAsync());
        Assert.Equal(1, await db.Roles.CountAsync());
        Assert.Equal(6, await db.Permissions.CountAsync());
    }
}
