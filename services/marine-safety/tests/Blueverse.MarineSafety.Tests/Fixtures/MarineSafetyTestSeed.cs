using Microsoft.EntityFrameworkCore;
using Blueverse.MarineSafety.Authorization;

namespace Blueverse.MarineSafety.Tests.Fixtures;

/// <summary>
/// Deterministic identity and configuration rows used by the integration
/// tests. Permission grants register in the TestPermissionResolver double;
/// this fixture does not execute the production resolver against Auth's
/// PostgreSQL identity schema.
/// </summary>
public static class MarineSafetyTestSeed
{
    public static readonly Guid ReaderUserId = Guid.Parse("44444444-4444-4444-4444-444444444401");
    public static readonly Guid ManagerUserId = Guid.Parse("44444444-4444-4444-4444-444444444402");
    public static readonly Guid UnauthorizedUserId = Guid.Parse("44444444-4444-4444-4444-444444444403");
    public static readonly Guid ReviewerUserId = Guid.Parse("44444444-4444-4444-4444-444444444404");

    public static readonly Guid ActivityId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    public static readonly Guid UnprofiledActivityId = Guid.Parse("33333333-3333-3333-3333-333333333399");

    public static Dictionary<string, object?> ValidProfilePayload(
        Guid? activityId = null,
        decimal maxWindSpeed = 25m,
        decimal maxWaveHeight = 1.5m,
        decimal maxSwellHeight = 1.2m,
        decimal? cautionWindSpeed = null,
        decimal? cautionWaveHeight = null,
        decimal? cautionSwellHeight = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["maxWindSpeed"] = maxWindSpeed,
            ["maxWaveHeight"] = maxWaveHeight,
            ["maxSwellHeight"] = maxSwellHeight,
            ["windCriteriaSource"] = "Test safety standard, section 4",
            ["windCriteriaRationale"] = "This cited limit is used as deterministic test evidence.",
            ["waveCriteriaSource"] = "Test sea-state standard, section 2",
            ["waveCriteriaRationale"] = "This cited limit is used as deterministic test evidence.",
            ["swellCriteriaSource"] = "Test swell guidance, section 3",
            ["swellCriteriaRationale"] = "This cited limit is used as deterministic test evidence."
        };
        if (activityId.HasValue) payload["activityId"] = activityId.Value;
        if (cautionWindSpeed.HasValue) payload["cautionWindSpeed"] = cautionWindSpeed.Value;
        if (cautionWaveHeight.HasValue) payload["cautionWaveHeight"] = cautionWaveHeight.Value;
        if (cautionSwellHeight.HasValue) payload["cautionSwellHeight"] = cautionSwellHeight.Value;
        return payload;
    }

    public static Task SeedIdentitiesAsync(MarineSafetyDbContext db, TestPermissionResolver? resolver = null)
    {
        resolver ??= new TestPermissionResolver();
        resolver.Grant(ReaderUserId, PermissionCodes.MarineProfileRead);
        resolver.Grant(ManagerUserId, PermissionCodes.MarineProfileRead);
        resolver.Grant(ManagerUserId, PermissionCodes.MarineProfileManage);
        resolver.Grant(ReviewerUserId, PermissionCodes.MarineProfileRead);
        resolver.Grant(ReviewerUserId, PermissionCodes.MarineProfileManage);
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
        }

        if (!await db.MarineActivities.AnyAsync(a => a.Id == UnprofiledActivityId))
        {
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
                WindCriteriaSource = "Test safety standard, section 4",
                WindCriteriaRationale = "This cited limit is used as deterministic test evidence.",
                WaveCriteriaSource = "Test sea-state standard, section 2",
                WaveCriteriaRationale = "This cited limit is used as deterministic test evidence.",
                SwellCriteriaSource = "Test swell guidance, section 3",
                SwellCriteriaRationale = "This cited limit is used as deterministic test evidence.",
                CreatedByUserId = ManagerUserId,
                ReviewedByUserId = ReviewerUserId,
                ReviewedAt = DateTime.UtcNow,
                EffectiveFrom = DateTime.UtcNow,
                IsActive = true,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
