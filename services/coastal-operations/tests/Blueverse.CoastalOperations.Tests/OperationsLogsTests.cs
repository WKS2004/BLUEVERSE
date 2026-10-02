using System.Security.Claims;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Controllers;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class OperationsLogsTests
{
    [Fact(DisplayName = "COASTAL-LOGS-001 owners see all their retained assessment states; queue readers never see another active draft")]
    [Trait("TestId", "COASTAL-LOGS-001")]
    public async Task AssessmentScopeAndPagination()
    {
        await using var db = Db(); var actor = Guid.NewGuid(); var other = Guid.NewGuid();
        var own = new[] { "DRAFT", "SUBMITTED", "CANCELLED", "REJECTED" }.Select(state => Assessment(actor, state)).ToArray();
        var otherDraft = Assessment(other, "DRAFT"); var otherCancelled = Assessment(other, "CANCELLED"); var published = Assessment(other, "SUBMITTED");
        db.Assessments.AddRange(own.Concat([otherDraft, otherCancelled, published])); await db.SaveChangesAsync();
        var service = new AssessmentApplicationService(db, new(db), new Collector());
        var owner = await service.GetQueueAsync(new(), actor, false, default, auditView: true);
        Assert.Equal(own.Select(x => x.Id).Order(), owner.Items.Select(x => x.AssessmentId).Order());
        Assert.DoesNotContain((await service.GetQueueAsync(new(), actor, false, default)).Items, x => x.WorkflowStatus == "CANCELLED");
        var queue = await service.GetQueueAsync(new(), actor, true, default, auditView: true);
        Assert.Equal(6, queue.Items.Count); Assert.DoesNotContain(queue.Items, x => x.AssessmentId == otherDraft.Id);
        Assert.Contains(queue.Items, x => x.AssessmentId == otherCancelled.Id); Assert.Contains(queue.Items, x => x.AssessmentId == published.Id);
        var seen = new List<Guid>(); string? cursor = null;
        do {
            var page = await service.GetQueueAsync(new() { PageSize = 1, Cursor = cursor }, actor, false, default, auditView: true);
            seen.Add(Assert.Single(page.Items).AssessmentId); cursor = page.NextCursor;
        } while (cursor is not null);
        Assert.Equal(4, seen.Distinct().Count()); Assert.Equal(own.Select(x => x.Id).Order(), seen.Order());
        var search = await service.GetQueueAsync(new() { Search = "cancelled", TargetType = "ACTIVITY" }, actor, false, default, auditView: true);
        Assert.Equal("CANCELLED", Assert.Single(search.Items).WorkflowStatus);
        Assert.Empty((await service.GetQueueAsync(new() { RecordId = otherDraft.Id }, actor, true, default, auditView: true)).Items);
        Assert.Empty(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-LOGS-002 alert logs include inactive states, preserve owner scope and keep normal visibility unchanged")]
    [Trait("TestId", "COASTAL-LOGS-002")]
    public async Task AlertScopeAndFilters()
    {
        await using var db = Db(); var actor = Guid.NewGuid(); var other = Guid.NewGuid();
        var states = new[] { "PROPOSED", "ACTIVE", "RESOLVED", "EXPIRED", "SUPERSEDED", "WITHDRAWN" };
        db.OperationalAlerts.AddRange(states.Select(state => Alert(actor, state)).Concat(states.Select(state => Alert(other, state))));
        await db.SaveChangesAsync(); var service = new AlertApplicationService(db, new(db));
        var owner = await service.GetQueueAsync(new(), actor, false, default, auditView: true);
        Assert.Equal(states.Order(), owner.Items.Select(x => x.Lifecycle).Order()); Assert.Equal(6, owner.Items.Count);
        var manager = await service.GetQueueAsync(new(), actor, true, default, auditView: true); Assert.Equal(12, manager.Items.Count);
        var history = await service.GetQueueAsync(new() { History = true }, actor, false, default, auditView: true);
        Assert.Equal(4, history.Items.Count); Assert.DoesNotContain(history.Items, x => x.Lifecycle is "PROPOSED" or "ACTIVE");
        var search = await service.GetQueueAsync(new() { Search = "WITHDRAWN", Severity = "LOW", Visibility = "OPERATIONS" }, actor, false, default, auditView: true);
        Assert.Equal("WITHDRAWN", Assert.Single(search.Items).Lifecycle);
        Assert.Empty((await service.GetQueueAsync(new(), actor, false, default)).Items);
        Assert.DoesNotContain((await service.GetQueueAsync(new(), actor, true, default)).Items, x => x.Lifecycle == "WITHDRAWN");
        var first = await service.GetQueueAsync(new() { PageSize = 5 }, actor, false, default, auditView: true);
        var second = await service.GetQueueAsync(new() { PageSize = 5, Cursor = first.NextCursor }, actor, false, default, auditView: true);
        Assert.Equal(5, first.Items.Count); Assert.Single(second.Items); Assert.Null(second.NextCursor);
        Assert.Equal(6, first.Items.Concat(second.Items).Select(x => x.AlertId).Distinct().Count()); Assert.Empty(await db.OperationsAudit.ToListAsync());
    }

    [Theory(DisplayName = "COASTAL-LOGS-003 malformed filters and out-of-range pages fail without state changes")]
    [Trait("TestId", "COASTAL-LOGS-003")]
    [InlineData(0, null, null)][InlineData(101, null, null)][InlineData(25, "malformed", null)][InlineData(25, null, "WRONG")]
    public async Task InvalidQueries(int size, string? cursor, string? target)
    {
        await using var db = Db(); var actor = Guid.NewGuid();
        var assessments = new AssessmentApplicationService(db, new(db), new Collector()); var alerts = new AlertApplicationService(db, new(db));
        var first = await Assert.ThrowsAsync<CoastalOperationsException>(() => assessments.GetQueueAsync(new() { PageSize = size, Cursor = cursor, TargetType = target }, actor, false, default, auditView: true));
        var second = await Assert.ThrowsAsync<CoastalOperationsException>(() => alerts.GetQueueAsync(new() { PageSize = size, Cursor = cursor, TargetType = target }, actor, false, default, auditView: true));
        Assert.Equal(422, first.StatusCode); Assert.Equal(422, second.StatusCode); Assert.Empty(await db.OperationsAudit.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-LOGS-004 log actions require audit policy plus corresponding read access and return private scoped bodies")]
    [Trait("TestId", "COASTAL-LOGS-004")]
    public async Task ControllerReadGates()
    {
        await using var db = Db(); var actor = Guid.NewGuid(); var assessment = Assessment(actor, "CANCELLED"); db.Assessments.Add(assessment); await db.SaveChangesAsync();
        var controller = new LogsController(new(db, new(db), new Collector()), new(db, new(db))) {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.User = Principal(actor, CoastalPermissions.AuditRead);
        Assert.IsType<ForbidResult>((await controller.Assessments(new(), default)).Result);
        Assert.IsType<ForbidResult>((await controller.Alerts(new(), default)).Result);
        controller.HttpContext.User = Principal(actor, CoastalPermissions.AuditRead, CoastalPermissions.AssessmentRead);
        var response = Assert.IsType<OkObjectResult>((await controller.Assessments(new(), default)).Result);
        var body = Assert.IsType<AssessmentQueueResponse>(response.Value); Assert.Equal(assessment.Id, Assert.Single(body.Items).AssessmentId);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        foreach (var action in new[] { nameof(LogsController.Assessments), nameof(LogsController.Alerts) }) {
            var attribute = Assert.Single(typeof(LogsController).GetMethod(action)!.GetCustomAttributes(typeof(HasPermissionAttribute), false).Cast<HasPermissionAttribute>());
            Assert.Equal(CoastalPermissions.AuditRead, attribute.PermissionCode);
        }
    }

    private static ClaimsPrincipal Principal(Guid actor, params string[] grants) => new(new ClaimsIdentity(new[] { new Claim("sub", actor.ToString()) }.Concat(grants.Select(g => new Claim("permission", g))), "test"));
    private static CoastalOperationsDbContext Db() => new(new DbContextOptionsBuilder<CoastalOperationsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Assessment Assessment(Guid owner, string status) => new() { Id = Guid.CreateVersion7(), WorkflowId = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), Title = $"Coastal {status}", Objective = "Inspect the coast", WorkflowStatus = status, InitiatedBy = owner, Version = 1, PeriodStartsAt = DateTimeOffset.UtcNow, PeriodEndsAt = DateTimeOffset.UtcNow.AddHours(1), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
    private static OperationalAlert Alert(Guid owner, string status) => new() { Id = Guid.CreateVersion7(), TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), Title = $"Coastal {status}", Description = "Use the marked path", Severity = "LOW", Visibility = "OPERATIONS", Lifecycle = status, CreatedBy = owner, UpdatedBy = owner, Version = 1, ValidFrom = DateTimeOffset.UtcNow.AddMinutes(-1), ValidUntil = DateTimeOffset.UtcNow.AddHours(1), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
    private sealed class Collector : IComponentDependencyCollector {
        public Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(string type, Guid id, DateTimeOffset start, DateTimeOffset end, Guid? workflow, CancellationToken token) => Task.FromResult<IReadOnlyList<ComponentDependencyResult>>([]);
    }
}
