using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Blueverse.CoastalOperations.Tests;

public sealed class PostgresWorkflowIntegrationTests
{
    private const string ConnectionVariable = "BLUEVERSE_CO_POSTGRES_TEST_CONNECTION";
    private static readonly DateTimeOffset PeriodStart = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [PostgresFact(DisplayName = "COASTAL-POSTGRES-001 migrations and concurrent idempotency writes use PostgreSQL constraints")]
    [Trait("TestId", "COASTAL-POSTGRES-001")]
    public async Task MigrationsAndConcurrentIdempotencyWritersUseTheDatabaseUniqueKey()
    {
        var connectionString = DedicatedConnectionString();
        await MigrateAndAssertReadyAsync(connectionString);
        var actor = Guid.NewGuid();
        var operation = "test.concurrent";
        var key = Guid.NewGuid().ToString("N");
        var digest = OperationsValidation.RequestDigest(new { request = "same" });

        try
        {
            await using var firstDb = CreateDb(connectionString);
            await using var secondDb = CreateDb(connectionString);
            var firstStore = new IdempotencyStore(firstDb);
            var secondStore = new IdempotencyStore(secondDb);
            var firstWrite = firstStore.SaveAsync(actor, operation, key, digest, 201, new StoredBody("created"), CancellationToken.None);
            var secondWrite = secondStore.SaveAsync(actor, operation, key, digest, 201, new StoredBody("created"), CancellationToken.None);

            var outcomes = await Task.WhenAll(firstWrite, secondWrite);

            Assert.Single(outcomes, outcome => !outcome.Replayed);
            Assert.Single(outcomes, outcome => outcome.Replayed);
            Assert.All(outcomes, outcome =>
            {
                Assert.Equal(201, outcome.StatusCode);
                Assert.Equal("created", outcome.Body.State);
            });
            await using var verifyDb = CreateDb(connectionString);
            Assert.Equal(1, await verifyDb.IdempotencyRecords.CountAsync(item => item.ActorId == actor && item.Operation == operation && item.Key == key));
        }
        finally
        {
            await using var cleanupDb = CreateDb(connectionString);
            await cleanupDb.IdempotencyRecords
                .Where(item => item.ActorId == actor && item.Operation == operation && item.Key == key)
                .ExecuteDeleteAsync();
        }
    }

