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

public sealed class AdditionalCoastalPlannerTests
{
    // Helper to create in‑memory DB
    private static CoastalPlannerDbContext CreateDatabase() => new(
        new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static CoastalPlannerService CreateService(CoastalPlannerDbContext db, IPeerServicesClient peer) =>
        new(db, peer, NullLogger<CoastalPlannerService>.Instance);

    [Fact]
    [Trait("TestId", "PLANNER-GETREC-001")]
    public async Task GetRecommendationAsync_returns_null_when_not_found()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var result = await service.GetRecommendationAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    [Trait("TestId", "PLANNER-GETWF-001")]
    public async Task GetWorkflowStatusAsync_returns_null_when_not_found()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var result = await service.GetWorkflowStatusAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-UNCERTAIN-001")]
    public async Task GetRecommendationAsync_handles_malformed_uncertainty_notes_gracefully()
    {
        await using var db = CreateDatabase();
        var recommendationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        // Insert a recommendation session with broken JSON for notes
        db.Recommendations.Add(new RecommendationSession
        {
            RecommendationId = recommendationId,
            WorkflowId = workflowId,
            UserId = userId,
            TargetDestinationId = Guid.NewGuid(),
            StartsAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddHours(2),
            DurationHours = 2,
            ExperienceLevel = "INTERMEDIATE",
            IncludeBiodiversityContext = false,
            CandidatesJson = "[]",
            UncertaintyNotesJson = "{ this is not json }",
            ExcludedCandidatesCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new TestPeerServicesClient());
        var result = await service.GetRecommendationAsync(recommendationId, userId);
        Assert.NotNull(result);
        // The malformed notes should be replaced with a fallback message
        Assert.Contains("Stored uncertainty notes could not be read", result.UncertaintyNotes[0]);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-UPDATE-DUPORDER-001")]
    public async Task UpdateItineraryAsync_throws_when_duplicate_order_indexes_are_provided()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddHours(2);
        var item = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);
        var itinerary = await service.CreateItineraryAsync(new CreateItineraryRequestDto("Trip", null, startsAt, endsAt, [item]), ownerId);

        var duplicateOrderReq = new UpdateItineraryRequestDto("Trip", null, startsAt, endsAt, 1,
            [item, CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt.AddHours(1), startsAt.AddHours(2), 0)]);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.UpdateItineraryAsync(itinerary.ItineraryId, duplicateOrderReq, ownerId));
    }

    [Fact]
    [Trait("TestId", "PLANNER-REEVAL-NOTFOUND-001")]
    public async Task ReEvaluateItineraryAsync_returns_null_when_itinerary_missing()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var result = await service.ReEvaluateItineraryAsync(Guid.NewGuid(), Guid.NewGuid(), new ItineraryReEvaluationRequestDto());
        Assert.Null(result);
    }

    // Helper to create a simple itinerary item DTO for tests
    private static CreateItineraryItemRequestDto CreateItineraryItem(
        Guid destinationId, Guid activityId, DateTime startsAt, DateTime endsAt, int order) =>
        new(destinationId, activityId, Guid.NewGuid(), "Activity", order, startsAt, endsAt);
}
