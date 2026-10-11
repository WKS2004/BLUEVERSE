using Blueverse.MarineSafety.Data;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Blueverse.MarineSafety.Services;
public sealed class ActivityService(MarineSafetyDbContext db, IExperienceCatalogueClient catalogue) : IActivityService
{
    public async Task<IReadOnlyList<MarineActivityDto>> GetActivitiesAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var published = await catalogue.GetPublishedAsync(cancellationToken);
        return isActive == false ? [] : published;
    }
    public async Task<MarineActivityDto?> GetActivityAsync(Guid id, CancellationToken cancellationToken) =>
        (await catalogue.GetPublishedAsync(cancellationToken)).SingleOrDefault(row => row.Id == id);
    public async Task<MarineActivity?> ResolvePublishedAsync(Guid id, CancellationToken cancellationToken)
    {
        var activity = await GetActivityAsync(id, cancellationToken);
        if (activity is null) return null;
        // Non-authoritative projection preserves profile FKs and historic references.
        var row = await db.MarineActivities.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (row is null)
        {
            row = new MarineActivity { Id = id, CreatedAt = activity.CreatedAt };
            db.MarineActivities.Add(row);
        }
        row.Name = activity.Name;
        row.ActivityType = activity.ActivityType;
        row.IsActive = true;
        row.UpdatedAt = activity.UpdatedAt;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_MarineActivities" })
        {
            db.Entry(row).State = EntityState.Detached;
            row = await db.MarineActivities.SingleAsync(a => a.Id == id, cancellationToken);
        }
        return row;
    }
}
