using System.Text.Json;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Data.Entities;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Models.Dtos;
using Blueverse.CoastalPlanner.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class CoastalPlannerServiceTests
{
    [Fact]
    [Trait("TestId", "PLANNER-PEER-001")]
    public async Task Recommendation_completes_without_peer_services_and_persists_unavailability_notes()
    {
        await using var db = CreateDatabase();
        var planner = CreateService(db, new TestPeerServicesClient());
        var actorId = Guid.NewGuid();

        var result = await planner.GenerateRecommendationsAsync(CreateRecommendationRequest(), actorId);

        Assert.Equal("COMPLETED", result.Status);
        Assert.Empty(result.Candidates);
        Assert.Contains(result.UncertaintyNotes, note => note.Contains("Experience Catalogue endpoint has not responded", StringComparison.Ordinal));
        Assert.Contains(result.UncertaintyNotes, note => note.Contains("Coastal Operations endpoint has not responded", StringComparison.Ordinal));

        var workflow = await db.PlanningWorkflows.SingleAsync();
        var session = await db.Recommendations.SingleAsync();
        Assert.Equal("COMPLETED", workflow.Status);
        Assert.Equal(actorId, workflow.InitiatorUserId);
        Assert.Equal(actorId, session.UserId);
        Assert.Equal(result.RecommendationId, session.RecommendationId);
        Assert.NotNull(session.UncertaintyNotesJson);
        Assert.Equal(result.UncertaintyNotes, JsonSerializer.Deserialize<List<string>>(session.UncertaintyNotesJson!));
        Assert.NotNull(await planner.GetRecommendationAsync(result.RecommendationId, actorId));
        Assert.Null(await planner.GetRecommendationAsync(result.RecommendationId, Guid.NewGuid()));
        Assert.NotNull(await planner.GetWorkflowStatusAsync(result.WorkflowId, actorId));
        Assert.Null(await planner.GetWorkflowStatusAsync(result.WorkflowId, Guid.NewGuid()));
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-001")]
    public async Task Recommendations_use_request_window_and_exclude_unpublished_unavailable_and_unsafe_offerings()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var safeActivityId = Guid.NewGuid();
        var unsafeActivityId = Guid.NewGuid();
        var peer = new TestPeerServicesClient
        {
            CatalogueResult =
            (
                [
                    Offering(destinationId, safeActivityId, "AVAILABLE", "PUBLISHED"),
                    Offering(destinationId, Guid.NewGuid(), "AVAILABLE", "DRAFT"),
                    Offering(destinationId, Guid.NewGuid(), "CANCELLED", "PUBLISHED"),
                    Offering(destinationId, unsafeActivityId, "AVAILABLE", "PUBLISHED")
                ],
                true,
                null
            ),
            OperationsResult =
            (
                new PeerOperationStatusResponse(destinationId, "OPEN", []),
                true,
                null
            ),
            MarineResultForActivity = activityId => activityId == safeActivityId
                ? (Suitable(destinationId, safeActivityId), true, null)
                : (new PeerSuitabilityResponse(destinationId, activityId, "UNSUITABLE", DateTime.UtcNow, Guid.NewGuid(), "unsafe"), true, null)
        };
        var planner = CreateService(db, peer);
        var startsAt = DateTime.UtcNow.AddDays(1);
        var request = CreateRecommendationRequest(destinationId, startsAt, startsAt.AddHours(5), durationHours: 3, includeBiodiversity: false);

        var result = await planner.GenerateRecommendationsAsync(request, Guid.NewGuid());

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(safeActivityId, candidate.ActivityId);
        Assert.Equal(startsAt, candidate.ScheduledStart);
        Assert.Equal(startsAt.AddHours(3), candidate.ScheduledEnd);
        Assert.Equal("SUITABLE", candidate.Suitability.Status);
        Assert.NotEqual(Guid.Empty, candidate.Suitability.SafetyProfileId);
        Assert.Equal(3, result.ExcludedCandidatesCount);
        Assert.Equal(2, peer.MarineCallCount);
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-002")]
    public async Task Recommendation_omits_candidate_when_marine_endpoint_does_not_respond()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var peer = new TestPeerServicesClient
        {
            CatalogueResult = ([Offering(destinationId, activityId, "AVAILABLE", "PUBLISHED")], true, null),
            OperationsResult = (new PeerOperationStatusResponse(destinationId, "OPEN", []), true, null)
        };
        var planner = CreateService(db, peer);

        var result = await planner.GenerateRecommendationsAsync(CreateRecommendationRequest(destinationId), Guid.NewGuid());

        Assert.Empty(result.Candidates);
        Assert.Contains(result.UncertaintyNotes, note => note.Contains("Marine Conditions endpoint has not responded", StringComparison.Ordinal));
        Assert.Equal(1, result.ExcludedCandidatesCount);
    }

    [Fact]
    [Trait("TestId", "PLANNER-BIO-001")]
    public async Task Biodiversity_outage_returns_unavailable_without_fabricated_prediction_or_cache_entry()
    {
        await using var db = CreateDatabase();
        var planner = CreateService(db, new TestPeerServicesClient());
        var destinationId = Guid.NewGuid();

        var result = await planner.GetBiodiversityPredictionsAsync(destinationId, null);

        Assert.Equal("UNAVAILABLE", result.Status);
        Assert.Empty(result.PredictedSpecies);
        Assert.Null(result.ModelMetadata);
        Assert.Contains("Biodiversity ML endpoint has not responded", result.Limitations, StringComparison.Ordinal);
        Assert.Empty(await db.BiodiversityPredictions.ToListAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-BIO-002")]
    public async Task Valid_biodiversity_result_is_cached_until_source_based_expiry()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var peer = new TestPeerServicesClient
        {
            BiodiversityResult =
            (
                new PeerBiodiversityInferenceResponse(destinationId, activityId, "AVAILABLE",
                    [new PeerSpeciesInference(Guid.NewGuid(), "Chelonia mydas", "Green sea turtle", 0.84, "HIGH")],
                    "model-1", timestamp, "Context only."),
                true,
                null
            )
        };
        var planner = CreateService(db, peer);

        var first = await planner.GetBiodiversityPredictionsAsync(destinationId, activityId);
        var second = await planner.GetBiodiversityPredictionsAsync(destinationId, activityId);

        Assert.Equal("AVAILABLE", first.Status);
        Assert.Equal("model-1", first.ModelMetadata?.ModelVersion);
        Assert.Equal(timestamp, first.ModelMetadata?.InferenceTimestamp);
        Assert.Equal(0.84, Assert.Single(first.PredictedSpecies).HabitatSuitability);
        Assert.Equal(first.PredictedSpecies, second.PredictedSpecies);
        Assert.Equal(1, peer.BiodiversityCallCount);
        var cached = await db.BiodiversityPredictions.SingleAsync();
        Assert.Equal(timestamp.AddHours(6), cached.ExpiresAtUtc);
    }

    [Fact]
    [Trait("TestId", "PLANNER-BIO-003")]
    public async Task Invalid_biodiversity_probability_is_not_exposed_or_cached()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var peer = new TestPeerServicesClient
        {
            BiodiversityResult =
            (
                new PeerBiodiversityInferenceResponse(destinationId, activityId, "AVAILABLE",
                    [new PeerSpeciesInference(Guid.NewGuid(), "Species", "Common name", 1.2, "HIGH")],
                    "model-1", DateTime.UtcNow, null),
                true,
                null
            )
        };
        var planner = CreateService(db, peer);

        var result = await planner.GetBiodiversityPredictionsAsync(destinationId, activityId);

        Assert.Equal("UNAVAILABLE", result.Status);
        Assert.Empty(result.PredictedSpecies);
        Assert.Null(result.ModelMetadata);
        Assert.Empty(await db.BiodiversityPredictions.ToListAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITINERARY-001")]
    public async Task New_itinerary_statuses_are_unknown_until_peer_evidence_is_checked()
    {
        await using var db = CreateDatabase();
        var planner = CreateService(db, new TestPeerServicesClient());
        var startsAt = DateTime.UtcNow.AddDays(1);
        var item = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);

        var result = await planner.CreateItineraryAsync(
            new CreateItineraryRequestDto("Coastal day", null, startsAt, startsAt.AddHours(2), [item]), Guid.NewGuid());

        var saved = Assert.Single(result.Items);
        Assert.Equal("UNKNOWN", saved.LastSuitabilityStatus);
        Assert.Equal("UNKNOWN", saved.LastAvailabilityStatus);
        Assert.Equal("UNKNOWN", saved.LastOperationalStatus);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITINERARY-002")]
    public async Task Itinerary_update_rejects_duplicate_order_and_stale_concurrency_version()
    {
        await using var db = CreateDatabase();
        var planner = CreateService(db, new TestPeerServicesClient());
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddHours(3);
        var ownerId = Guid.NewGuid();
        var item = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);
        var itinerary = await planner.CreateItineraryAsync(
            new CreateItineraryRequestDto("Coastal day", null, startsAt, endsAt, [item]), ownerId);
        var duplicateOrder = new UpdateItineraryRequestDto("Coastal day", null, startsAt, endsAt, 1,
            [item, CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt.AddHours(1), startsAt.AddHours(2), 0)]);

        await Assert.ThrowsAsync<ArgumentException>(() => planner.UpdateItineraryAsync(itinerary.ItineraryId, duplicateOrder, ownerId));

        var validUpdate = duplicateOrder with { Items = [item], ConcurrencyVersion = 0 };
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            planner.UpdateItineraryAsync(itinerary.ItineraryId, validUpdate, ownerId));
        Assert.Equal(1, (await db.Itineraries.SingleAsync()).ConcurrencyVersion);
        Assert.Equal(1, await db.ItineraryItems.CountAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-VALIDATION-001")]
    public async Task Recommendation_rejects_duration_outside_requested_window_and_missing_actor()
    {
        await using var db = CreateDatabase();
        var planner = CreateService(db, new TestPeerServicesClient());
        var startsAt = DateTime.UtcNow.AddDays(1);
        var invalidDuration = CreateRecommendationRequest(Guid.NewGuid(), startsAt, startsAt.AddHours(1), durationHours: 2);

        await Assert.ThrowsAsync<ArgumentException>(() => planner.GenerateRecommendationsAsync(invalidDuration, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => planner.GenerateRecommendationsAsync(CreateRecommendationRequest(), Guid.Empty));
        Assert.Empty(await db.PlanningWorkflows.ToListAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-REEVAL-001")]
    public async Task Re_evaluation_returns_unknown_and_requires_review_when_peers_are_unavailable()
    {
        await using var db = CreateDatabase();
        var planner = CreateService(db, new TestPeerServicesClient());
        var startsAt = DateTime.UtcNow.AddDays(1);
        var ownerId = Guid.NewGuid();
        var itinerary = await planner.CreateItineraryAsync(
            new CreateItineraryRequestDto("Coastal day", null, startsAt, startsAt.AddHours(2),
                [CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0)]), ownerId);

        var result = await planner.ReEvaluateItineraryAsync(itinerary.ItineraryId, ownerId, new ItineraryReEvaluationRequestDto());

        var item = Assert.Single(Assert.IsType<ItineraryReEvaluationResultDto>(result).Items);
        Assert.False(result!.HasChanges); // Unavailable -> unavailable is not a new condition change.
        Assert.True(result.RequiresReview);
        Assert.Equal("UNKNOWN", item.CurrentAvailability);
        Assert.Equal("UNKNOWN", item.CurrentSuitability);
        Assert.Equal("UNKNOWN", item.CurrentOperationalStatus);
        Assert.Equal("REVIEW_CONDITIONS", item.SuggestedAction);
        var savedItem = await db.ItineraryItems.SingleAsync();
        Assert.Equal("UNKNOWN", savedItem.LastSuitabilityStatus);
        Assert.Contains("unavailable", savedItem.AdvisoryNote, StringComparison.OrdinalIgnoreCase);
    }

    private static CoastalPlannerDbContext CreateDatabase() => new(
        new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static CoastalPlannerService CreateService(CoastalPlannerDbContext db, IPeerServicesClient peer) =>
        new(db, peer, NullLogger<CoastalPlannerService>.Instance);

    private static RecommendationRequestDto CreateRecommendationRequest(
        Guid? destinationId = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        int durationHours = 2,
        bool includeBiodiversity = false)
    {
        var start = startsAt ?? DateTime.UtcNow.AddDays(1);
        return new RecommendationRequestDto(
            destinationId ?? Guid.NewGuid(), start, endsAt ?? start.AddHours(4), durationHours,
            null, "INTERMEDIATE", includeBiodiversity);
    }

    private static PeerCatalogueItem Offering(Guid destinationId, Guid activityId, string availability, string publication) =>
        new(destinationId, activityId, Guid.NewGuid(), $"Activity {activityId:N}", availability, publication, DateTime.UtcNow.AddHours(12), DateTime.UtcNow.AddDays(29), ["BEGINNER", "INTERMEDIATE", "ADVANCED"], DateTime.UtcNow, "Asia/Colombo");

    private static PeerSuitabilityResponse Suitable(Guid destinationId, Guid activityId) =>
        new(destinationId, activityId, "SUITABLE", DateTime.UtcNow.AddDays(1), Guid.NewGuid(), null, IsFresh: true);

    private static CreateItineraryItemRequestDto CreateItineraryItem(
        Guid destinationId, Guid activityId, DateTime startsAt, DateTime endsAt, int order) =>
        new(destinationId, activityId, Guid.NewGuid(), "Coastal activity", order, startsAt, endsAt);
}
