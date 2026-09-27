using Microsoft.EntityFrameworkCore;
using Blueverse.MarineSafety.Data;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Models;

namespace Blueverse.MarineSafety.Services;

/// <summary>
/// Safety profile management. Profile changes are permission-gated at the
/// controller; this service enforces the domain rules: activity must exist and
/// be active, limits must be positive, optional caution limits must sit below
/// their hard limit, and an activity keeps exactly one active profile
/// (creating a new active profile supersedes the previous one by deactivating
/// it while preserving history).
/// </summary>
public sealed class SafetyProfileService : ISafetyProfileService
{
    private readonly MarineSafetyDbContext _db;

    public SafetyProfileService(MarineSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SafetyProfileDto>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        var profiles = await _db.SafetyProfiles
            .AsNoTracking()
            .Include(p => p.Activity)
            .OrderBy(p => p.Activity!.Name)
            .ThenByDescending(p => p.Version)
            .ToListAsync(cancellationToken);

        return profiles.Select(ToDto).ToList();
    }

    public Task<SafetyProfileDto?> GetProfileAsync(Guid id, CancellationToken cancellationToken) =>
        _db.SafetyProfiles
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => ToDto(p, p.Activity!.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<SafetyProfileDto?> GetProfileForActivityAsync(Guid activityId, CancellationToken cancellationToken) =>
        _db.SafetyProfiles
            .AsNoTracking()
            .Where(p => p.ActivityId == activityId && p.IsActive)
            .Select(p => ToDto(p, p.Activity!.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SafetyProfileDto> CreateProfileAsync(CreateSafetyProfileDto dto, CancellationToken cancellationToken)
    {
        ValidateCautionBands(dto.MaxWindSpeed, dto.MaxWaveHeight, dto.MaxSwellHeight, dto);

        var activity = await _db.MarineActivities
            .SingleOrDefaultAsync(a => a.Id == dto.ActivityId!.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Activity {dto.ActivityId} does not exist.");

        if (!activity.IsActive)
        {
            throw new InvalidOperationException($"Activity '{activity.Name}' is not active.");
        }

        // Supersede the previous active profile; history rows remain.
        var existing = await _db.SafetyProfiles
            .Where(p => p.ActivityId == dto.ActivityId && p.IsActive)
            .ToListAsync(cancellationToken);

        var nextVersion = 1;
        if (existing.Count > 0)
        {
            nextVersion = existing.Max(p => p.Version) + 1;
            foreach (var profile in existing)
            {
                profile.IsActive = false;
            }
        }

        var entity = new SafetyProfile
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            MaxWindSpeed = dto.MaxWindSpeed,
            MaxWaveHeight = dto.MaxWaveHeight,
            MaxSwellHeight = dto.MaxSwellHeight,
            CautionWindSpeed = dto.CautionWindSpeed,
            CautionWaveHeight = dto.CautionWaveHeight,
            CautionSwellHeight = dto.CautionSwellHeight,
            IsActive = true,
            Version = nextVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SafetyProfiles.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(entity, activity.Name);
    }

    public async Task<SafetyProfileDto?> UpdateProfileAsync(Guid id, UpdateSafetyProfileDto dto, CancellationToken cancellationToken)
    {
        ValidateCautionBands(dto.MaxWindSpeed, dto.MaxWaveHeight, dto.MaxSwellHeight, dto);

        var profile = await _db.SafetyProfiles
            .Include(p => p.Activity)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        profile.MaxWindSpeed = dto.MaxWindSpeed;
        profile.MaxWaveHeight = dto.MaxWaveHeight;
        profile.MaxSwellHeight = dto.MaxSwellHeight;
        profile.CautionWindSpeed = dto.CautionWindSpeed;
        profile.CautionWaveHeight = dto.CautionWaveHeight;
        profile.CautionSwellHeight = dto.CautionSwellHeight;
        profile.IsActive = dto.IsActive;
        profile.Version++;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    /// <summary>
    /// Profiles are deactivated rather than deleted: assessments retain a
    /// reference to the profile version that produced them.
    /// </summary>
    public async Task<bool> DeactivateProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _db.SafetyProfiles.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (profile is null || !profile.IsActive)
        {
            return profile is not null && !profile.IsActive;
        }

        profile.IsActive = false;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateCautionBands(
        decimal maxWind,
        decimal maxWave,
        decimal maxSwell,
        object dto)
    {
        decimal? cautionWind = dto switch
        {
            CreateSafetyProfileDto create => create.CautionWindSpeed,
            UpdateSafetyProfileDto update => update.CautionWindSpeed,
            _ => null
        };
        decimal? cautionWave = dto switch
        {
            CreateSafetyProfileDto create => create.CautionWaveHeight,
            UpdateSafetyProfileDto update => update.CautionWaveHeight,
            _ => null
        };
        decimal? cautionSwell = dto switch
        {
            CreateSafetyProfileDto create => create.CautionSwellHeight,
            UpdateSafetyProfileDto update => update.CautionSwellHeight,
            _ => null
        };

        if (cautionWind.HasValue && cautionWind.Value >= maxWind)
        {
            throw new InvalidOperationException("Caution wind speed must be below the maximum wind speed.");
        }

        if (cautionWave.HasValue && cautionWave.Value >= maxWave)
        {
            throw new InvalidOperationException("Caution wave height must be below the maximum wave height.");
        }

        if (cautionSwell.HasValue && cautionSwell.Value >= maxSwell)
        {
            throw new InvalidOperationException("Caution swell height must be below the maximum swell height.");
        }
    }

    private static SafetyProfileDto ToDto(SafetyProfile profile) =>
        ToDto(profile, profile.Activity?.Name ?? string.Empty);

    private static SafetyProfileDto ToDto(SafetyProfile profile, string activityName) => new(
        profile.Id,
        profile.ActivityId,
        activityName,
        profile.MaxWindSpeed,
        profile.MaxWaveHeight,
        profile.MaxSwellHeight,
        profile.CautionWindSpeed,
        profile.CautionWaveHeight,
        profile.CautionSwellHeight,
        profile.IsActive,
        profile.Version,
        profile.CreatedAt,
        profile.UpdatedAt);
}
