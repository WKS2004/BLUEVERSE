using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blueverse.CoastalOperations.Tests;

public sealed class AssessmentEvidenceApplicationServiceTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");

    [Fact(DisplayName = "COASTAL-EVIDENCE-006 valid owner upload stores sanitized bytes and versioned audit metadata")]
    [Trait("TestId", "COASTAL-EVIDENCE-006")]
    public async Task UploadPersistsDigestAndAdvancesAssessmentVersion()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();
        var input = PngWithTextChunk();

        var result = await CreateService(db, storage).UploadAsync(
            assessment.Id, owner, "evidence-upload", "image/png", new MemoryStream(input), CancellationToken.None);
        var savedEvidence = await db.AssessmentEvidence.SingleAsync();
        var savedAssessment = await db.Assessments.SingleAsync();
        var storedBytes = await storage.ReadAsync(result.EvidenceId, CancellationToken.None);

        Assert.Equal(2, result.AssessmentVersion);
        Assert.Equal("image/png", result.MediaType);
        Assert.Equal("AVAILABLE", result.InspectionStatus);
        Assert.Equal(storedBytes!.LongLength, result.ByteLength);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(storedBytes)).ToLowerInvariant(), result.ContentSha256);
        Assert.Equal(result.ContentSha256, savedEvidence.ContentSha256);
        Assert.Equal(result.UploadedAt.AddDays(365), result.ExpiresAt);
        Assert.Equal(2, savedAssessment.Version);
        Assert.Equal(owner, savedEvidence.UploadedBy);
        Assert.Equal("UPLOADED", (await db.OperationsAudit.SingleAsync()).Action);
        Assert.Equal("evidence-upload", (await db.OperationsAudit.SingleAsync()).CorrelationId);
        Assert.False(ContainsBytes(storedBytes, System.Text.Encoding.ASCII.GetBytes("tEXt")));
        Assert.Equal(1, storage.StoreCalls);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-007 uploads reject anonymous, missing and out-of-scope assessments")]
    [Trait("TestId", "COASTAL-EVIDENCE-007")]
    public async Task UploadEnforcesActorAndAssessmentScope()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();
        var service = CreateService(db, storage);

        var anonymous = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.UploadAsync(
            assessment.Id, Guid.Empty, "corr", "image/png", new MemoryStream(ValidPng()), CancellationToken.None));
        var missing = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.UploadAsync(
            Guid.NewGuid(), owner, "corr", "image/png", new MemoryStream(ValidPng()), CancellationToken.None));
        var otherOwner = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.UploadAsync(
            assessment.Id, Guid.NewGuid(), "corr", "image/png", new MemoryStream(ValidPng()), CancellationToken.None));

        Assert.Equal(StatusCodes.Status401Unauthorized, anonymous.StatusCode);
        Assert.Equal("actor_invalid", anonymous.Code);
        Assert.Equal(StatusCodes.Status404NotFound, missing.StatusCode);
        Assert.Equal("assessment_not_found", missing.Code);
        Assert.Equal(StatusCodes.Status404NotFound, otherOwner.StatusCode);
        Assert.Equal("assessment_not_found", otherOwner.Code);
        Assert.Empty(await db.AssessmentEvidence.ToListAsync());
        Assert.Empty(await db.OperationsAudit.ToListAsync());
        Assert.Equal(0, storage.StoreCalls);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-008 uploads are closed after assessment leaves submitted or revision state")]
    [Trait("TestId", "COASTAL-EVIDENCE-008")]
    public async Task UploadRejectsClosedAssessment()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        assessment.WorkflowStatus = "REJECTED";
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => CreateService(db, storage).UploadAsync(
            assessment.Id, owner, "corr", "image/png", new MemoryStream(ValidPng()), CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("assessment_evidence_closed", exception.Code);
        Assert.Empty(await db.AssessmentEvidence.ToListAsync());
        Assert.Equal(0, storage.StoreCalls);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-009 the sixth attachment is rejected before storage")]
    [Trait("TestId", "COASTAL-EVIDENCE-009")]
    public async Task UploadEnforcesFiveAttachmentLimit()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        for (var index = 0; index < 5; index++)
            db.AssessmentEvidence.Add(Evidence(assessment.Id, owner, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1)));
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => CreateService(db, storage).UploadAsync(
            assessment.Id, owner, "corr", "image/png", new MemoryStream(ValidPng()), CancellationToken.None));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal("evidence_count_limit", exception.Code);
        Assert.Equal(5, await db.AssessmentEvidence.CountAsync());
        Assert.Equal(1, assessment.Version);
        Assert.Equal(0, storage.StoreCalls);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-010 file storage failure leaves no database metadata or version change")]
    [Trait("TestId", "COASTAL-EVIDENCE-010")]
    public async Task UploadMapsStorageFailureAndDoesNotPersist()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage { StoreException = new IOException("Synthetic unavailable storage") };

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => CreateService(db, storage).UploadAsync(
            assessment.Id, owner, "corr", "image/png", new MemoryStream(ValidPng()), CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
        Assert.Equal("evidence_storage_unavailable", exception.Code);
        Assert.Equal(1, assessment.Version);
        Assert.Empty(await db.AssessmentEvidence.ToListAsync());
        Assert.Empty(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-011 persistence failure cleans up previously written private content")]
    [Trait("TestId", "COASTAL-EVIDENCE-011")]
    public async Task UploadCleansStoredFileWhenMetadataSaveFails()
    {
        var interceptor = new FailSaveChangesInterceptor();
        var options = new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-evidence-save-failure-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new CoastalOperationsDbContext(options);
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();
        interceptor.ShouldFail = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(db, storage).UploadAsync(
            assessment.Id, owner, "corr", "image/png", new MemoryStream(ValidPng()), CancellationToken.None));

        Assert.Equal("Synthetic metadata-store failure", exception.Message);
        Assert.Equal(1, storage.StoreCalls);
        Assert.Equal(1, storage.DeleteCalls);
        Assert.Empty(storage.Content);
        await db.Entry(assessment).ReloadAsync();
        Assert.Equal(1, assessment.Version);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-012 owner and reviewer can read bytes only within assessment scope")]
    [Trait("TestId", "COASTAL-EVIDENCE-012")]
    public async Task ReadChecksScopeAndReturnsVerifiedContent()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        var evidenceId = Guid.NewGuid();
        var content = ValidPng();
        db.AssessmentEvidence.Add(Evidence(assessment.Id, owner, evidenceId, DateTimeOffset.UtcNow.AddDays(1), content));
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();
        storage.Content[evidenceId] = content;
        var service = CreateService(db, storage);

        var ownerRead = await service.GetContentAsync(assessment.Id, evidenceId, owner, false, CancellationToken.None);
        var reviewerRead = await service.GetContentAsync(assessment.Id, evidenceId, Guid.NewGuid(), true, CancellationToken.None);
        var denied = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetContentAsync(
            assessment.Id, evidenceId, Guid.NewGuid(), false, CancellationToken.None));
        var wrongAssessment = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetContentAsync(
            assessment.Id, Guid.NewGuid(), owner, true, CancellationToken.None));

        Assert.Equal("image/png", ownerRead.MediaType);
        Assert.Equal(content, ownerRead.Bytes);
        Assert.Equal(content, reviewerRead.Bytes);
        Assert.Equal(StatusCodes.Status404NotFound, denied.StatusCode);
        Assert.Equal("assessment_not_found", denied.Code);
        Assert.Equal("evidence_not_found", wrongAssessment.Code);
        Assert.Equal(2, storage.ReadCalls);
    }

    [Theory(DisplayName = "COASTAL-EVIDENCE-013 expired evidence is not served")]
    [Trait("TestId", "COASTAL-EVIDENCE-013")]
    [InlineData("AVAILABLE", true)]
    [InlineData("EXPIRED", false)]
    public async Task ExpiredEvidenceReturnsGoneWithoutReadingStorage(string inspectionStatus, bool expiresInPast)
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        var evidenceId = Guid.NewGuid();
        var content = ValidPng();
        var expiry = DateTimeOffset.UtcNow.AddDays(expiresInPast ? -1 : 1);
        var evidence = Evidence(assessment.Id, owner, evidenceId, expiry, content);
        evidence.InspectionStatus = inspectionStatus;
        await db.AssessmentEvidence.AddAsync(evidence);
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();
        storage.Content[evidenceId] = content;

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => CreateService(db, storage).GetContentAsync(
            assessment.Id, evidenceId, owner, false, CancellationToken.None));

        Assert.Equal(StatusCodes.Status410Gone, exception.StatusCode);
        Assert.Equal("evidence_expired", exception.Code);
        Assert.Equal(0, storage.ReadCalls);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-014 missing, inaccessible and altered stored bytes fail safely")]
    [Trait("TestId", "COASTAL-EVIDENCE-014")]
    public async Task ReadRejectsUnavailableOrCorruptContent()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        var absentId = Guid.NewGuid();
        var corruptId = Guid.NewGuid();
        var expectedBytes = ValidPng();
        db.AssessmentEvidence.AddRange(
            Evidence(assessment.Id, owner, absentId, DateTimeOffset.UtcNow.AddDays(1), expectedBytes),
            Evidence(assessment.Id, owner, corruptId, DateTimeOffset.UtcNow.AddDays(1), expectedBytes));
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage { ReadException = null };
        var tampered = expectedBytes.ToArray();
        tampered[^1] ^= 0x01;
        storage.Content[corruptId] = tampered;
        var service = CreateService(db, storage);

        var absent = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetContentAsync(
            assessment.Id, absentId, owner, false, CancellationToken.None));
        var corrupt = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetContentAsync(
            assessment.Id, corruptId, owner, false, CancellationToken.None));
        storage.ReadException = new IOException("Synthetic read failure");
        var inaccessible = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetContentAsync(
            assessment.Id, corruptId, owner, false, CancellationToken.None));

        Assert.All(new[] { absent, corrupt, inaccessible }, exception =>
        {
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
            Assert.Contains(exception.Code, new[] { "evidence_storage_unavailable", "evidence_integrity_failed" });
        });
        Assert.Equal("evidence_storage_unavailable", absent.Code);
        Assert.Equal("evidence_integrity_failed", corrupt.Code);
        Assert.Equal("evidence_storage_unavailable", inaccessible.Code);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-015 retention expires at most fifty due items and leaves later items for the next pass")]
    [Trait("TestId", "COASTAL-EVIDENCE-015")]
    public async Task RetentionUsesBoundedBatchesAndAuditsExpiry()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        var now = DateTimeOffset.UtcNow;
        var due = Enumerable.Range(0, 51)
            .Select(index => Evidence(assessment.Id, owner, Guid.NewGuid(), now.AddMinutes(-index - 1)))
            .ToArray();
        var future = Evidence(assessment.Id, owner, Guid.NewGuid(), now.AddDays(1));
        db.AssessmentEvidence.AddRange(due);
        db.AssessmentEvidence.Add(future);
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();
        foreach (var evidence in due.Append(future)) storage.Content[evidence.Id] = [1];
        var service = CreateService(db, storage);

        var firstBatch = await service.ExpireBatchAsync(now, CancellationToken.None);
        var remainingDue = await db.AssessmentEvidence.CountAsync(item => item.InspectionStatus == "AVAILABLE" && item.ExpiresAt <= now);
        var secondBatch = await service.ExpireBatchAsync(now, CancellationToken.None);

        Assert.Equal(50, firstBatch);
        Assert.Equal(1, remainingDue);
        Assert.Equal(1, secondBatch);
        Assert.Equal(51, await db.AssessmentEvidence.CountAsync(item => item.InspectionStatus == "EXPIRED"));
        Assert.Equal(51, await db.OperationsAudit.CountAsync(item => item.Action == "EXPIRED"));
        Assert.Equal(1, await db.AssessmentEvidence.CountAsync(item => item.InspectionStatus == "AVAILABLE"));
        Assert.DoesNotContain(due.Select(item => item.Id), id => storage.Content.ContainsKey(id));
        Assert.Contains(future.Id, storage.Content.Keys);
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-016 storage delete failure leaves evidence eligible for retention retry")]
    [Trait("TestId", "COASTAL-EVIDENCE-016")]
    public async Task RetentionFailureIsRetryableAndDoesNotMarkEvidenceExpired()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var assessment = AddAssessment(db, owner);
        var now = DateTimeOffset.UtcNow;
        var failedId = Guid.NewGuid();
        var successfulId = Guid.NewGuid();
        db.AssessmentEvidence.AddRange(
            Evidence(assessment.Id, owner, failedId, now.AddMinutes(-2)),
            Evidence(assessment.Id, owner, successfulId, now.AddMinutes(-1)));
        await db.SaveChangesAsync();
        var storage = new MemoryEvidenceStorage();
        storage.Content[failedId] = [1];
        storage.Content[successfulId] = [2];
        storage.DeleteFailures.Add(failedId);

        var count = await CreateService(db, storage).ExpireBatchAsync(now, CancellationToken.None);
        var failedEvidence = await db.AssessmentEvidence.SingleAsync(item => item.Id == failedId);
        var successfulEvidence = await db.AssessmentEvidence.SingleAsync(item => item.Id == successfulId);

        Assert.Equal(1, count);
        Assert.Equal("AVAILABLE", failedEvidence.InspectionStatus);
        Assert.Null(failedEvidence.ExpiredAt);
        Assert.Equal("EXPIRED", successfulEvidence.InspectionStatus);
        Assert.Equal(now, successfulEvidence.ExpiredAt);
        Assert.Equal(1, await db.OperationsAudit.CountAsync());
        Assert.Contains(failedId, storage.Content.Keys);
        Assert.DoesNotContain(successfulId, storage.Content.Keys);
    }

    private static CoastalOperationsDbContext CreateDb(DbContextOptions<CoastalOperationsDbContext>? options = null) => new(
        options ?? new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-evidence-tests-{Guid.NewGuid():N}")
            .Options);

    private static AssessmentEvidenceApplicationService CreateService(CoastalOperationsDbContext db, MemoryEvidenceStorage storage) => new(
        db, new AssessmentEvidenceSanitizer(), storage, NullLogger<AssessmentEvidenceApplicationService>.Instance);

    private static Assessment AddAssessment(CoastalOperationsDbContext db, Guid owner, string status = "SUBMITTED")
    {
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = TargetId,
            PeriodStartsAt = DateTimeOffset.UtcNow, PeriodEndsAt = DateTimeOffset.UtcNow.AddHours(1),
            Objective = "Evidence test assessment", WorkflowStatus = status, InitiatedBy = owner, Version = 1,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assessments.Add(assessment);
        return assessment;
    }

    private static AssessmentEvidence Evidence(
        Guid assessmentId,
        Guid owner,
        Guid id,
        DateTimeOffset expiresAt,
        byte[]? content = null) => new()
    {
        Id = id, AssessmentId = assessmentId, AssessmentVersion = 1, UploadedBy = owner,
        MediaType = "image/png", ByteLength = (content ?? [1, 2, 3]).LongLength,
        ContentSha256 = Convert.ToHexString(SHA256.HashData(content ?? [1, 2, 3])).ToLowerInvariant(),
        InspectionStatus = "AVAILABLE", UploadedAt = expiresAt.AddDays(-365), ExpiresAt = expiresAt
    };

    private static byte[] ValidPng() => PngWithTextChunk(includeText: false);

    private static byte[] PngWithTextChunk(bool includeText = true)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), 1);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, "IHDR", header);
        if (includeText) WriteChunk(output, "tEXt", System.Text.Encoding.ASCII.GetBytes("Comment\0private-location"));
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(new byte[] { 0, 20, 40, 60, 255 });
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string name, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        var type = System.Text.Encoding.ASCII.GetBytes(name);
        output.Write(type);
        output.Write(data);
        var crcData = new byte[type.Length + data.Length];
        type.CopyTo(crcData, 0);
        data.CopyTo(crcData, type.Length);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc(crcData));
        output.Write(crc);
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 1 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }

    private static bool ContainsBytes(byte[] source, byte[] value)
    {
        for (var index = 0; index <= source.Length - value.Length; index++)
            if (source.AsSpan(index, value.Length).SequenceEqual(value)) return true;
        return false;
    }

    private sealed class MemoryEvidenceStorage : IAssessmentEvidenceStorage
    {
        public Dictionary<Guid, byte[]> Content { get; } = [];
        public HashSet<Guid> DeleteFailures { get; } = [];
        public Exception? StoreException { get; set; }
        public Exception? ReadException { get; set; }
        public int StoreCalls { get; private set; }
        public int ReadCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public Task StoreAsync(Guid evidenceId, byte[] content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StoreCalls++;
            if (StoreException is not null) throw StoreException;
            Content.Add(evidenceId, content.ToArray());
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadAsync(Guid evidenceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCalls++;
            if (ReadException is not null) throw ReadException;
            return Task.FromResult(Content.TryGetValue(evidenceId, out var bytes) ? bytes.ToArray() : null);
        }

        public Task DeleteAsync(Guid evidenceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteCalls++;
            if (DeleteFailures.Contains(evidenceId)) throw new IOException("Synthetic delete failure");
            Content.Remove(evidenceId);
            return Task.CompletedTask;
        }
    }

    private sealed class FailSaveChangesInterceptor : SaveChangesInterceptor
    {
        public bool ShouldFail { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFail) throw new InvalidOperationException("Synthetic metadata-store failure");
            return ValueTask.FromResult(result);
        }
    }
}
