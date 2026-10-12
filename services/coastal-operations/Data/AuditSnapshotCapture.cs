using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Domain;
using Blueverse.CoastalOperations.Security;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Data;

internal static class AuditSnapshotCapture
{
    private static readonly HashSet<string> Fields = ["Title", "Objective", "Description", "TargetType", "TargetId", "SourceWorkflowId", "AssessmentId", "PeriodStartsAt", "PeriodEndsAt", "ValidFrom", "ValidUntil", "TimeZoneId", "Severity", "Visibility", "WorkflowStatus", "Lifecycle", "Version", "InspectionStatus", "ByteLength", "MediaType", "ExpiresAt", "RemovedAt", "Decision", "Explanation", "AppliedOperationalState", "WorkflowStatusAfterDecision", "ExpiredAt", "AppliedOperationalStateVersion", "ExpectedTargetStateVersion", "AiDependencyStatus", "AiDispatchOutcome", "AiDispatchRetryable"];

    public static void Capture(CoastalOperationsDbContext db, ClaimsPrincipal? principal)
    {
        foreach (var entry in db.ChangeTracker.Entries<OperationsAuditEntry>().Where(x => x.State == EntityState.Added))
        {
            var audit = entry.Entity;
            if (audit.Summary is not null) continue;
            if (audit.ActorId == Guid.Empty) { audit.ActorName = "System"; audit.ActorRolesJson = "[\"System\"]"; }
            else if (principal?.Identity?.IsAuthenticated == true && AuthenticatedActor.GetId(principal) == audit.ActorId)
            {
                var name = principal.FindFirstValue(ClaimTypes.Name) ?? principal.FindFirstValue("name");
                audit.ActorName = name is { Length: <= 100 } ? name : null;
                audit.ActorRolesJson = JsonSerializer.Serialize(principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("role")).Select(x => x.Value).Where(x => x.Length <= 128).Distinct().Order().Take(32));
            }
            var tracked = db.ChangeTracker.Entries().Where(x =>
                (audit.ResourceType == "assessment" && x.Entity is Assessment a && a.Id == audit.ResourceId) ||
                (audit.ResourceType == "alert" && x.Entity is OperationalAlert b && b.Id == audit.ResourceId) ||
                (audit.ResourceType == "evidence" && x.Entity is AssessmentEvidence c && c.Id == audit.ResourceId)).ToArray();
            var changes = new List<AuditFieldChange>();
            foreach (var record in tracked)
            {
                audit.RecordTitle = record.Entity switch { Assessment a => a.Title, OperationalAlert b => b.Title, AssessmentEvidence image => db.ChangeTracker.Entries<Assessment>().FirstOrDefault(x => x.Entity.Id == image.AssessmentId)?.Entity.Title, _ => null };
                foreach (var property in record.Properties.Where(x => Fields.Contains(x.Metadata.Name) && (record.State == EntityState.Added || x.IsModified)))
                {
                    var before = record.State == EntityState.Added ? null : Value(property.OriginalValue);
                    var after = Value(property.CurrentValue);
                    if (before != after) changes.Add(new(property.Metadata.Name, before, after));
                }
            }
            // Review explanations are business outcomes associated with this assessment event.
            foreach (var decision in db.ChangeTracker.Entries<ReviewerDecision>().Where(x => x.State == EntityState.Added && x.Entity.AssessmentId == audit.ResourceId))
                foreach (var property in decision.Properties.Where(x => Fields.Contains(x.Metadata.Name)))
                    changes.Add(new(property.Metadata.Name, null, Value(property.CurrentValue)));
            audit.ChangesJson = JsonSerializer.Serialize(changes, OperationsValidation.JsonOptions);
            var noun = audit.ResourceType == "evidence" ? "evidence image" : audit.ResourceType;
            var verb = audit.Action switch { "CREATED" => "Created a draft", "UPDATED" or "DRAFT_UPDATED" => "Updated the draft", "SUBMITTED" => "Published for assessment", "CANCELLED" => "Cancelled the draft", "WITHDRAWN" or "DRAFT_WITHDRAWN" => "Withdrew the draft", "PUBLISHED" or "PUBLISH" => "Published the advisory", "RESOLVED" or "RESOLVE" => "Resolved the advisory", "UPLOADED" => "Attached an evidence image", "REMOVED" => "Removed the draft evidence image", "EXPIRED" => "Expired the retained content", "DECISION_APPROVE" => "Approved the assessment recommendation", "DECISION_REJECT" => "Rejected the assessment recommendation", "DECISION_REQUEST_REVISION" => "Requested a revision of the recommendation", "STATE_CHANGED" => "Changed the coastal operational state", _ => audit.Action.ToLowerInvariant().Replace('_', ' ') };
            audit.Summary = $"{verb} ({noun})";
        }
    }

    private static string? Value(object? value) => value switch { null => null, DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture), _ => Convert.ToString(value, CultureInfo.InvariantCulture) };
}
