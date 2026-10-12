using Blueverse.Auth.Data;
using Blueverse.Auth.Models;

namespace Blueverse.Auth.Tests.Unit;

public sealed class PlannerPermissionsSeederTests
{
    [Fact]
    [Trait("TestId", "AUTH-PLANNER-SEED-001")]
    public async Task Planner_catalogue_and_traveller_grants_are_idempotent_and_have_no_admin_privileges()
    {
        await using var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var admin = new Role { Id = Guid.NewGuid(), Name = "Admin", IsSystemRole = true };
        db.Roles.Add(admin);
        await db.SaveChangesAsync();
        await PlannerPermissionsSeeder.SeedAsync(db);
        await PlannerPermissionsSeeder.SeedAsync(db);
        Assert.Equal(5, await db.Permissions.CountAsync());
        var traveller = await db.Roles.SingleAsync(r => r.Name == PlannerPermissionsSeeder.TravellerRole);
        Assert.False(traveller.IsSystemRole);
        var codes = await db.RolePermissions.Where(r => r.RoleId == traveller.Id).Select(r => r.Permission.Code).ToListAsync();
        Assert.Equal(PlannerPermissionsSeeder.Codes.Order(), codes.Order());
        Assert.DoesNotContain(codes, c => c.StartsWith("auth."));
        Assert.Equal(5, await db.RolePermissions.CountAsync(r => r.RoleId == admin.Id));
        Assert.Empty(await db.Users.ToListAsync());
    }
}
