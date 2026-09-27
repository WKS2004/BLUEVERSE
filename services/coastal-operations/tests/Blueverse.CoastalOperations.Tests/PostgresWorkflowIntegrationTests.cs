using Blueverse.CoastalOperations.Application;
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
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BLUEVERSE_CO_POSTGRES_TEST_CONNECTION")))
            Skip = "Set BLUEVERSE_CO_POSTGRES_TEST_CONNECTION to a dedicated blueverse_co_test database to run provider-specific cases.";
    }
}
