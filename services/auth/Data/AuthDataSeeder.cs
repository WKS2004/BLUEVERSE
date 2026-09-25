using Blueverse.Auth.Models;
using Blueverse.Auth.Services;
using Blueverse.Auth.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.Auth.Data;

public static class AuthDataSeeder
{
    public static async Task SeedAdminAsync(
        AuthDbContext db,
        IConfiguration configuration,
        IPasswordHasherService passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var email = configuration["ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
        var password = configuration["ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("ADMIN_EMAIL or ADMIN_PASSWORD is not configured; bootstrap admin seeding was skipped.");
            return;
        }

        var adminRole = await db.Roles.SingleOrDefaultAsync(r => r.Name == "Admin", cancellationToken);
        if (adminRole is null)
        {
            adminRole = new Role
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222201"),
                Name = "Admin",
                Description = "System Administrator with full management access",
                IsSystemRole = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!adminRole.IsSystemRole)
        {
            adminRole.IsSystemRole = true;
        }

        var adminPermissions = new Dictionary<string, (Guid Id, string Description)>
        {
            [PermissionCodes.UserRead] = (Guid.Parse("11111111-1111-1111-1111-111111111101"), "View users"),
            [PermissionCodes.UserManage] = (Guid.Parse("11111111-1111-1111-1111-111111111102"), "Manage users"),
            [PermissionCodes.RoleRead] = (Guid.Parse("11111111-1111-1111-1111-111111111103"), "View roles"),
            [PermissionCodes.RoleManage] = (Guid.Parse("11111111-1111-1111-1111-111111111104"), "Manage roles"),
            [PermissionCodes.PermissionRead] = (Guid.Parse("11111111-1111-1111-1111-111111111105"), "View permissions"),
            [PermissionCodes.SystemRoleManage] = (Guid.Parse("11111111-1111-1111-1111-111111111106"), "Assign or manage system roles"),
            [PermissionCodes.UserCreate] = (Guid.Parse("11111111-1111-1111-1111-111111111107"), "Create user accounts"),
            [PermissionCodes.UserUpdate] = (Guid.Parse("11111111-1111-1111-1111-111111111108"), "Update user accounts and role assignments"),
            [PermissionCodes.UserDelete] = (Guid.Parse("11111111-1111-1111-1111-111111111109"), "Delete user accounts"),
            [PermissionCodes.RoleCreate] = (Guid.Parse("11111111-1111-1111-1111-111111111110"), "Create roles"),
            [PermissionCodes.RoleUpdate] = (Guid.Parse("11111111-1111-1111-1111-111111111111"), "Update roles and assigned permissions"),
            [PermissionCodes.RoleDelete] = (Guid.Parse("11111111-1111-1111-1111-111111111112"), "Delete roles")
        };

        var permissions = await db.Permissions
            .Where(p => adminPermissions.Keys.Contains(p.Code))
            .ToListAsync(cancellationToken);

        foreach (var (code, definition) in adminPermissions)
        {
            if (permissions.Any(p => p.Code == code))
                continue;

            var permission = new Permission
            {
                Id = definition.Id,
                Code = code,
                Description = definition.Description,
                CreatedAt = DateTime.UtcNow
            };
            db.Permissions.Add(permission);
            permissions.Add(permission);
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var permission in permissions)
        {
            var assigned = await db.RolePermissions.AnyAsync(
                rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id,
                cancellationToken);

            if (!assigned)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.Id,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        var adminUser = await db.Users
            .Include(u => u.UserRoles)
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (adminUser is null)
        {
            adminUser = new User
            {
                Email = email,
                FullName = "BLUEVERSE Administrator",
                PasswordHash = passwordHasher.HashPassword(password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(adminUser);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!adminUser.UserRoles.Any(ur => ur.RoleId == adminRole.Id))
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = adminUser.Id,
                RoleId = adminRole.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Bootstrap administrator is available as {AdminEmail}.", email);
    }
}
