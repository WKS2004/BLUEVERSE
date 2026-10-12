using System.Security.Claims;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Controllers;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Tests;

public sealed class RecordExperienceTests
{
    [Fact(DisplayName = "COASTAL-RECORD-005 legacy draft request digests remain compatible with persisted replays")]
    [Trait("TestId", "COASTAL-RECORD-005")]
    public void LegacyDraftDigest()
    {
        var target = Guid.Parse("10000000-0000-4000-8000-000000000001");
        var request = new CreateAssessmentRequest
        {
            TargetType = "DESTINATION", TargetId = target,
            PeriodStartsAt = "2026-10-01T09:00Z", PeriodEndsAt = "2026-10-01T12:00Z",
            Objective = "Inspect the access path."
        };
        const string originalJson = "{\"targetType\":\"DESTINATION\",\"targetId\":\"10000000-0000-4000-8000-000000000001\",\"sourceWorkflowId\":null,\"periodStartsAt\":\"2026-10-01T09:00Z\",\"periodEndsAt\":\"2026-10-01T12:00Z\",\"objective\":\"Inspect the access path.\"}";
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(originalJson))).ToLowerInvariant();
        Assert.Equal(expected, OperationsValidation.AssessmentDraftDigest(request));
        Assert.NotEqual(expected, OperationsValidation.AssessmentDraftDigest(new CreateAssessmentRequest
        {
            TargetType = request.TargetType, TargetId = target, PeriodStartsAt = request.PeriodStartsAt,
            PeriodEndsAt = request.PeriodEndsAt, Objective = request.Objective, Title = "Access review"
        }));
    }

    private static CoastalOperationsDbContext Db() => new(new DbContextOptionsBuilder<CoastalOperationsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task SeedZones(CoastalOperationsDbContext db) { db.TimeZoneLocations.AddRange(TimeZoneCatalogueSeed.Locations); await db.SaveChangesAsync(); }
    private sealed class Collector : IComponentDependencyCollector
    {
        public int Calls;
        public Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(string type, Guid id, DateTimeOffset from, DateTimeOffset until, Guid? source, CancellationToken token) { Calls++; return Task.FromResult<IReadOnlyList<ComponentDependencyResult>>([]); }
    }
    private static CreateAssessmentRequest Draft(string? title = "Rain access review", Guid? target = null) => new() { Title = title, TargetType = "DESTINATION", TargetId = target, TimeZoneId = "Asia/Colombo", PeriodStartsAt = "2026-10-01T09:00", PeriodEndsAt = "2026-10-01T12:00", Objective = "Inspect the marked entrance after rain." };

    [Fact(DisplayName = "COASTAL-RECORD-006 linking and editing a draft publishes immutable title, zone and UTC context")]
    [Trait("TestId", "COASTAL-RECORD-006")]
    public async Task LinkThenPublish()
    {
        await using var db = Db(); await SeedZones(db);
        var actor = Guid.NewGuid(); var target = Guid.NewGuid(); var plan = Guid.NewGuid(); var collector = new Collector();
        var service = new AssessmentApplicationService(db, new(db), collector);
        var created = await service.CreateAsync(Draft(), actor, "create", "new", default);
        var update = new UpdateAssessmentDraftRequest
        {
            ExpectedVersion = 1, Title = "  Linked beach review  ", TargetType = "DESTINATION",
            TargetId = target, SourceWorkflowId = plan, TimeZoneId = "Asia/Kathmandu",
            PeriodStartsAt = "2026-10-01T09:00", PeriodEndsAt = "2026-10-01T12:00",
            Objective = "Inspect the linked entrance."
        };
        var edited = await service.UpdateDraftAsync(created.Body.AssessmentId, update, actor, "edit", default);
        Assert.Equal("Linked beach review", edited.Title); Assert.Equal(2, edited.Version);
        Assert.Equal(target, edited.TargetId); Assert.Equal(plan, edited.SourceWorkflowId);
        var published = await service.SubmitDraftAsync(edited.AssessmentId, new() { ExpectedVersion = 2 }, actor, "publish", "publish", default);
        Assert.Equal("SUBMITTED", published.Body.WorkflowStatus); Assert.Equal(3, published.Body.Version);
        var saved = Assert.Single(await db.AssessmentDispatches.ToListAsync());
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<PublishedAssessmentDispatch>(saved.PayloadJson, OperationsValidation.JsonOptions)!;
        Assert.Equal("Linked beach review", snapshot.Title); Assert.Equal("Asia/Kathmandu", snapshot.TimeZoneId);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T03:15Z"), snapshot.PeriodStartsAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T06:15Z"), snapshot.PeriodEndsAt);
        Assert.Equal(target, snapshot.TargetId); Assert.Equal(plan, snapshot.SourceWorkflowId);
        Assert.Equal(actor, snapshot.ActorId); Assert.Equal(3, snapshot.PublishedVersion);
        Assert.Equal("Inspect the linked entrance.", snapshot.Objective); Assert.Equal(1, collector.Calls);
        Assert.Equal(new[] { "CREATED", "DRAFT_UPDATED", "SUBMITTED" }, await db.OperationsAudit.OrderBy(x => x.CreatedAt).Select(x => x.Action).ToArrayAsync());
        Assert.Equal(409, (await Assert.ThrowsAsync<CoastalOperationsException>(() => service.UpdateDraftAsync(edited.AssessmentId, update, actor, "late-edit", default))).StatusCode);
        Assert.Equal(saved.PayloadJson, (await db.AssessmentDispatches.SingleAsync()).PayloadJson);
        Assert.Equal("Linked beach review", (await db.Assessments.SingleAsync()).Title);
        Assert.Equal(3, await db.OperationsAudit.CountAsync());
    }

    [Fact(DisplayName = "COASTAL-RECORD-001 titled unlinked drafts get server IDs, survive replay and remain unpublished")]
    [Trait("TestId", "COASTAL-RECORD-001")]
    public async Task TitledDraftsWithoutInventedTargets()
    {
        await using var db = Db(); await SeedZones(db); var actor = Guid.NewGuid(); var collector = new Collector();
        var service = new AssessmentApplicationService(db, new IdempotencyStore(db), collector);
        var first = await service.CreateAsync(Draft("  Rain access review  "), actor, "create", "one", default);
        var replay = await service.CreateAsync(Draft("  Rain access review  "), actor, "create", "one", default);
        var second = await service.CreateAsync(Draft(), actor, "create", "two", default);
        Assert.True(replay.Replayed); Assert.Equal(first.SerializedBody, replay.SerializedBody);
        Assert.NotEqual(Guid.Empty, first.Body.AssessmentId); Assert.NotEqual(first.Body.AssessmentId, second.Body.AssessmentId);
        Assert.Equal("Rain access review", first.Body.Title); Assert.Equal(Guid.Empty, first.Body.TargetId);
        Assert.Equal("Asia/Colombo", first.Body.TimeZoneId); Assert.Equal("2026-10-01T09:00", first.Body.PeriodStartsLocal);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T03:30Z"), first.Body.PeriodStartsAt);
        Assert.Equal(2, await db.Assessments.CountAsync()); Assert.Equal(2, await db.OperationsAudit.CountAsync());
        var rejected = await Assert.ThrowsAsync<CoastalOperationsException>(() => service.SubmitDraftAsync(first.Body.AssessmentId, new() { ExpectedVersion = 1 }, actor, "publish", "publish", default));
        Assert.Equal(422, rejected.StatusCode); Assert.Equal("DRAFT", (await db.Assessments.FindAsync(first.Body.AssessmentId))!.WorkflowStatus);
        Assert.Equal(0, collector.Calls); Assert.Empty(await db.AssessmentDispatches.ToListAsync());
    }

    [Fact(DisplayName = "COASTAL-RECORD-002 reviewer All includes own drafts and title filters never reveal another owner's drafts")]
    [Trait("TestId", "COASTAL-RECORD-002")]
    public async Task ScopedDraftDiscovery()
    {
        await using var db = Db(); await SeedZones(db); var actor = Guid.NewGuid(); var service = new AssessmentApplicationService(db, new(db), new Collector());
        var own = await service.CreateAsync(Draft(), actor, "create", "own", default);
        await service.CreateAsync(Draft(), Guid.NewGuid(), "other", "other", default);
        var page = await service.GetQueueAsync(new() { Search = "rain", PageSize = 1 }, actor, true, default);
        Assert.Equal(own.Body.AssessmentId, Assert.Single(page.Items).AssessmentId); Assert.Null(page.NextCursor);
        Assert.Single((await service.GetQueueAsync(new() { OnlyMine = true, WorkflowStatus = "DRAFT" }, actor, true, default)).Items);
        Assert.Empty((await service.GetQueueAsync(new() { Search = "marked entrance" }, actor, true, default)).Items);
        Assert.Single((await service.GetQueueAsync(new() { RecordId = own.Body.AssessmentId }, actor, true, default)).Items);
        Assert.Empty((await service.GetQueueAsync(new() { RecordId = own.Body.AssessmentId, Search = "different" }, actor, true, default)).Items);
        Assert.Empty((await service.GetQueueAsync(new() { PublishedOnly = true }, actor, true, default)).Items);
    }

    [Theory(DisplayName = "COASTAL-TIMEZONE-001 location rules resolve each date independently and preserve fractional offsets")]
    [Trait("TestId", "COASTAL-TIMEZONE-001")]
    [InlineData("Asia/Colombo", "2026-10-01T09:00", "2026-10-01T12:00", "2026-10-01T03:30Z", "2026-10-01T06:30Z")]
    [InlineData("Asia/Kathmandu", "2026-10-01T09:00", "2026-10-01T12:00", "2026-10-01T03:15Z", "2026-10-01T06:15Z")]
    [InlineData("America/New_York", "2026-03-08T01:30", "2026-03-08T03:30", "2026-03-08T06:30Z", "2026-03-08T07:30Z")]
    public async Task LocationPeriod(string zone, string start, string end, string utcStart, string utcEnd)
    {
        await using var db = Db(); await SeedZones(db);
        var period = await OperationsTimeZones.ParsePeriodAsync(db, zone, start, end, default);
        Assert.Equal(DateTimeOffset.Parse(utcStart), period.StartsAt); Assert.Equal(DateTimeOffset.Parse(utcEnd), period.EndsAt);
        Assert.Equal(TimeSpan.Zero, period.StartsAt.Offset); Assert.Equal(start, OperationsTimeZones.LocalValue(period.StartsAt, zone));
    }
    [Theory(DisplayName = "COASTAL-TIMEZONE-002 invalid, ambiguous, nonexistent and reversed local periods are rejected")]
    [Trait("TestId", "COASTAL-TIMEZONE-002")]
    [InlineData("No/such_zone", "2026-10-01T09:00", "2026-10-01T12:00")]
    [InlineData("America/New_York", "2026-03-08T02:30", "2026-03-08T03:30")]
    [InlineData("America/New_York", "2026-11-01T01:30", "2026-11-01T03:30")]
    [InlineData("Etc/UTC", "not a date", "2026-10-01T12:00")]
    [InlineData("Etc/UTC", "2026-10-01T09:00+05:30", "2026-10-01T12:00")]
    [InlineData("Etc/UTC", "2026-10-01T09:00", "2026-10-01T09:00")]
    [InlineData("Etc/UTC", "2026-10-01T12:00", "2026-10-01T09:00")]
    public async Task BadLocalPeriods(string zone, string start, string end)
    {
        await using var db = Db(); await SeedZones(db);
        Assert.Equal(422, (await Assert.ThrowsAsync<CoastalOperationsException>(() => OperationsTimeZones.ParsePeriodAsync(db, zone, start, end, default))).StatusCode);
        Assert.Empty(await db.Assessments.ToListAsync()); Assert.Empty(await db.OperationsAudit.ToListAsync());
    }
    [Theory(DisplayName = "COASTAL-RECORD-003 titles reject empty and excessive input while legacy objectives remain compatible")]
    [Trait("TestId", "COASTAL-RECORD-003")]
    [InlineData(0)] [InlineData(161)] [InlineData(10000)]
    public void TitleBounds(int size)
    {
        Assert.Equal(422, Assert.Throws<CoastalOperationsException>(() => OperationsTimeZones.Title(new string(' ',size), "objective")).StatusCode);
        Assert.Equal(new string('a',160),OperationsTimeZones.Title(null,new string('a',200)));
        Assert.Equal(new string('a',160),OperationsTimeZones.Title(new string('a',160),"objective"));
        Assert.Throws<CoastalOperationsException>(() => OperationsTimeZones.Title(new string('a',161),"objective"));
    }
    [Fact(DisplayName = "COASTAL-OPTIONS-001 database catalogue and private association availability are scoped and never cached")]
    [Trait("TestId", "COASTAL-OPTIONS-001")]
    public async Task FormChoices()
    {
        await using var db = Db(); await SeedZones(db); var actor = Guid.NewGuid(); var service = new AssessmentApplicationService(db,new(db),new Collector());
        var own = await service.CreateAsync(Draft(target:Guid.NewGuid()),actor,"create","own",default);
        await service.CreateAsync(Draft(target:Guid.NewGuid()),Guid.NewGuid(),"other","other",default);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new("sub",actor.ToString()),new("permission","operations.assessment.read")],"test")) };
        var controller = new FormOptionsController(db,new DisconnectedCoastalReferencePort()) { ControllerContext = new ControllerContext { HttpContext=context } };
        var result = Assert.IsType<OkObjectResult>((await controller.GetFormOptions(default)).Result);
        var body=Assert.IsType<OperationsFormOptions>(result.Value);
        Assert.Equal("no-store",context.Response.Headers.CacheControl);
        Assert.Equal(419,body.TimeZones.Count); Assert.Contains(body.TimeZones,x=>x.Id=="Asia/Colombo" && x.Country=="Sri Lanka" && x.SourceVersion=="2026e" && x.CurrentOffsetMinutes==330 && x.RulesAvailable);
        Assert.Equal(own.Body.AssessmentId,Assert.Single(body.Assessments).Id);
        Assert.Equal("NOT_CONNECTED",body.Plans.Status); Assert.Empty(body.Plans.Items); Assert.Equal("NOT_CONNECTED",body.Targets.Status); Assert.Empty(body.Targets.Items);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new("sub",actor.ToString()),new("permission","operations.alert.create")],"test"));
        Assert.Empty(Assert.IsType<OperationsFormOptions>(Assert.IsType<OkObjectResult>((await controller.GetFormOptions(default)).Result).Value).Assessments);
    }
    [Fact(DisplayName = "COASTAL-RECORD-004 titled alert drafts remain searchable and cannot publish an absent coastal target")]
    [Trait("TestId", "COASTAL-RECORD-004")]
    public async Task UnlinkedAlert()
    {
        await using var db=Db(); await SeedZones(db); var actor=Guid.NewGuid(); var service=new AlertApplicationService(db,new(db));
        var alert=await service.CreateAsync(new() { Title="Rain warning",Description="Marked path closed",TargetType="DESTINATION",Severity="LOW",TimeZoneId="Etc/UTC",ValidFrom="2027-10-01T09:00",ValidUntil="2027-10-01T12:00" },actor,"create",default);
        Assert.NotEqual(Guid.Empty,alert.AlertId); Assert.Equal(Guid.Empty,alert.TargetId);
        Assert.Equal(alert.AlertId,Assert.Single((await service.GetQueueAsync(new(){Search="rain",Lifecycle="PROPOSED"},actor,true,default)).Items).AlertId);
        Assert.Empty((await service.GetQueueAsync(new(){Search="Marked path"},actor,true,default)).Items);
        Assert.Equal(422,(await Assert.ThrowsAsync<CoastalOperationsException>(()=>service.DecideAsync(alert.AlertId,new(){Decision="PUBLISH",ExpectedVersion=1},actor,"publish","publish",default))).StatusCode);
        Assert.Equal("PROPOSED",(await db.OperationalAlerts.SingleAsync()).Lifecycle); Assert.Empty(await db.AlertDecisions.ToListAsync()); Assert.Single(await db.OperationsAudit.ToListAsync());
    }
}
