using Blueverse.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.Auth.Data;

// Additive domain integration. Auth's existing administration and session flows stay authoritative.
public static class PlannerPermissionsSeeder
{
    public const string TravellerRole = "Coastal traveller";
    public static readonly string[] Codes = ["planner.recommendations.create", "planner.recommendations.read",
        "planner.workflows.read", "planner.itineraries.manage", "planner.biodiversity.read"];

    public static async Task SeedAsync(AuthDbContext db, CancellationToken ct = default)
    {
        var permissions = await db.Permissions.Where(p => Codes.Contains(p.Code)).ToListAsync(ct);
        foreach (var code in Codes.Except(permissions.Select(p => p.Code)))
        {
            var permission = new Permission { Id = Guid.NewGuid(), Code = code,
                Description = code switch {
                    "planner.recommendations.create" => "Find coastal experiences for a trip",
                    "planner.recommendations.read" => "Read own coastal trip suggestions",
                    "planner.workflows.read" => "Read own trip search progress",
                    "planner.itineraries.manage" => "Save, edit and review own coastal trips",
                    _ => "Read sourced biodiversity context" }, CreatedAt = DateTime.UtcNow };
            db.Permissions.Add(permission);
            permissions.Add(permission);
        }
        var traveller = await db.Roles.SingleOrDefaultAsync(r => r.Name == TravellerRole, ct);
        if (traveller is null)
        {
            traveller = new Role { Id = Guid.NewGuid(), Name = TravellerRole,
                Description = "Personal coastal planning; no administrative privileges", CreatedAt = DateTime.UtcNow };
            db.Roles.Add(traveller);
        }
        await db.SaveChangesAsync(ct);
        var roleIds = await db.Roles.Where(r => r.Name == "Admin" || r.Id == traveller.Id).Select(r => r.Id).ToListAsync(ct);
        foreach (var roleId in roleIds)
        foreach (var permission in permissions)
            if (!await db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permission.Id, ct))
                db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id, AssignedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
