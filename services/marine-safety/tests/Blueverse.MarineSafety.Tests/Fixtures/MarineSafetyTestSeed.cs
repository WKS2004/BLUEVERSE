using Microsoft.EntityFrameworkCore;
using Blueverse.MarineSafety.Authorization;

namespace Blueverse.MarineSafety.Tests.Fixtures;

/// <summary>
/// Deterministic identity and configuration rows used by the integration
/// tests. Permission grants register in the TestPermissionResolver double;
/// the production IdentityPermissionResolver over the shared identity schema
/// is exercised by Auth's own permission tests and PostgreSQL evidence.
/// </summary>
public static class MarineSafetyTestSeed
{
    public static readonly Guid ReaderUserId = Guid.Parse("44444444-4444-4444-4444-444444444401");
    public static readonly Guid ManagerUserId = Guid.Parse("44444444-4444-4444-4444-444444444402");
    public static readonly Guid UnauthorizedUserId = Guid.Parse("44444444-4444-4444-4444-444444444403");

    public static readonly Guid ActivityId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    public static readonly Guid UnprofiledActivityId = Guid.Parse("33333333-3333-3333-3333-333333333399");

    public static Task SeedIdentitiesAsync(MarineSafetyDbContext db, TestPermissionResolver? resolver = null)
    {
        resolver ??= new TestPermissionResolver();
        resolver.Grant(ReaderUserId, PermissionCodes.MarineProfileRead);
        resolver.Grant(ManagerUserId, PermissionCodes.MarineProfileRead);
        resolver.Grant(ManagerUserId, PermissionCodes.MarineProfileManage);
        resolver.Grant(UnauthorizedUserId, "auth.user.read");
        return Task.CompletedTask;
    }

    public static async Task SeedConfigurationAsync(MarineSafetyDbContext db)
    {
        if (!await db.MarineActivities.AnyAsync(a => a.Id == ActivityId))
        {
            db.MarineActivities.Add(new MarineActivity
            {
                Id = ActivityId,
                Name = "Surfing",
                ActivityType = "Surfing",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            db.MarineActivities.Add(new MarineActivity
            {
                Id = UnprofiledActivityId,
                Name = "Freediving (no profile yet)",
                ActivityType = "Diving",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await db.SafetyProfiles.AnyAsync(p => p.ActivityId == ActivityId && p.IsActive))
        {
            db.SafetyProfiles.Add(new SafetyProfile
            {
                Id = Guid.NewGuid(),
                ActivityId = ActivityId,
                MaxWindSpeed = 25m,
                MaxWaveHeight = 1.5m,
                MaxSwellHeight = 1.2m,
                CautionWindSpeed = 20m,
                CautionWaveHeight = 1.0m,
                IsActive = true,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
