using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class CoastalPlannerDbContextTests
{
    [Fact]
    [Trait("TestId", "PLANNER-DB-CASCADE-001")]
    public async Task Itinerary_deletion_cascades_to_itinerary_items()
    {
        await using var db = CreateDatabase();
        var itineraryId = Guid.NewGuid();
        var itinerary = new Itinerary
        {
            ItineraryId = itineraryId,
            OwnerUserId = Guid.NewGuid(),
            Title = "Cascade Test",
            StartsAtUtc = DateTime.UtcNow.AddDays(1),
            EndsAtUtc = DateTime.UtcNow.AddDays(2),
            ConcurrencyVersion = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Items =
            [
                new ItineraryItem
                {
                    ItemId = Guid.NewGuid(),
                    DestinationId = Guid.NewGuid(),
                    ActivityId = Guid.NewGuid(),
                    Title = "Item 1",
                    OrderIndex = 0,
                    ScheduledStartUtc = DateTime.UtcNow.AddDays(1),
                    ScheduledEndUtc = DateTime.UtcNow.AddDays(1).AddHours(2),
                    LastSuitabilityStatus = "UNKNOWN",
                    LastAvailabilityStatus = "UNKNOWN",
                    LastOperationalStatus = "UNKNOWN"
                },
                new ItineraryItem
                {
                    ItemId = Guid.NewGuid(),
                    DestinationId = Guid.NewGuid(),
                    ActivityId = Guid.NewGuid(),
                    Title = "Item 2",
                    OrderIndex = 1,
                    ScheduledStartUtc = DateTime.UtcNow.AddDays(1).AddHours(2),
                    ScheduledEndUtc = DateTime.UtcNow.AddDays(1).AddHours(4),
                    LastSuitabilityStatus = "UNKNOWN",
                    LastAvailabilityStatus = "UNKNOWN",
                    LastOperationalStatus = "UNKNOWN"
                }
            ]
        };

        db.Itineraries.Add(itinerary);
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Itineraries.CountAsync());
        Assert.Equal(2, await db.ItineraryItems.CountAsync());

        db.Itineraries.Remove(itinerary);
        await db.SaveChangesAsync();

        Assert.Equal(0, await db.Itineraries.CountAsync());
        Assert.Equal(0, await db.ItineraryItems.CountAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-DB-CASCADE-002")]
    public async Task Planning_workflow_deletion_cascades_to_recommendation_session()
    {
        await using var db = CreateDatabase();
        var workflowId = Guid.NewGuid();
        var workflow = new PlanningWorkflow
        {
            WorkflowId = workflowId,
            WorkflowType = "TOURIST_RECOMMENDATION",
            Status = "COMPLETED",
            InitiatorUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };
        var recommendation = new RecommendationSession
        {
            RecommendationId = Guid.NewGuid(),
            WorkflowId = workflowId,
            UserId = workflow.InitiatorUserId,
            TargetDestinationId = Guid.NewGuid(),
            StartsAtUtc = DateTime.UtcNow.AddDays(1),
            EndsAtUtc = DateTime.UtcNow.AddDays(2),
            DurationHours = 4,
            ExperienceLevel = "INTERMEDIATE",
            IncludeBiodiversityContext = false,
            CandidatesJson = "[]",
            UncertaintyNotesJson = "[]",
            CreatedAtUtc = DateTime.UtcNow
        };

        db.PlanningWorkflows.Add(workflow);
        db.Recommendations.Add(recommendation);
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.PlanningWorkflows.CountAsync());
        Assert.Equal(1, await db.Recommendations.CountAsync());

        db.PlanningWorkflows.Remove(workflow);
        await db.SaveChangesAsync();

        Assert.Equal(0, await db.PlanningWorkflows.CountAsync());
        Assert.Equal(0, await db.Recommendations.CountAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-DB-CONCURRENCY-001")]
    public async Task ConcurrencyVersion_is_configured_as_concurrency_token()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var itineraryId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await using (var db1 = new CoastalPlannerDbContext(options))
        {
            db1.Itineraries.Add(new Itinerary
            {
                ItineraryId = itineraryId,
                OwnerUserId = ownerId,
                Title = "Initial Title",
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                EndsAtUtc = DateTime.UtcNow.AddDays(2),
                ConcurrencyVersion = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db1.SaveChangesAsync();
        }

        // Two concurrent contexts load the same entity
        await using var dbUserA = new CoastalPlannerDbContext(options);
        await using var dbUserB = new CoastalPlannerDbContext(options);

        var itinA = await dbUserA.Itineraries.SingleAsync(i => i.ItineraryId == itineraryId);
        var itinB = await dbUserB.Itineraries.SingleAsync(i => i.ItineraryId == itineraryId);

        // User A updates and increments ConcurrencyVersion to 2
        itinA.Title = "Updated by A";
        itinA.ConcurrencyVersion = 2;
        await dbUserA.SaveChangesAsync();

        // User B tries to update with stale version
        itinB.Title = "Updated by B";
        itinB.ConcurrencyVersion = 2; // will conflict because DB already has version 2

        // Since EF in-memory tracks concurrency tokens:
        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbUserB.SaveChangesAsync());
        Assert.NotNull(ex);
    }

    [Fact]
    [Trait("TestId", "PLANNER-DB-MODEL-001")]
    public void ModelBuilder_configures_default_schema_and_entity_metadata()
    {
        using var db = CreateDatabase();
        var model = db.Model;

        Assert.Equal("coastal_planner", model.GetDefaultSchema());

        var workflowEntity = model.FindEntityType(typeof(PlanningWorkflow));
        Assert.NotNull(workflowEntity);
        Assert.Equal("planning_workflows", workflowEntity.GetTableName());

        var itineraryEntity = model.FindEntityType(typeof(Itinerary));
        Assert.NotNull(itineraryEntity);
        Assert.Equal("itineraries", itineraryEntity.GetTableName());
        var versionProp = itineraryEntity.FindProperty(nameof(Itinerary.ConcurrencyVersion));
        Assert.NotNull(versionProp);
        Assert.True(versionProp.IsConcurrencyToken);

        var itemEntity = model.FindEntityType(typeof(ItineraryItem));
        Assert.NotNull(itemEntity);
        Assert.Equal("itinerary_items", itemEntity.GetTableName());

        var recommendationEntity = model.FindEntityType(typeof(RecommendationSession));
        Assert.NotNull(recommendationEntity);
        Assert.Equal("recommendations", recommendationEntity.GetTableName());

        var bioEntity = model.FindEntityType(typeof(BiodiversityPredictionCache));
        Assert.NotNull(bioEntity);
        Assert.Equal("BiodiversityPredictionCache", bioEntity.GetTableName());
    }

    private static CoastalPlannerDbContext CreateDatabase() => new(
        new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