    [PostgresFact(DisplayName = "COASTAL-POSTGRES-002 stale assessment writes are rejected by the PostgreSQL concurrency token")]
    [Trait("TestId", "COASTAL-POSTGRES-002")]
    public async Task AssessmentVersionPreventsAStaleConcurrentWrite()
    {
        var connectionString = DedicatedConnectionString();
        await MigrateAndAssertReadyAsync(connectionString);
        var assessmentId = Guid.NewGuid();
        try
        {
            await using (var seedDb = CreateDb(connectionString))
            {
                seedDb.Assessments.Add(new Assessment
                {
                    Id = assessmentId, WorkflowId = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = Guid.NewGuid(),
                    PeriodStartsAt = PeriodStart, PeriodEndsAt = PeriodStart.AddHours(1),
                    Objective = "concurrency fixture", WorkflowStatus = "SUBMITTED", InitiatedBy = Guid.NewGuid(),
                    Version = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
                });
                await seedDb.SaveChangesAsync();
            }

            await using var winnerDb = CreateDb(connectionString);
            await using var staleDb = CreateDb(connectionString);
            var winner = await winnerDb.Assessments.SingleAsync(item => item.Id == assessmentId);
            var stale = await staleDb.Assessments.SingleAsync(item => item.Id == assessmentId);
            winner.Objective = "winner update";
            winner.Version++;
            winner.UpdatedAt = DateTimeOffset.UtcNow;
            stale.Objective = "stale update";
            stale.Version++;
            stale.UpdatedAt = DateTimeOffset.UtcNow;

            await winnerDb.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());

            await using var verifyDb = CreateDb(connectionString);
            var persisted = await verifyDb.Assessments.AsNoTracking().SingleAsync(item => item.Id == assessmentId);
            Assert.Equal(2, persisted.Version);
            Assert.Equal("winner update", persisted.Objective);
        }
        finally
        {
            await using var cleanupDb = CreateDb(connectionString);
            await cleanupDb.Assessments.Where(item => item.Id == assessmentId).ExecuteDeleteAsync();
        }
    }

    [PostgresFact(DisplayName = "COASTAL-POSTGRES-003 concurrent assessment submission replays one idempotent result")]
    [Trait("TestId", "COASTAL-POSTGRES-003")]
    public async Task ConcurrentSubmissionWithTheSameKeyHasOneCommitAndOneReplay()
    {
        var connectionString = DedicatedConnectionString();
        await MigrateAndAssertReadyAsync(connectionString);
        var assessmentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var operation = $"assessment.submit:{assessmentId:N}";
        var key = Guid.NewGuid().ToString("N");

        try
        {
            await using (var seedDb = CreateDb(connectionString))
            {
                seedDb.Assessments.Add(new Assessment
                {
                    Id = assessmentId,
                    WorkflowId = Guid.NewGuid(),
                    TargetType = "ACTIVITY",
                    TargetId = Guid.NewGuid(),
                    PeriodStartsAt = PeriodStart,
                    PeriodEndsAt = PeriodStart.AddHours(1),
                    Objective = "concurrent submission fixture",
                    WorkflowStatus = "DRAFT",
                    InitiatedBy = actorId,
                    Version = 1,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await seedDb.SaveChangesAsync();
            }

            var collector = new SubmissionBarrierCollector();
            await using var firstDb = CreateDb(connectionString);
            await using var secondDb = CreateDb(connectionString);
            var firstService = new AssessmentApplicationService(firstDb, new IdempotencyStore(firstDb), collector);
            var secondService = new AssessmentApplicationService(secondDb, new IdempotencyStore(secondDb), collector);
            var request = new SubmitAssessmentDraftRequest { ExpectedVersion = 1 };
            var first = firstService.SubmitDraftAsync(assessmentId, request, actorId, "first", key, CancellationToken.None);
            var second = secondService.SubmitDraftAsync(assessmentId, request, actorId, "second", key, CancellationToken.None);

            var outcomes = await Task.WhenAll(first, second);

            Assert.Single(outcomes, outcome => !outcome.Replayed);
            Assert.Single(outcomes, outcome => outcome.Replayed);
            Assert.All(outcomes, outcome =>
            {
                Assert.Equal("SUBMITTED", outcome.Body.WorkflowStatus);
                Assert.Equal(2, outcome.Body.Version);
            });
            await using var verifyDb = CreateDb(connectionString);
            var persisted = await verifyDb.Assessments.AsNoTracking().SingleAsync(item => item.Id == assessmentId);
            Assert.Equal("SUBMITTED", persisted.WorkflowStatus);
            Assert.Equal(2, persisted.Version);
            Assert.Equal(1, await verifyDb.IdempotencyRecords.CountAsync(item => item.ActorId == actorId && item.Operation == operation && item.Key == key));
            Assert.Equal(1, await verifyDb.OperationsAudit.CountAsync(item => item.ResourceType == "assessment" && item.ResourceId == assessmentId && item.Action == "SUBMITTED"));
        }
        finally
        {
            await using var cleanupDb = CreateDb(connectionString);
            await cleanupDb.OperationsAudit.Where(item => item.ResourceType == "assessment" && item.ResourceId == assessmentId).ExecuteDeleteAsync();
            await cleanupDb.IdempotencyRecords.Where(item => item.ActorId == actorId && item.Operation == operation && item.Key == key).ExecuteDeleteAsync();
            await cleanupDb.AssessmentDispatches.Where(item => item.AssessmentId == assessmentId).ExecuteDeleteAsync();
            await cleanupDb.Assessments.Where(item => item.Id == assessmentId).ExecuteDeleteAsync();
        }
    }

    [PostgresFact(DisplayName = "COASTAL-POSTGRES-004 retained log queries and removal tombstones use PostgreSQL migration and audit persistence")]
    [Trait("TestId", "COASTAL-POSTGRES-004")]
    public async Task LogsAndEvidenceRemovalPersistOnPostgres()
    {
        var connection = DedicatedConnectionString(); await MigrateAndAssertReadyAsync(connection);
        var owner = Guid.NewGuid(); var assessmentId = Guid.NewGuid(); var evidenceId = Guid.NewGuid(); var alertId = Guid.NewGuid();
        var title = $"Provider logs {assessmentId:N}"; var storage = new RemovalStorage();
        try
        {
            await using var db = CreateDb(connection);
            db.Assessments.Add(new Assessment { Id = assessmentId, WorkflowId = Guid.NewGuid(), InitiatedBy = owner,
                TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), Title = title, Objective = "Inspect access", WorkflowStatus = "DRAFT",
                PeriodStartsAt = PeriodStart, PeriodEndsAt = PeriodStart.AddHours(1), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            db.AssessmentEvidence.Add(new AssessmentEvidence { Id = evidenceId, AssessmentId = assessmentId, AssessmentVersion = 1, UploadedBy = owner,
                ByteLength = 3, ContentSha256 = new string('a', 64), UploadedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            db.OperationalAlerts.Add(new OperationalAlert { Id = alertId, TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), Title = title,
                Description = "Archived notice", Severity = "LOW", Visibility = "OPERATIONS", Lifecycle = "WITHDRAWN", CreatedBy = owner, UpdatedBy = owner,
                ValidFrom = PeriodStart, ValidUntil = PeriodStart.AddHours(1), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            var result = await new AssessmentEvidenceApplicationService(db, new(), storage, Microsoft.Extensions.Logging.Abstractions.NullLogger<AssessmentEvidenceApplicationService>.Instance)
                .RemoveAsync(assessmentId, evidenceId, 1, owner, "provider-remove", default);
            Assert.Equal(2, result.AssessmentVersion); Assert.Equal("REMOVED", result.InspectionStatus); Assert.Equal(1, storage.Deletes);
            await using var verify = CreateDb(connection);
            var evidence = await verify.AssessmentEvidence.SingleAsync(x => x.Id == evidenceId);
            Assert.NotNull(evidence.RemovedAt); Assert.NotNull(evidence.ContentDeletedAt); Assert.Equal("REMOVED", evidence.InspectionStatus);
            var audit = await new OperationsAuditReader(verify).GetAssessmentAsync(assessmentId, owner, false, new(), default);
            Assert.Equal("REMOVED", Assert.Single(audit.Items).Action); Assert.Equal(evidenceId, audit.Items[0].ResourceId);
            var assessmentService = new AssessmentApplicationService(verify, new(verify), new SubmissionBarrierCollector());
            Assert.Equal(assessmentId, Assert.Single((await assessmentService.GetQueueAsync(new() { Search = title.ToLowerInvariant(), PageSize = 5 }, owner, false, default, auditView: true)).Items).AssessmentId);
            var alertService = new AlertApplicationService(verify, new(verify));
            Assert.Equal(alertId, Assert.Single((await alertService.GetQueueAsync(new() { Search = title, History = true }, owner, false, default, auditView: true)).Items).AlertId);
            Assert.Empty((await alertService.GetQueueAsync(new() { Search = title }, Guid.NewGuid(), false, default, auditView: true)).Items);
        }
        finally
        {
            await using var cleanup = CreateDb(connection);
            await cleanup.OperationsAudit.Where(x => x.ResourceId == evidenceId || x.ResourceId == assessmentId).ExecuteDeleteAsync();
            await cleanup.AssessmentEvidence.Where(x => x.Id == evidenceId).ExecuteDeleteAsync();
            await cleanup.OperationalAlerts.Where(x => x.Id == alertId).ExecuteDeleteAsync();
            await cleanup.Assessments.Where(x => x.Id == assessmentId).ExecuteDeleteAsync();
        }
    }

    [PostgresFact(DisplayName = "COASTAL-POSTGRES-005 publication racing stale removal preserves published evidence and rejects the stale write")]
    [Trait("TestId", "COASTAL-POSTGRES-005")]
    public async Task PublicationRacePreservesEvidence()
    {
        var connection = DedicatedConnectionString(); await MigrateAndAssertReadyAsync(connection);
        var owner = Guid.NewGuid(); var assessmentId = Guid.NewGuid(); var evidenceId = Guid.NewGuid(); var storage = new RemovalStorage();
        try
        {
            await using (var seed = CreateDb(connection))
            {
                seed.Assessments.Add(new Assessment { Id = assessmentId, WorkflowId = Guid.NewGuid(), InitiatedBy = owner,
                    TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), Title = "Removal race", Objective = "Inspect access", WorkflowStatus = "DRAFT",
                    PeriodStartsAt = PeriodStart, PeriodEndsAt = PeriodStart.AddHours(1), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
                seed.AssessmentEvidence.Add(new AssessmentEvidence { Id = evidenceId, AssessmentId = assessmentId, AssessmentVersion = 1, UploadedBy = owner,
                    ByteLength = 3, ContentSha256 = new string('a', 64), UploadedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
                await seed.SaveChangesAsync();
            }
            await using var stale = CreateDb(connection);
            await stale.Assessments.SingleAsync(x => x.Id == assessmentId); await stale.AssessmentEvidence.SingleAsync(x => x.Id == evidenceId);
            await using (var winner = CreateDb(connection))
            {
                var published = await winner.Assessments.SingleAsync(x => x.Id == assessmentId); published.WorkflowStatus = "SUBMITTED"; published.Version++;
                await winner.SaveChangesAsync();
            }
            var error = await Assert.ThrowsAsync<CoastalOperationsException>(() => new AssessmentEvidenceApplicationService(stale, new(), storage,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AssessmentEvidenceApplicationService>.Instance).RemoveAsync(assessmentId, evidenceId, 1, owner, "race-remove", default));
            Assert.Equal(409, error.StatusCode); Assert.Equal("assessment_version_stale", error.Code); Assert.Equal(0, storage.Deletes);
            await using var verify = CreateDb(connection);
            var saved = await verify.Assessments.SingleAsync(x => x.Id == assessmentId); Assert.Equal("SUBMITTED", saved.WorkflowStatus); Assert.Equal(2, saved.Version);
            var image = await verify.AssessmentEvidence.SingleAsync(x => x.Id == evidenceId); Assert.Equal("AVAILABLE", image.InspectionStatus); Assert.Null(image.RemovedAt); Assert.Null(image.ContentDeletedAt);
            Assert.Empty(await verify.OperationsAudit.Where(x => x.ResourceId == evidenceId).ToListAsync());
        }
        finally
        {
            await using var cleanup = CreateDb(connection);
            await cleanup.OperationsAudit.Where(x => x.ResourceId == evidenceId || x.ResourceId == assessmentId).ExecuteDeleteAsync();
            await cleanup.AssessmentEvidence.Where(x => x.Id == evidenceId).ExecuteDeleteAsync();
            await cleanup.Assessments.Where(x => x.Id == assessmentId).ExecuteDeleteAsync();
        }
    }

    [PostgresFact(DisplayName = "COASTAL-POSTGRES-006 detailed audit migration stores JSON snapshots and rejects non-array changes")]
    [Trait("TestId", "COASTAL-POSTGRES-006")]
    public async Task DetailedAuditSnapshotsRoundTripWithConstraints()
    {
        var connection = DedicatedConnectionString(); await MigrateAndAssertReadyAsync(connection);
        var id = Guid.NewGuid(); var rejectedId = Guid.NewGuid();
        try
        {
            await using (var db = CreateDb(connection))
            {
                db.OperationsAudit.Add(new OperationsAuditEntry { Id = id, ResourceType = "assessment", ResourceId = id, Action = "DRAFT_UPDATED", ActorId = Guid.NewGuid(), CorrelationId = "test-detailed", CreatedAt = DateTimeOffset.UtcNow,
                    ActorName = "Coastal Steward", ActorRolesJson = "[\"Field Officer\"]", RecordTitle = "Updated review", Summary = "Updated the draft (assessment)", ChangesJson = "[{\"field\":\"Title\",\"before\":\"Original\",\"after\":\"Updated\"}]" });
                await db.SaveChangesAsync();
            }
            await using (var verify = CreateDb(connection))
            {
                var row = await verify.OperationsAudit.SingleAsync(x => x.Id == id);
                Assert.Equal("Coastal Steward", row.ActorName); Assert.Equal("Updated review", row.RecordTitle);
                Assert.Equal(new[] { "Field Officer" }, System.Text.Json.JsonSerializer.Deserialize<string[]>(row.ActorRolesJson));
                var changes = System.Text.Json.JsonSerializer.Deserialize<AuditFieldChange[]>(row.ChangesJson, OperationsValidation.JsonOptions)!;
                var change = Assert.Single(changes); Assert.Equal("Title", change.Field); Assert.Equal("Original", change.Before); Assert.Equal("Updated", change.After);
                verify.OperationsAudit.Add(new OperationsAuditEntry { Id = rejectedId, ResourceType = "assessment", ResourceId = rejectedId, Action = "UPDATED", CorrelationId = "invalid-shape", Summary = "Invalid", ChangesJson = "{}", CreatedAt = DateTimeOffset.UtcNow });
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => verify.SaveChangesAsync());
                Assert.Equal("CK_OperationsAudit_Snapshots", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
            }
            await using var fresh = CreateDb(connection); Assert.False(await fresh.OperationsAudit.AnyAsync(x => x.Id == rejectedId));
        }
        finally { await using var cleanup = CreateDb(connection); await cleanup.OperationsAudit.Where(x => x.Id == id || x.Id == rejectedId).ExecuteDeleteAsync(); }
    }

    private sealed class RemovalStorage : IAssessmentEvidenceStorage
    {
        public int Deletes { get; private set; }
        public Task StoreAsync(Guid id, byte[] content, CancellationToken token) => Task.CompletedTask;
        public Task<byte[]?> ReadAsync(Guid id, CancellationToken token) => Task.FromResult<byte[]?>([1, 2, 3]);
        public Task DeleteAsync(Guid id, CancellationToken token) { Deletes++; return Task.CompletedTask; }
    }

    private static async Task MigrateAndAssertReadyAsync(string connectionString)
    {
        await using var db = CreateDb(connectionString);
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private static CoastalOperationsDbContext CreateDb(string connectionString) => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>().UseNpgsql(connectionString).Options);

    private static string DedicatedConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set {ConnectionVariable} to a dedicated Coastal Operations test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database ?? string.Empty;
        if (!databaseName.StartsWith("blueverse_co_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The PostgreSQL integration suite only accepts databases whose name starts with blueverse_co_test.");
        return connectionString;
    }

    private sealed record StoredBody(string State);

    private sealed class SubmissionBarrierCollector : IComponentDependencyCollector
    {
        private readonly TaskCompletionSource<bool> bothRequestsReachedCollector =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int requestCount;

        public async Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(
            string targetType,
            Guid targetId,
            DateTimeOffset periodStartsAt,
            DateTimeOffset periodEndsAt,
            Guid? sourceWorkflowId,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref requestCount) == 2)
                bothRequestsReachedCollector.TrySetResult(true);
            await bothRequestsReachedCollector.Task.WaitAsync(cancellationToken);
            return Array.Empty<ComponentDependencyResult>();
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BLUEVERSE_CO_POSTGRES_TEST_CONNECTION")))
            Skip = "Set BLUEVERSE_CO_POSTGRES_TEST_CONNECTION to a dedicated blueverse_co_test database to run provider-specific cases.";
    }
}
