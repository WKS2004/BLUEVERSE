using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blueverse.Auth.Tests.Unit;

public sealed class AuthAccountDeletionTests
{
    [Fact]
    [Trait("TestId", "AUTH-DEL-001")]
    public async Task SelfDeleteDenialNamesOnlySystemRolesAssignedToThatAccount()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-delete-system-role-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AuthDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new User { Email = "protected@example.com", FullName = "Protected User", PasswordHash = "hash" };
        var assignedRoles = new[]
        {
            new Role { Name = "Local Guide", IsSystemRole = false },
            new Role { Name = "Coastal Steward", IsSystemRole = true },
            new Role { Name = "Admin", IsSystemRole = true }
        };
        db.Users.Add(user);
        db.Roles.AddRange(assignedRoles);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(assignedRoles.Select(role => new UserRole { UserId = user.Id, RoleId = role.Id }));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteSelfAsync(user.Id));

        Assert.Equal(
            "This account cannot be deleted because it is assigned the system roles: Admin, Coastal Steward.",
            error.Message);
        Assert.Contains(await db.Users.ToListAsync(), savedUser => savedUser.Id == user.Id);
    }

    [Fact]
    [Trait("TestId", "AUTH-DEL-002")]
    public async Task SelfDeleteRemovesAnAccountWithoutSystemRoles()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-delete-ordinary-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AuthDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new User { Email = "ordinary@example.com", FullName = "Ordinary User", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var deleted = await CreateService(db).DeleteSelfAsync(user.Id);

        Assert.True(deleted);
        Assert.Null(await db.Users.FindAsync(user.Id));
    }

    [Fact]
    [Trait("TestId", "AUTH-DEL-003")]
    public async Task SelfDeleteReportsMissingAccountWithoutThrowing()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-delete-missing-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AuthDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var deleted = await CreateService(db).DeleteSelfAsync(Guid.NewGuid());

        Assert.False(deleted);
        Assert.Empty(await db.Users.ToListAsync());
    }

    private static AuthService CreateService(AuthDbContext db) =>
        new(
            db,
            null!,
            null!,
            null!,
            Options.Create(new AuthSessionOptions()),
            NullLogger<AuthService>.Instance);
}
