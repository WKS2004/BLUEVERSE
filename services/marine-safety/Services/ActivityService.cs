using Microsoft.EntityFrameworkCore;
using Blueverse.MarineSafety.Data;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Models;

namespace Blueverse.MarineSafety.Services;

/// <summary>
/// Marine activity reference-table management. This is the locally-owned
/// placeholder for Member 1's canonical coastal activity taxonomy: rows are
/// created, renamed, retyped and deactivated here until the canonical IDs
/// replace the table. Rows are never hard-deleted — profiles reference them
/// by cascade and assessments by restrict, so deactivation (soft-delete)
/// keeps the whole history interpretable.
/// </summary>
public sealed class ActivityService : IActivityService
{
    private readonly MarineSafetyDbContext _db;

    public ActivityService(MarineSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MarineActivityDto>> GetActivitiesAsync(
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = _db.MarineActivities.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        var activities = await query
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return activities.Select(ToDto).ToList();
    }

    public Task<MarineActivityDto?> GetActivityAsync(Guid id, CancellationToken cancellationToken) =>
        _db.MarineActivities
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => ToDto(a))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<MarineActivityDto> CreateActivityAsync(CreateActivityDto dto, CancellationToken cancellationToken)
    {
        var name = dto.Name!.Trim();
        var type = dto.ActivityType!.Trim();

        await EnsureNameAvailableAsync(name, null, cancellationToken);

        var entity = new MarineActivity
        {
            Id = Guid.NewGuid(),
            Name = name,
            ActivityType = type,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.MarineActivities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<MarineActivityDto?> UpdateActivityAsync(
        Guid id,
        UpdateActivityDto dto,
        CancellationToken cancellationToken)
    {
        var activity = await _db.MarineActivities
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (activity is null)
        {
            return null;
        }

        var name = dto.Name!.Trim();
        var type = dto.ActivityType!.Trim();

        await EnsureNameAvailableAsync(name, activity.Id, cancellationToken);

        activity.Name = name;
        activity.ActivityType = type;
        activity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(activity);
    }

    /// <summary>
    /// Deactivation is the delete surface: assessments retain a restrict
    /// reference to their activity and profiles reference it by cascade, so a
    /// hard delete is never offered. An inactive activity cannot receive new
    /// profiles or evaluations but keeps its full history.
    /// </summary>
    public async Task<bool> DeactivateActivityAsync(Guid id, CancellationToken cancellationToken)
    {
        var activity = await _db.MarineActivities.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (activity is null)
        {
            return false;
        }

        if (activity.IsActive)
        {
            activity.IsActive = false;
            activity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Names are unique inside the reference table. The conflict surfaces as a
    /// domain state conflict (409), not a lost-concurrency error.
    /// </summary>
    private async Task EnsureNameAvailableAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var taken = await _db.MarineActivities
            .AnyAsync(a => a.Name == name && (excludingId == null || a.Id != excludingId.Value), cancellationToken);

        if (taken)
        {
            throw new ActivityNameConflictException($"An activity named '{name}' already exists.");
        }
    }

    private static MarineActivityDto ToDto(MarineActivity activity) => new(
        activity.Id,
        activity.Name,
        activity.ActivityType,
        activity.IsActive,
        activity.CreatedAt,
        activity.UpdatedAt);
}

/// <summary>The requested activity name is already used by another reference row.</summary>
public sealed class ActivityNameConflictException(string message) : Exception(message);
