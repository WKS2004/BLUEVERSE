using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class IdempotencyStoreTests
{
    [Fact(DisplayName = "COASTAL-IDEMPOTENCY-001 saved outcome replays its status and response body")]
    [Trait("TestId", "COASTAL-IDEMPOTENCY-001")]
    public async Task SavedOutcomeReplaysExactlyOnce()
    {
        await using var db = CreateDb();
        var store = new IdempotencyStore(db);
        var actorId = Guid.NewGuid();
        var digest = OperationsValidation.RequestDigest(new { value = "coast" });
        var firstBody = new StoredBody(Guid.NewGuid(), "SUBMITTED");

        var saved = await store.SaveAsync(actorId, "assessment.create", "request-1", digest, 201, firstBody, CancellationToken.None);
        var replay = await store.TryReplayAsync<StoredBody>(actorId, "assessment.create", "request-1", digest, CancellationToken.None);

        Assert.NotNull(replay);
        Assert.Equal(201, saved.StatusCode);
        Assert.False(saved.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(firstBody, replay.Body);
        Assert.Equal(saved.SerializedBody, replay.SerializedBody);
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-IDEMPOTENCY-002 same scope and key with changed content conflicts")]
    [Trait("TestId", "COASTAL-IDEMPOTENCY-002")]
    public async Task ChangedContentCannotReuseOutcomeKey()
    {
        await using var db = CreateDb();
        var store = new IdempotencyStore(db);
        var actorId = Guid.NewGuid();
        await store.SaveAsync(
            actorId, "assessment.create", "same-key",
            OperationsValidation.RequestDigest(new { value = "first" }), 201,
            new StoredBody(Guid.NewGuid(), "SUBMITTED"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => store.TryReplayAsync<StoredBody>(
            actorId,
            "assessment.create",
            "same-key",
            OperationsValidation.RequestDigest(new { value = "changed" }),
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("idempotency_key_reused", exception.Code);
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-IDEMPOTENCY-003 actor and operation are part of the key scope")]
    [Trait("TestId", "COASTAL-IDEMPOTENCY-003")]
    public async Task SameKeyCanBeUsedByAnotherActorOrOperation()
    {
        await using var db = CreateDb();
        var store = new IdempotencyStore(db);
        var firstActor = Guid.NewGuid();
        var digest = OperationsValidation.RequestDigest(new { value = "same" });
        await store.SaveAsync(firstActor, "assessment.create", "shared-key", digest, 201,
            new StoredBody(Guid.NewGuid(), "SUBMITTED"), CancellationToken.None);

        var secondActor = await store.TryReplayAsync<StoredBody>(Guid.NewGuid(), "assessment.create", "shared-key", digest, CancellationToken.None);
        var secondOperation = await store.TryReplayAsync<StoredBody>(firstActor, "alert.decide:other", "shared-key", digest, CancellationToken.None);

        Assert.Null(secondActor);
        Assert.Null(secondOperation);
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-IDEMPOTENCY-004 responses above the storage bound are not persisted")]
    [Trait("TestId", "COASTAL-IDEMPOTENCY-004")]
    public async Task OversizedSerializedResponseIsRejected()
    {
        await using var db = CreateDb();
        var store = new IdempotencyStore(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(
            Guid.NewGuid(), "assessment.create", "large-response", "digest", 201,
            new StoredBody(Guid.NewGuid(), new string('x', 65536)), CancellationToken.None));

        Assert.Contains("configured storage limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-IDEMPOTENCY-005 a canceled replay query observes caller cancellation")]
    [Trait("TestId", "COASTAL-IDEMPOTENCY-005")]
    public async Task ReplayQueryPropagatesCancellation()
    {
        await using var db = CreateDb();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new IdempotencyStore(db).TryReplayAsync<StoredBody>(
            Guid.NewGuid(), "assessment.create", "request-1", "digest", cancellation.Token));
    }

    private static CoastalOperationsDbContext CreateDb() => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-idempotency-tests-{Guid.NewGuid():N}")
            .Options);

    private sealed record StoredBody(Guid Id, string State);
}
