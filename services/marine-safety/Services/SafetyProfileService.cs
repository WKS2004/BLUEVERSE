using Microsoft.EntityFrameworkCore;
using Blueverse.MarineSafety.Data;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Models;
using Npgsql;

namespace Blueverse.MarineSafety.Services;

/// <summary>
/// Owns immutable profile versions and their review lifecycle. A new or
/// changed profile remains a draft until a different authorized manager
/// approves its cited wind, wave and swell criteria.
/// </summary>
public sealed class SafetyProfileService : ISafetyProfileService
{
    private readonly MarineSafetyDbContext _db;

    public SafetyProfileService(MarineSafetyDbContext db) => _db = db;

    public async Task<IReadOnlyList<SafetyProfileDto>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        var profiles = await _db.SafetyProfiles
            .AsNoTracking()
            .Include(profile => profile.Activity)
            .OrderBy(profile => profile.Activity!.Name)
            .ThenByDescending(profile => profile.Version)
            .ToListAsync(cancellationToken);
        return profiles.Select(ToDto).ToList();
    }

    public Task<SafetyProfileDto?> GetProfileAsync(Guid id, CancellationToken cancellationToken) =>
        _db.SafetyProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == id)
            .Select(profile => ToDto(profile, profile.Activity!.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<SafetyProfileDto?> GetProfileForActivityAsync(Guid activityId, CancellationToken cancellationToken) =>
        _db.SafetyProfiles
            .AsNoTracking()
            .Where(profile => profile.ActivityId == activityId && profile.IsActive)
            .Select(profile => ToDto(profile, profile.Activity!.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SafetyProfileDto> CreateProfileAsync(
        CreateSafetyProfileDto dto,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ValidateCautionBands(dto.MaxWindSpeed, dto.MaxWaveHeight, dto.MaxSwellHeight, dto);
        ValidateCriteria(dto.WindCriteriaSource, dto.WindCriteriaRationale, "wind");
        ValidateCriteria(dto.WaveCriteriaSource, dto.WaveCriteriaRationale, "wave");
        ValidateCriteria(dto.SwellCriteriaSource, dto.SwellCriteriaRationale, "swell");

        var activity = await _db.MarineActivities
            .SingleOrDefaultAsync(item => item.Id == dto.ActivityId!.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Activity {dto.ActivityId} does not exist.");
        if (!activity.IsActive)
        {
            throw new InvalidOperationException($"Activity '{activity.Name}' is not active.");
        }

        var highestVersion = await _db.SafetyProfiles
            .Where(profile => profile.ActivityId == activity.Id)
            .MaxAsync(profile => (int?)profile.Version, cancellationToken) ?? 0;
        var now = DateTime.UtcNow;
        var draft = new SafetyProfile
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            Activity = activity,
            MaxWindSpeed = dto.MaxWindSpeed,
            MaxWaveHeight = dto.MaxWaveHeight,
            MaxSwellHeight = dto.MaxSwellHeight,
            CautionWindSpeed = dto.CautionWindSpeed,
            CautionWaveHeight = dto.CautionWaveHeight,
            CautionSwellHeight = dto.CautionSwellHeight,
            WindCriteriaSource = dto.WindCriteriaSource!.Trim(),
            WindCriteriaRationale = dto.WindCriteriaRationale!.Trim(),
            WaveCriteriaSource = dto.WaveCriteriaSource!.Trim(),
            WaveCriteriaRationale = dto.WaveCriteriaRationale!.Trim(),
            SwellCriteriaSource = dto.SwellCriteriaSource!.Trim(),
            SwellCriteriaRationale = dto.SwellCriteriaRationale!.Trim(),
            CreatedByUserId = actorUserId,
            IsActive = false,
            Version = highestVersion + 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.SafetyProfiles.Add(draft);
        await SaveDraftAsync(cancellationToken);
        return ToDto(draft);
    }

    public async Task<SafetyProfileDto?> UpdateProfileAsync(
        Guid id,
        UpdateSafetyProfileDto dto,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ValidateCautionBands(dto.MaxWindSpeed, dto.MaxWaveHeight, dto.MaxSwellHeight, dto);
        ValidateCriteria(dto.WindCriteriaSource, dto.WindCriteriaRationale, "wind");
        ValidateCriteria(dto.WaveCriteriaSource, dto.WaveCriteriaRationale, "wave");
        ValidateCriteria(dto.SwellCriteriaSource, dto.SwellCriteriaRationale, "swell");

        var source = await _db.SafetyProfiles
            .AsNoTracking()
            .Include(profile => profile.Activity)
            .SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var highestVersion = await _db.SafetyProfiles
            .Where(profile => profile.ActivityId == source.ActivityId)
            .MaxAsync(profile => (int?)profile.Version, cancellationToken) ?? source.Version;
        var now = DateTime.UtcNow;
        var draft = new SafetyProfile
        {
            Id = Guid.NewGuid(),
            ActivityId = source.ActivityId,
            MaxWindSpeed = dto.MaxWindSpeed,
            MaxWaveHeight = dto.MaxWaveHeight,
            MaxSwellHeight = dto.MaxSwellHeight,
            CautionWindSpeed = dto.CautionWindSpeed,
            CautionWaveHeight = dto.CautionWaveHeight,
            CautionSwellHeight = dto.CautionSwellHeight,
            WindCriteriaSource = dto.WindCriteriaSource!.Trim(),
            WindCriteriaRationale = dto.WindCriteriaRationale!.Trim(),
            WaveCriteriaSource = dto.WaveCriteriaSource!.Trim(),
            WaveCriteriaRationale = dto.WaveCriteriaRationale!.Trim(),
            SwellCriteriaSource = dto.SwellCriteriaSource!.Trim(),
            SwellCriteriaRationale = dto.SwellCriteriaRationale!.Trim(),
            CreatedByUserId = actorUserId,
            IsActive = false,
            Version = highestVersion + 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.SafetyProfiles.Add(draft);
        await SaveDraftAsync(cancellationToken);
        return ToDto(draft, source.Activity?.Name ?? string.Empty);
    }

    public async Task<SafetyProfileDto?> ReviewProfileAsync(
        Guid id,
        Guid reviewerUserId,
        CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // An execution-strategy retry starts from database state, not
            // objects mutated by a transaction whose commit failed.
            _db.ChangeTracker.Clear();
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var profile = await _db.SafetyProfiles
                .Include(item => item.Activity)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (profile is null)
            {
                return null;
            }

            if (profile.ReviewedAt.HasValue)
            {
                if (profile.IsActive && profile.ReviewedByUserId == reviewerUserId)
                {
                    return ToDto(profile);
                }
                throw new SafetyProfileReviewConflictException("This profile version has already been reviewed.");
            }

            if (profile.CreatedByUserId == reviewerUserId)
            {
                throw new SafetyProfileReviewConflictException("A different authorized manager must review this profile version.");
            }

            ValidateCriteria(profile.WindCriteriaSource, profile.WindCriteriaRationale, "wind");
            ValidateCriteria(profile.WaveCriteriaSource, profile.WaveCriteriaRationale, "wave");
            ValidateCriteria(profile.SwellCriteriaSource, profile.SwellCriteriaRationale, "swell");
            if (profile.Activity is null || !profile.Activity.IsActive)
            {
                throw new SafetyProfileReviewConflictException("A profile for an inactive activity cannot be approved.");
            }

            var now = DateTime.UtcNow;
            var activeVersions = await _db.SafetyProfiles
                .Where(item => item.ActivityId == profile.ActivityId && item.IsActive && item.Id != profile.Id)
                .ToListAsync(cancellationToken);
            foreach (var active in activeVersions)
            {
                active.IsActive = false;
                active.EffectiveTo = now;
                active.UpdatedAt = now;
            }

            // PostgreSQL checks the partial unique index per statement. Save
            // the superseded version before activating the new one, within the
            // same transaction so an error restores the old active version.
            if (activeVersions.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            profile.IsActive = true;
            profile.ReviewedByUserId = reviewerUserId;
            profile.ReviewedAt = now;
            profile.EffectiveFrom = now;
            profile.UpdatedAt = now;
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsActiveProfileUniqueViolation(exception))
            {
                throw new SafetyProfileReviewConflictException("Another profile version was approved concurrently. Refresh and review the pending version again.");
            }
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return ToDto(profile);
        });
    }

    public async Task<bool> DeactivateProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _db.SafetyProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null || !profile.IsActive)
        {
            return profile is not null;
        }

        var now = DateTime.UtcNow;
        profile.IsActive = false;
        profile.EffectiveTo = now;
        profile.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateCautionBands(decimal maxWind, decimal maxWave, decimal maxSwell, object dto)
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
            throw new InvalidOperationException("Caution wind speed must be below the maximum wind speed.");
        if (cautionWave.HasValue && cautionWave.Value >= maxWave)
            throw new InvalidOperationException("Caution wave height must be below the maximum wave height.");
        if (cautionSwell.HasValue && cautionSwell.Value >= maxSwell)
            throw new InvalidOperationException("Caution swell height must be below the maximum swell height.");
    }

    private static void ValidateCriteria(string? source, string? rationale, string factor)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Trim().Length is < 3 or > 512)
            throw new InvalidOperationException($"A source of 3 to 512 characters is required for the {factor} limit.");
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length is < 10 or > 2000)
            throw new InvalidOperationException($"A rationale of 10 to 2000 characters is required for the {factor} limit.");
    }

    private async Task SaveDraftAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsVersionUniqueViolation(exception))
        {
            throw new SafetyProfileVersionConflictException("Another profile version was created concurrently. Refresh and save the draft again.");
        }
    }

    private static bool IsVersionUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_SafetyProfiles_ActivityId_Version"
        };

    private static bool IsActiveProfileUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_SafetyProfiles_ActivityId_IsActive"
        };

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
        profile.WindCriteriaSource,
        profile.WindCriteriaRationale,
        profile.WaveCriteriaSource,
        profile.WaveCriteriaRationale,
        profile.SwellCriteriaSource,
        profile.SwellCriteriaRationale,
        profile.IsActive,
        profile.Version,
        profile.CreatedAt,
        profile.UpdatedAt,
        profile.CreatedByUserId,
        profile.ReviewedByUserId,
        profile.ReviewedAt,
        profile.EffectiveFrom,
        profile.EffectiveTo);
}

public sealed class SafetyProfileReviewConflictException : Exception
{
    public SafetyProfileReviewConflictException(string message) : base(message) { }
}

public sealed class SafetyProfileVersionConflictException : Exception
{
    public SafetyProfileVersionConflictException(string message) : base(message) { }
}
