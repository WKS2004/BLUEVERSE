using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class TargetStatusApplicationServiceTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");

    [Fact(DisplayName = "COASTAL-TARGET-001 status normalizes target type and returns persisted authority")]
    [Trait("TestId", "COASTAL-TARGET-001")]
    public async Task StatusReturnsKnownTargetState()
    {
        await using var db = CreateDb();
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        db.TargetOperationalStates.Add(State("ACTIVITY", TargetId, "CAUTION", 4, updatedAt));
        await db.SaveChangesAsync();

        var result = await new TargetStatusApplicationService(db).GetStatusAsync(" activity ", TargetId, CancellationToken.None);

        Assert.Equal("ACTIVITY", result.TargetType);
        Assert.Equal(TargetId, result.TargetId);
        Assert.Equal("CAUTION", result.OperationalState);
        Assert.Equal(4, result.StateVersion);
        Assert.Equal(updatedAt, result.UpdatedAt);
    }

    [Fact(DisplayName = "COASTAL-TARGET-002 unknown operational status remains not found")]
    [Trait("TestId", "COASTAL-TARGET-002")]
    public async Task MissingStatusIsNotSynthesized()
    {
        await using var db = CreateDb();
        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            new TargetStatusApplicationService(db).GetStatusAsync("ACTIVITY", TargetId, CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
        Assert.Equal("target_state_not_found", exception.Code);
        Assert.Empty(await db.TargetOperationalStates.ToListAsync());
    }

    [Theory(DisplayName = "COASTAL-TARGET-003 status rejects unsupported target identity")]
    [Trait("TestId", "COASTAL-TARGET-003")]
    [InlineData("BEACH", "11111111-2222-4333-8444-555555555555")]
    [InlineData("ACTIVITY", "00000000-0000-0000-0000-000000000000")]
    public async Task InvalidStatusIdentityIsRejected(string targetType, string targetId)
    {
        await using var db = CreateDb();
        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            new TargetStatusApplicationService(db).GetStatusAsync(targetType, Guid.Parse(targetId), CancellationToken.None));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal(targetType == "BEACH" ? "target_type_invalid" : "target_identity_invalid", exception.Code);
    }

    [Fact(DisplayName = "COASTAL-TARGET-004 history is scoped, ordered and cursor paginated")]
    [Trait("TestId", "COASTAL-TARGET-004")]
    public async Task HistoryUsesTargetAndCursorBoundaries()
    {
        await using var db = CreateDb();
        db.TargetOperationalStates.Add(State("ACTIVITY", TargetId, "OPEN", 1, DateTimeOffset.UtcNow));
        var otherTarget = Guid.NewGuid();
        db.OperationalHistory.AddRange(
            History(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), TargetId, "OPEN", "CAUTION"),
            History(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), TargetId, "CAUTION", "OPEN"),
            History(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"), TargetId, "OPEN", "TEMPORARILY_SUSPENDED"),
            History(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), otherTarget, "OPEN", "CAUTION"));
        await db.SaveChangesAsync();
        var service = new TargetStatusApplicationService(db);

        var first = await service.GetHistoryAsync("ACTIVITY", TargetId, 2, null, CancellationToken.None);
        var second = await service.GetHistoryAsync("ACTIVITY", TargetId, 2, first.NextCursor, CancellationToken.None);

        Assert.Equal(2, first.Items.Count);
        Assert.True(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3").CompareTo(first.Items[0].HistoryId) == 0);
        Assert.True(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2").CompareTo(first.Items[1].HistoryId) == 0);
        Assert.NotNull(first.NextCursor);
        Assert.Single(second.Items);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), second.Items[0].HistoryId);
        Assert.Null(second.NextCursor);
        Assert.DoesNotContain(first.Items.Concat(second.Items), item =>
            item.HistoryId == Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    }

    [Fact(DisplayName = "COASTAL-TARGET-005 known state with no history returns an empty page")]
    [Trait("TestId", "COASTAL-TARGET-005")]
    public async Task KnownTargetCanHaveEmptyHistory()
    {
        await using var db = CreateDb();
        db.TargetOperationalStates.Add(State("ACTIVITY", TargetId, "OPEN", 1, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var result = await new TargetStatusApplicationService(db).GetHistoryAsync(
            "ACTIVITY", TargetId, 25, null, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Fact(DisplayName = "COASTAL-TARGET-006 history rejects missing state, invalid page size and malformed cursor")]
    [Trait("TestId", "COASTAL-TARGET-006")]
    public async Task InvalidHistoryRequestIsRejectedWithoutChangingState()
    {
        await using var db = CreateDb();
        var service = new TargetStatusApplicationService(db);

        var missing = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.GetHistoryAsync("ACTIVITY", TargetId, 25, null, CancellationToken.None));
        var invalidSize = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.GetHistoryAsync("ACTIVITY", TargetId, 101, null, CancellationToken.None));
        var invalidCursor = await Assert.ThrowsAsync<CoastalOperationsException>(() =>
            service.GetHistoryAsync("ACTIVITY", TargetId, 25, "%%%", CancellationToken.None));

        Assert.Equal("target_history_not_found", missing.Code);
        Assert.Equal("page_size_invalid", invalidSize.Code);
        Assert.Equal("cursor_invalid", invalidCursor.Code);
        Assert.Equal(StatusCodes.Status404NotFound, missing.StatusCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, invalidSize.StatusCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, invalidCursor.StatusCode);
        Assert.Empty(await db.OperationalHistory.ToListAsync());
    }

    private static CoastalOperationsDbContext CreateDb() => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-target-tests-{Guid.NewGuid():N}")
            .Options);

    private static TargetOperationalState State(
        string targetType,
        Guid targetId,
        string state,
        int version,
        DateTimeOffset updatedAt) => new()
    {
        Id = Guid.NewGuid(), TargetType = targetType, TargetId = targetId,
        State = state, Version = version, UpdatedBy = Guid.NewGuid(), UpdatedAt = updatedAt
    };

    private static OperationalHistoryEntry History(Guid id, Guid targetId, string previous, string next) => new()
    {
        Id = id, TargetType = "ACTIVITY", TargetId = targetId,
        PreviousState = previous, NewState = next, AssessmentId = Guid.NewGuid(),
        ProposalId = Guid.NewGuid(), DecisionId = Guid.NewGuid(), ActorId = Guid.NewGuid(),
        CorrelationId = "target-history-test", CreatedAt = DateTimeOffset.UtcNow
    };
}
