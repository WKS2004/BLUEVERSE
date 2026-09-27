using System.Security.Cryptography;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Application;

public sealed record AssessmentEvidenceContent(byte[] Bytes, string MediaType);

public sealed class AssessmentEvidenceApplicationService(
    CoastalOperationsDbContext db,
    AssessmentEvidenceSanitizer sanitizer,
    IAssessmentEvidenceStorage storage,
    ILogger<AssessmentEvidenceApplicationService> logger)
{
    private const int MaximumEvidencePerAssessment = 5;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(365);

    public async Task<AssessmentEvidenceUploadResponse> UploadAsync(
        Guid assessmentId,
        Guid actorId,
        string correlationId,
        string? declaredContentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var assessment = await db.Assessments.SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken)
            ?? throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist.");
        if (assessment.InitiatedBy != actorId)
            throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist or is outside the caller's scope.");
        if (assessment.WorkflowStatus is not ("SUBMITTED" or "REVISION_REQUESTED"))
            throw Conflict("assessment_evidence_closed", "Evidence can no longer be added", "Evidence can be added only while the assessment is awaiting analysis or revision.");

        var count = await db.AssessmentEvidence.CountAsync(x => x.AssessmentId == assessmentId, cancellationToken);
        if (count >= MaximumEvidencePerAssessment)
            throw new CoastalOperationsException(StatusCodes.Status422UnprocessableEntity,
                "evidence_count_limit", "Evidence limit reached", "An assessment can contain at most five image attachments.");

        var sanitized = await sanitizer.SanitizePngAsync(content, declaredContentType, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var evidence = new AssessmentEvidence
        {
            Id = Guid.CreateVersion7(),
            AssessmentId = assessment.Id,
            AssessmentVersion = assessment.Version + 1,
            UploadedBy = actorId,
            MediaType = "image/png",
            ByteLength = sanitized.LongLength,
            ContentSha256 = Convert.ToHexString(SHA256.HashData(sanitized)).ToLowerInvariant(),
            InspectionStatus = "AVAILABLE",
            UploadedAt = now,
            ExpiresAt = now.Add(Retention)
        };

        try
        {
            await storage.StoreAsync(evidence.Id, sanitized, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Private Coastal Operations evidence storage is unavailable.");
            throw new CoastalOperationsException(StatusCodes.Status503ServiceUnavailable,
                "evidence_storage_unavailable", "Evidence storage is unavailable", "The image was not attached. Retry after the service is available.");
        }

        assessment.Version++;
        assessment.UpdatedAt = now;
        db.AssessmentEvidence.Add(evidence);
        db.OperationsAudit.Add(new OperationsAuditEntry
        {
            Id = Guid.CreateVersion7(), ResourceType = "evidence", ResourceId = evidence.Id,
            Action = "UPLOADED", ActorId = actorId, CorrelationId = correlationId, CreatedAt = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await DeleteStoredContentAsync(evidence.Id);
            throw Conflict("assessment_version_stale", "The assessment changed", "Reload the assessment before adding more evidence.");
        }
        catch
        {
            await DeleteStoredContentAsync(evidence.Id);
            throw;
        }

        return new AssessmentEvidenceUploadResponse(
            evidence.Id, assessment.Id, evidence.AssessmentVersion, evidence.MediaType,
            evidence.ByteLength, evidence.ContentSha256, evidence.InspectionStatus,
            evidence.UploadedAt, evidence.ExpiresAt);
    }

    public async Task<AssessmentEvidenceContent> GetContentAsync(
        Guid assessmentId,
        Guid evidenceId,
        Guid actorId,
        bool canReadQueue,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        var assessment = await db.Assessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null || (!canReadQueue && assessment.InitiatedBy != actorId))
            throw NotFound("assessment_not_found", "Assessment not found", "The assessment does not exist or is outside the caller's scope.");

        var evidence = await db.AssessmentEvidence.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == evidenceId && x.AssessmentId == assessmentId, cancellationToken)
            ?? throw NotFound("evidence_not_found", "Evidence not found", "The image does not exist or is outside the assessment scope.");
        if (evidence.InspectionStatus == "EXPIRED" || evidence.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new CoastalOperationsException(StatusCodes.Status410Gone,
                "evidence_expired", "Evidence has expired", "The private image retention period has ended.");

        byte[]? bytes;
        try
        {
            bytes = await storage.ReadAsync(evidence.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Private Coastal Operations evidence content is unavailable.");
            throw new CoastalOperationsException(StatusCodes.Status503ServiceUnavailable,
                "evidence_storage_unavailable", "Evidence storage is unavailable", "The image content is temporarily unavailable.");
        }
        if (bytes is null)
            throw new CoastalOperationsException(StatusCodes.Status503ServiceUnavailable,
                "evidence_storage_unavailable", "Evidence storage is unavailable", "The image content is temporarily unavailable.");
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (bytes.LongLength != evidence.ByteLength ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(digest),
                System.Text.Encoding.ASCII.GetBytes(evidence.ContentSha256)))
        {
            logger.LogError("Private evidence integrity validation failed for {EvidenceId}.", evidence.Id);
            throw new CoastalOperationsException(StatusCodes.Status503ServiceUnavailable,
                "evidence_integrity_failed", "Evidence content is unavailable", "The stored image did not pass its integrity check.");
        }
        return new AssessmentEvidenceContent(bytes, evidence.MediaType);
    }

    public async Task<int> ExpireBatchAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = await db.AssessmentEvidence
            .Where(x => x.InspectionStatus == "AVAILABLE" && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        var count = 0;
        foreach (var evidence in expired)
        {
            try
            {
                await storage.DeleteAsync(evidence.Id, cancellationToken);
                evidence.InspectionStatus = "EXPIRED";
                evidence.ExpiredAt = now;
                db.OperationsAudit.Add(new OperationsAuditEntry
                {
                    Id = Guid.CreateVersion7(), ResourceType = "evidence", ResourceId = evidence.Id,
                    Action = "EXPIRED", ActorId = Guid.Empty, CorrelationId = "evidence-retention", CreatedAt = now
                });
                count++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not expire private evidence {EvidenceId}; the retention job will retry.", evidence.Id);
            }
        }

        if (count > 0) await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task DeleteStoredContentAsync(Guid evidenceId)
    {
        try
        {
            await storage.DeleteAsync(evidenceId, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Could not clean up uncommitted Coastal Operations evidence {EvidenceId}.", evidenceId);
        }
    }

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new CoastalOperationsException(StatusCodes.Status401Unauthorized, "actor_invalid", "Authentication is required", "A valid authenticated actor ID is required.");
    }

    private static CoastalOperationsException NotFound(string code, string title, string detail) =>
        new(StatusCodes.Status404NotFound, code, title, detail);

    private static CoastalOperationsException Conflict(string code, string title, string detail) =>
        new(StatusCodes.Status409Conflict, code, title, detail);
}
