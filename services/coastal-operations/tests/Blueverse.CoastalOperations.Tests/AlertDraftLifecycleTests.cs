using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class AlertDraftLifecycleTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-2222-4333-8444-555555555555");

    [Fact(DisplayName = "COASTAL-ALERT-022 draft withdrawal is a retained idempotent tombstone")]
    [Trait("TestId", "COASTAL-ALERT-022")]
    public async Task WithdrawPersistsLifecycleAuditAndExactReplay()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), owner, "create", CancellationToken.None);
        var request = new WithdrawAlertDraftRequest { ExpectedVersion = 1 };

        var first = await service.WithdrawDraftAsync(created.AlertId, request, owner, "withdraw", "withdraw-22", CancellationToken.None);
        var replay = await service.WithdrawDraftAsync(created.AlertId, request, owner, "ignored", "withdraw-22", CancellationToken.None);
        var saved = await db.OperationalAlerts.AsNoTracking().SingleAsync();

        Assert.Equal(StatusCodes.Status200OK, first.StatusCode);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.SerializedBody, replay.SerializedBody);
        Assert.Equal("WITHDRAWN", first.Body.Lifecycle);
        Assert.Equal(2, first.Body.Version);
        Assert.Equal(owner, first.Body.WithdrawnBy);
        Assert.NotNull(first.Body.WithdrawnAt);
        Assert.Equal("WITHDRAWN", saved.Lifecycle);
        Assert.Equal(owner, saved.WithdrawnBy);
        Assert.NotNull(saved.WithdrawnAt);
        Assert.Equal(owner, saved.UpdatedBy);
        Assert.Single(await db.OperationalAlerts.ToListAsync());
        Assert.Empty(await db.AlertDecisions.ToListAsync());
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
        Assert.Contains(await db.OperationsAudit.ToListAsync(), item => item.Action == "DRAFT_WITHDRAWN" && item.ActorId == owner);
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-023 withdrawn alerts are hidden from ordinary lists and cannot publish")]
    [Trait("TestId", "COASTAL-ALERT-023")]
    public async Task WithdrawnAlertRequiresManagerAuditFilterAndIsTerminal()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), owner, "create", CancellationToken.None);
        await service.WithdrawDraftAsync(
            created.AlertId, new WithdrawAlertDraftRequest { ExpectedVersion = 1 }, owner, "withdraw", "withdraw-23", CancellationToken.None);

        var ordinaryManagerList = await service.GetQueueAsync(new AlertListQuery(), owner, canManage: true, CancellationToken.None);
        var auditView = await service.GetQueueAsync(new AlertListQuery { Lifecycle = "WITHDRAWN" }, owner, canManage: true, CancellationToken.None);
        var deniedAudit = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.GetQueueAsync(
            new AlertListQuery { Lifecycle = "WITHDRAWN" }, Guid.NewGuid(), canManage: false, CancellationToken.None));
        var publish = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.DecideAsync(
            created.AlertId,
            new AlertDecisionRequest { Decision = "PUBLISH", ExpectedVersion = 2 },
            owner,
            "publish",
            "publish-withdrawn",
            CancellationToken.None));

        Assert.Empty(ordinaryManagerList.Items);
        Assert.Equal("WITHDRAWN", Assert.Single(auditView.Items).Lifecycle);
        Assert.Equal(StatusCodes.Status403Forbidden, deniedAudit.StatusCode);
        Assert.Equal("alert_not_publishable", publish.Code);
        Assert.Empty(await db.AlertDecisions.ToListAsync());
        Assert.Equal(2, await db.OperationsAudit.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-024 withdrawal rejects foreign owners stale versions and closed drafts")]
    [Trait("TestId", "COASTAL-ALERT-024")]
    public async Task WithdrawEnforcesOwnerVersionAndDraftState()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), owner, "create", CancellationToken.None);

        var foreignOwner = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.WithdrawDraftAsync(
            created.AlertId, new WithdrawAlertDraftRequest { ExpectedVersion = 1 }, Guid.NewGuid(), "foreign", "foreign-key", CancellationToken.None));
        var stale = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.WithdrawDraftAsync(
            created.AlertId, new WithdrawAlertDraftRequest { ExpectedVersion = 2 }, owner, "stale", "stale-key", CancellationToken.None));
        var alert = await db.OperationalAlerts.SingleAsync();
        alert.Lifecycle = "ACTIVE";
        await db.SaveChangesAsync();
        var closed = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.WithdrawDraftAsync(
            created.AlertId, new WithdrawAlertDraftRequest { ExpectedVersion = 1 }, owner, "closed", "closed-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, foreignOwner.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, stale.StatusCode);
        Assert.Equal("alert_version_stale", stale.Code);
        Assert.Equal(StatusCodes.Status409Conflict, closed.StatusCode);
        Assert.Equal("alert_not_withdrawable", closed.Code);
        Assert.Null(alert.WithdrawnAt);
        Assert.Equal("ACTIVE", alert.Lifecycle);
        Assert.Single(await db.OperationsAudit.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-ALERT-025 only the draft owner can edit a proposed alert")]
    [Trait("TestId", "COASTAL-ALERT-025")]
    public async Task UpdateDraftIsCallerOwned()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest(), owner, "create", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.UpdateDraftAsync(
            created.AlertId, new UpdateAlertRequest
            {
                ExpectedVersion = 1,
                Title = "Other owner's edit",
                Description = "Should be hidden",
                Severity = "LOW",
                ValidFrom = DateTimeOffset.UtcNow.ToString("O"),
                ValidUntil = DateTimeOffset.UtcNow.AddHours(1).ToString("O")
            }, Guid.NewGuid(), "foreign-update", CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
        Assert.Equal("Test alert", (await db.OperationalAlerts.AsNoTracking().SingleAsync()).Title);
        Assert.Single(await db.OperationsAudit.ToListAsync());
    }

    private static CoastalOperationsDbContext CreateDb() => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-alert-draft-{Guid.NewGuid():N}")
            .Options);

    private static AlertApplicationService CreateService(CoastalOperationsDbContext db) => new(db, new IdempotencyStore(db));

    private static CreateAlertRequest CreateRequest()
    {
        var starts = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new CreateAlertRequest
        {
            TargetType = "ACTIVITY",
            TargetId = TargetId,
            Title = "Test alert",
            Description = "Synthetic alert draft",
            Severity = "LOW",
            Visibility = "PUBLIC",
            ValidFrom = starts.ToString("O"),
            ValidUntil = starts.AddHours(1).ToString("O")
        };
    }
}
