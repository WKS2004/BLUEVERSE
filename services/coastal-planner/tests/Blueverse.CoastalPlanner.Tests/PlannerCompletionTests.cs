using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

// PROJECT_REQUIREMENTS 16, 17; Member 3 component acceptance: schedules,
// source validation, owner isolation, durable intent and condition evidence.
public sealed class PlannerCompletionTests
{
    private static CoastalPlannerDbContext Database() => new(new DbContextOptionsBuilder<CoastalPlannerDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private static CoastalPlannerService Service(CoastalPlannerDbContext db, IPeerServicesClient peer) =>
        new(db, peer, NullLogger<CoastalPlannerService>.Instance);
    private static PeerCatalogueItem Offering(Guid destination, Guid activity, DateTime start) => new(destination, activity,
        Guid.NewGuid(), "Guided lagoon walk", "AVAILABLE", "PUBLISHED", start, start.AddHours(4),
        ["BEGINNER", "INTERMEDIATE"], DateTime.UtcNow, "Asia/Colombo");
    private static TestPeerServicesClient Peer(PeerCatalogueItem item, DateTime start, string suitability = "SUITABLE") => new()
    {
        CatalogueResult = ([item], true, null),
        MarineResult = (new(item.DestinationId, item.ActivityId, suitability, start, Guid.NewGuid(), null, IsFresh: true), true, null),
        OperationsResult = (new(item.DestinationId, "OPEN", []), true, null)
    };
    private static RecommendationRequestDto Request(Guid destination, DateTime start) =>
        new(destination, start, start.AddHours(6), 2, [], "INTERMEDIATE", false);
    private static CreateItineraryItemRequestDto Stop(PeerCatalogueItem item, DateTime start, int order = 0) =>
        new(item.DestinationId, item.ActivityId, item.OfferingId, item.Title, order, start, start.AddHours(2));

    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("zone")]
    [Trait("TestId", "PLANNER-SCHEDULE-001")]
    public async Task Missing_or_stale_schedule_evidence_never_becomes_a_recommendation(string problem)
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var item = Offering(Guid.NewGuid(), Guid.NewGuid(), start);
        item = problem switch { "missing" => item with { AvailableUntil = null }, "stale" => item with { CheckedAt = DateTime.UtcNow.AddHours(-1) }, _ => item with { TimeZone = "unknown-zone" } };
        var result = await Service(db, Peer(item, start)).GenerateRecommendationsAsync(Request(item.DestinationId, start), Guid.NewGuid());
        Assert.Empty(result.Candidates);
        Assert.Equal("DEPENDENCIES_UNAVAILABLE", result.Outcome);
        Assert.Contains(result.UncertaintyNotes, note => note.Contains("schedule or availability"));
        Assert.Equal(result.Outcome, (await db.Recommendations.SingleAsync()).Outcome);
    }

    [Fact]
    [Trait("TestId", "PLANNER-FRESHNESS-001")]
    public async Task Suitable_status_without_source_freshness_cannot_be_recommended()
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var item = Offering(Guid.NewGuid(), Guid.NewGuid(), start);
        var peer = Peer(item, start);
        peer.MarineResult = (peer.MarineResult.Result! with { IsFresh = false }, true, null);
        var result = await Service(db, peer).GenerateRecommendationsAsync(Request(item.DestinationId, start), Guid.NewGuid());
        Assert.Empty(result.Candidates);
        Assert.Equal("DEPENDENCIES_UNAVAILABLE", result.Outcome);
        Assert.Equal(1, result.ExcludedCandidatesCount);
    }

    [Fact]
    [Trait("TestId", "PLANNER-WORKFLOW-FAILURE-001")]
    public async Task Dependency_exception_closes_the_workflow_without_persisting_a_false_result_or_exception_details()
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var item = Offering(Guid.NewGuid(), Guid.NewGuid(), start);
        var peer = Peer(item, start);
        peer.MarineResultForActivity = _ => throw new InvalidOperationException("private provider exception");
        var owner = Guid.NewGuid();
        var service = Service(db, peer);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateRecommendationsAsync(Request(item.DestinationId, start), owner));
        var workflow = await db.PlanningWorkflows.SingleAsync();
        Assert.Equal("FAILED", workflow.Status);
        Assert.NotNull(workflow.CompletedAtUtc);
        Assert.DoesNotContain("private", workflow.FailureReason);
        Assert.Empty(await db.Recommendations.ToListAsync());
        Assert.Null(await service.GetWorkflowStatusAsync(workflow.WorkflowId, Guid.NewGuid()));
    }

    [Fact]
    [Trait("TestId", "PLANNER-SCHEDULE-002")]
    public async Task Schedule_intersection_moves_start_without_inventing_an_offering_time()
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var item = Offering(Guid.NewGuid(), Guid.NewGuid(), start.AddHours(1));
        var peer = Peer(item, start.AddHours(1));
        var result = await Service(db, peer).GenerateRecommendationsAsync(Request(item.DestinationId, start), Guid.NewGuid());
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(start.AddHours(1), candidate.ScheduledStart);
        Assert.Equal(start.AddHours(3), candidate.ScheduledEnd);
        Assert.Equal("Asia/Colombo", candidate.TimeZone);
        Assert.Equal("MATCHES_FOUND", result.Outcome);
        Assert.NotEqual(Guid.Empty, candidate.Suitability.SafetyProfileId);
    }

    [Fact]
    [Trait("TestId", "PLANNER-SCHEDULE-003")]
    public async Task Experience_level_and_too_short_schedule_exclude_candidates_before_marine_calls()
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var item = Offering(Guid.NewGuid(), Guid.NewGuid(), start) with { ExperienceLevels = ["ADVANCED"] };
        var peer = Peer(item, start);
        var service = Service(db, peer);
        Assert.Empty((await service.GenerateRecommendationsAsync(Request(item.DestinationId, start), Guid.NewGuid())).Candidates);
        peer.CatalogueResult = ([item with { ExperienceLevels = ["INTERMEDIATE"], AvailableUntil = start.AddMinutes(59) }], true, null);
        Assert.Empty((await service.GenerateRecommendationsAsync(Request(item.DestinationId, start), Guid.NewGuid())).Candidates);
        Assert.Equal(0, peer.MarineCallCount);
    }

    [Fact]
    [Trait("TestId", "PLANNER-SAVE-001")]
    public async Task Saving_a_recommendation_checks_owner_snapshot_and_expiration()
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var owner = Guid.NewGuid();
        var item = Offering(Guid.NewGuid(), Guid.NewGuid(), start);
        var service = Service(db, Peer(item, start));
        var result = await service.GenerateRecommendationsAsync(Request(item.DestinationId, start), owner);
        var request = new CreateItineraryRequestDto("Lagoon morning", null, start, start.AddHours(2), [Stop(item, start)], result.RecommendationId);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(request, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(request with { Items = [Stop(item, start) with { Title = "Forged title" }] }, owner));
        Assert.Empty(await db.Itineraries.ToListAsync());
        var saved = await service.CreateItineraryAsync(request, owner);
        Assert.Equal(owner, saved.OwnerUserId);
        Assert.Equal("UNKNOWN", Assert.Single(saved.Items).LastSuitabilityStatus);
        (await db.Recommendations.SingleAsync()).CreatedAtUtc = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(request, owner));
        Assert.Single(await db.Itineraries.ToListAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-STOPS-001")]
    public async Task Reorder_preserves_identity_and_review_evidence_but_reschedule_invalidates_it()
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var owner = Guid.NewGuid();
        var first = Offering(Guid.NewGuid(), Guid.NewGuid(), start);
        var second = Offering(first.DestinationId, Guid.NewGuid(), start.AddHours(2));
        var service = Service(db, new TestPeerServicesClient());
        var saved = await service.CreateItineraryAsync(new("Trip", null, start, start.AddHours(6), [Stop(first, start), Stop(second, start.AddHours(2), 1)]), owner);
        var persisted = await db.ItineraryItems.SingleAsync(i => i.ItemId == saved.Items[0].ItemId);
        persisted.LastSuitabilityStatus = "CAUTION";
        await db.SaveChangesAsync();
        var edit = new UpdateItineraryRequestDto("Trip edited", null, start, start.AddHours(6), 1,
            [Stop(second, start.AddHours(2), 0) with { ItemId = saved.Items[1].ItemId }, Stop(first, start, 1) with { ItemId = saved.Items[0].ItemId }]);
        var updated = await service.UpdateItineraryAsync(saved.ItineraryId, edit, owner);
        Assert.Equal(saved.Items[1].ItemId, updated!.Items[0].ItemId);
        Assert.Equal("CAUTION", updated.Items[1].LastSuitabilityStatus);
        var rescheduled = edit with { ConcurrencyVersion = 2, Items = [edit.Items[0], edit.Items[1] with { ScheduledStart = start.AddMinutes(15), ScheduledEnd = start.AddHours(2) }] };
        updated = await service.UpdateItineraryAsync(saved.ItineraryId, rescheduled, owner);
        Assert.Equal(saved.Items[0].ItemId, updated!.Items[1].ItemId);
        Assert.Equal("UNKNOWN", updated.Items[1].LastSuitabilityStatus);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateItineraryAsync(saved.ItineraryId, edit, owner));
        Assert.Equal(3, (await service.GetItineraryAsync(saved.ItineraryId, owner))!.ConcurrencyVersion);
    }

    [Fact]
    [Trait("TestId", "PLANNER-STOPS-002")]
    public async Task Duplicate_and_overlapping_stops_are_rejected_without_persistence()
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var item = Offering(Guid.NewGuid(), Guid.NewGuid(), start);
        var stop = Stop(item, start);
        var service = Service(db, new TestPeerServicesClient());
        var request = new CreateItineraryRequestDto("Trip", null, start, start.AddHours(4), [stop, stop with { OrderIndex = 1 }]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(request, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(request with { Items = [stop, Stop(item with { OfferingId = Guid.NewGuid() }, start.AddHours(1), 1)] }, Guid.NewGuid()));
        Assert.Empty(await db.Itineraries.ToListAsync());
        Assert.Empty(await db.ItineraryItems.ToListAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-HISTORY-001")]
    public async Task Repeated_caution_stays_actionable_without_inventing_changes_and_history_is_owner_scoped()
    {
        await using var db = Database();
        var start = DateTime.UtcNow.AddDays(1);
        var item = Offering(Guid.NewGuid(), Guid.NewGuid(), start);
        var owner = Guid.NewGuid();
        var service = Service(db, Peer(item, start, "CAUTION"));
        var saved = await service.CreateItineraryAsync(new("Trip", null, start, start.AddHours(2), [Stop(item, start)]), owner);
        var first = await service.ReEvaluateItineraryAsync(saved.ItineraryId, owner, new());
        var second = await service.ReEvaluateItineraryAsync(saved.ItineraryId, owner, new());
        Assert.True(first!.HasChanges);
        Assert.False(second!.HasChanges);
        Assert.True(first.RequiresReview);
        Assert.True(second.RequiresReview);
        Assert.Equal("UNKNOWN", first.Items[0].PreviousSuitability);
        Assert.Equal("CAUTION", second.Items[0].PreviousSuitability);
        Assert.Equal(3, second.ConcurrencyVersion);
        Assert.NotEqual(first.EvaluationId, second.EvaluationId);
        Assert.Equal(2, (await service.GetEvaluationHistoryAsync(saved.ItineraryId, owner))!.Count);
        Assert.Null(await service.GetEvaluationHistoryAsync(saved.ItineraryId, Guid.NewGuid()));
        var current = await service.GetItineraryAsync(saved.ItineraryId, owner);
        Assert.Equal(start, current!.Items[0].ScheduledStart);
        Assert.Equal(saved.Items[0].ItemId, current.Items[0].ItemId);
    }
}
