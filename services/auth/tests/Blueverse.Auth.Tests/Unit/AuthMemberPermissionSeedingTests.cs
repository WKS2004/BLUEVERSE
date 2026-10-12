using Blueverse.Auth.Authorization;
using Blueverse.Auth.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blueverse.Auth.Tests.Unit;

public sealed class AuthMemberPermissionSeedingTests
{
    [Theory, Trait("TestId", "AUTH-SEED-DOMAIN-001")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Occupied_member_seed_ids_preserve_existing_permissions_and_grants_on_repeated_bootstrap(bool bothIdsOccupied)
    {
        await using var db = CreateDatabase();
        await db.Database.EnsureCreatedAsync();
        var role = await db.Roles.SingleAsync(r => r.Name == "Admin");
        var originalTime = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var existing = new List<Permission>
        {
            new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111113"), Code = "experiences.catalogue.read", Description = "Existing catalogue access", CreatedAt = originalTime }
        };
        if (bothIdsOccupied) existing.Add(new Permission { Id = Guid.Parse("11111111-1111-1111-1111-111111111114"), Code = "experiences.catalogue.manage", Description = "Existing catalogue management", CreatedAt = originalTime });
        foreach (var permission in existing)
        {
            db.Permissions.Add(permission);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, AssignedAt = originalTime });
        }
        await db.SaveChangesAsync();
        await Seed(db);
        var first = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id);
        var read = await db.Permissions.SingleAsync(p => p.Code == PermissionCodes.MarineProfileRead);
        var manage = await db.Permissions.SingleAsync(p => p.Code == PermissionCodes.MarineProfileManage);
        Assert.DoesNotContain(read.Id, existing.Select(p => p.Id));
        Assert.DoesNotContain(manage.Id, existing.Select(p => p.Id));
        Assert.NotEqual(read.Id, manage.Id);
        foreach (var permission in existing)
        {
            var stored = await db.Permissions.SingleAsync(p => p.Id == permission.Id);
            Assert.Equal(permission.Code, stored.Code);
            Assert.Equal(permission.Description, stored.Description);
            Assert.Equal(originalTime, stored.CreatedAt);
            Assert.Equal(originalTime, (await db.RolePermissions.SingleAsync(p => p.RoleId == role.Id && p.PermissionId == permission.Id)).AssignedAt);
        }
        Assert.True(await db.RolePermissions.AnyAsync(p => p.RoleId == role.Id && p.PermissionId == read.Id));
        Assert.True(await db.RolePermissions.AnyAsync(p => p.RoleId == role.Id && p.PermissionId == manage.Id));
        await Seed(db);
        var second = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id);
        Assert.Equal(first.Count, second.Count);
        foreach (var pair in first) Assert.Equal(pair.Value, second[pair.Key]);
        Assert.Equal(16, await db.Permissions.CountAsync());
        Assert.Equal(16, await db.RolePermissions.CountAsync(p => p.RoleId == role.Id));
        Assert.Single(await db.Users.ToListAsync());
    }

    [Fact, Trait("TestId", "AUTH-SEED-DOMAIN-002")]
    public async Task Existing_permission_code_keeps_its_identity_instead_of_being_replaced_by_a_seed_id()
    {
        await using var db = CreateDatabase();
        await db.Database.EnsureCreatedAsync();
        var id = Guid.NewGuid();
        db.Permissions.Add(new Permission { Id = id, Code = PermissionCodes.MarineProfileRead, Description = "Existing Marine permission", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await Seed(db);
        await Seed(db);
        var read = Assert.Single(await db.Permissions.Where(p => p.Code == PermissionCodes.MarineProfileRead).ToListAsync());
        Assert.Equal(id, read.Id);
        Assert.Equal("Existing Marine permission", read.Description);
        Assert.Equal(16, await db.Permissions.CountAsync());
        Assert.True(await db.RolePermissions.AnyAsync(p => p.PermissionId == id));
    }

    private static AuthDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase($"member-seed-{Guid.NewGuid():N}").Options);
    private static Task Seed(AuthDbContext db) => AuthDataSeeder.SeedAdminAsync(db,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ADMIN_EMAIL"] = "member-seed@example.test", ["ADMIN_PASSWORD"] = "SyntheticSeedPassword-123!" }).Build(),
        new PasswordHasherService(), NullLogger.Instance);
}
