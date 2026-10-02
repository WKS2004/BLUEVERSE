using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Globalization;

namespace Blueverse.CoastalOperations.Application;

public sealed class OperationsAuditReader(CoastalOperationsDbContext db)
{
    public async Task<OperationsAuditPage> GetAssessmentAsync(
        Guid id, Guid actorId, bool canReadQueue, AuditListQuery query, CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var record = await db.Assessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null || (record.InitiatedBy != actorId && (!canReadQueue || record.WorkflowStatus == "DRAFT")))
            throw Missing();
        var evidenceIds = db.AssessmentEvidence.Where(x => x.AssessmentId == id).Select(x => x.Id);
        var events = db.OperationsAudit.AsNoTracking().Where(x =>
            (x.ResourceType == "assessment" && x.ResourceId == id) ||
            (x.ResourceType == "evidence" && evidenceIds.Contains(x.ResourceId)));
        return await PageAsync(events, query, cancellationToken);
    }

    public async Task<OperationsAuditPage> GetAlertAsync(
        Guid id, Guid actorId, bool canManage, AuditListQuery query, CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var record = await db.OperationalAlerts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null || (!canManage && record.CreatedBy != actorId)) throw Missing();
        return await PageAsync(db.OperationsAudit.AsNoTracking().Where(x => x.ResourceType == "alert" && x.ResourceId == id), query, cancellationToken);
    }

    private static async Task<OperationsAuditPage> PageAsync(
        IQueryable<OperationsAuditEntry> events, AuditListQuery query, CancellationToken cancellationToken)
    {
        CoastalRecordQueries.Search(null, query.PageSize);
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            DateTimeOffset time;
            Guid id;
            try
            {
                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(query.Cursor)).Split('|');
                if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                    !Guid.TryParseExact(parts[1], "N", out id) || id == Guid.Empty) throw new FormatException();
                time = new DateTimeOffset(ticks, TimeSpan.Zero);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
            { throw new CoastalOperationsException(422, "cursor_invalid", "The activity cursor is invalid", "Use the cursor returned by the previous page."); }
            events = events.Where(x => x.CreatedAt < time || (x.CreatedAt == time && x.Id.CompareTo(id) < 0));
        }
        var page = await events.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(query.PageSize + 1).ToListAsync(cancellationToken);
        var more = page.Count > query.PageSize;
        if (more) page.RemoveAt(page.Count - 1);
        return new OperationsAuditPage(page.Select(x => new OperationsAuditResponse(
            x.Id, x.ResourceType, x.ResourceId, x.Action, x.ActorId, x.CorrelationId, x.CreatedAt, x.ActorName,
            System.Text.Json.JsonSerializer.Deserialize<string[]>(x.ActorRolesJson), x.RecordTitle, x.Summary,
            System.Text.Json.JsonSerializer.Deserialize<AuditFieldChange[]>(x.ChangesJson, OperationsValidation.JsonOptions))).ToArray(),
            more ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{page[^1].CreatedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{page[^1].Id:N}")) : null);
    }

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty) throw new CoastalOperationsException(401, "actor_invalid", "Authentication is required", "A valid authenticated actor ID is required.");
    }
    private static CoastalOperationsException Missing() => new(404, "record_not_found", "Record not found", "This record is unavailable or outside your access.");
}
