using Microsoft.Extensions.Logging.Abstractions;

namespace Blueverse.Auth.Tests.Unit;

public sealed class AdminSystemRolePermissionTests
{
    [Fact(DisplayName = "AUTH-SEED-ADMIN-ALL-001 Admin System Role receives every registered permission idempotently")]
    [Trait("TestId", "AUTH-SEED-ADMIN-ALL-001")]
    public async Task AdminSystemRoleReceivesEveryRegisteredPermission()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-admin-coastal-permissions-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AuthDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Permissions.Add(new Permission
        {
            Id = Guid.NewGuid(),
            Code = "marine.safety.read",
            Description = "Read marine safety information",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAIL"] = "admin-coastal@blueverse.local",
                ["ADMIN_PASSWORD"] = "AdminCoastalPassword-123!"
            })
            .Build();
        var hasher = new PasswordHasherService();

        await AuthDataSeeder.SeedAdminAsync(db, configuration, hasher, NullLogger.Instance);
        await AuthDataSeeder.SeedAdminAsync(db, configuration, hasher, NullLogger.Instance);

        var adminRole = await db.Roles.SingleAsync(role => role.Name == "Admin" && role.IsSystemRole);
        var assignedPermissionCodes = await db.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == adminRole.Id)
            .Select(rolePermission => rolePermission.Permission.Code)
            .ToListAsync();
        var allRegisteredPermissionCodes = await db.Permissions
            .Select(permission => permission.Code)
            .ToListAsync();
        var assignedCodes = assignedPermissionCodes.ToHashSet(StringComparer.Ordinal);

        Assert.Equal(allRegisteredPermissionCodes.Count, assignedPermissionCodes.Count);
        Assert.Equal(assignedPermissionCodes.Count, assignedPermissionCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.True(allRegisteredPermissionCodes.ToHashSet(StringComparer.Ordinal).SetEquals(assignedCodes));
        Assert.Contains("marine.safety.read", assignedCodes);
        Assert.All(
            new[]
            {
                PermissionCodes.OperationsAssessmentCreate,
                PermissionCodes.OperationsAssessmentRead,
                PermissionCodes.OperationsAssessmentQueueRead,
                PermissionCodes.OperationsAssessmentDecide,
                PermissionCodes.OperationsTargetStatusRead,
                PermissionCodes.OperationsTargetHistoryRead,
                PermissionCodes.OperationsEvidenceUpload,
                PermissionCodes.OperationsEvidenceRead,
                PermissionCodes.OperationsAlertRead,
                PermissionCodes.OperationsAlertManage,
                PermissionCodes.OperationsAlertDecide
            },
            code => Assert.Contains(code, assignedCodes));
    }
}
