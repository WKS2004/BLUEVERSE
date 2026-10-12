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

public sealed class RecommendationDisplayTimeTests
{
    [Theory]
    [InlineData("Asia/Colombo", "2026-10-20T07:30:00")]
    [InlineData("America/New_York", "2026-10-19T22:00:00")]
    [InlineData("Pacific/Auckland", "2026-10-20T15:00:00")]
    [Trait("TestId", "PLANNER-DISPLAY-TIME-001")]
    public async Task Legacy_snapshot_displays_destination_clock_without_rewriting_instants_or_ownership(string zone, string expected)
    {
        await using var db = new CoastalPlannerDbContext(new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var owner = Guid.NewGuid();
        var instant = new DateTime(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);
        var candidate = new RecommendationCandidateDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Coastal walk",
            instant, instant.AddHours(1), "AVAILABLE", new("SUITABLE", instant, Guid.NewGuid()), "OPEN", null, .95, ["Fits the request"], zone);
        var stored = JsonSerializer.Serialize(new[] { candidate });
        var session = new RecommendationSession { RecommendationId = Guid.NewGuid(), UserId = owner,
            WorkflowId = Guid.NewGuid(), TargetDestinationId = candidate.DestinationId, CandidatesJson = stored };
        db.Recommendations.Add(session);
        await db.SaveChangesAsync();
        var service = new CoastalPlannerService(db, new TestPeerServicesClient(), NullLogger<CoastalPlannerService>.Instance);
        var result = await service.GetRecommendationAsync(session.RecommendationId, owner);
        var displayed = Assert.Single(result!.Candidates);
        Assert.Equal(expected, displayed.ScheduledStartLocal);
        Assert.NotNull(displayed.ScheduledEndLocal);
        Assert.Equal(instant, displayed.ScheduledStart);
        Assert.Equal(instant.AddHours(1), displayed.ScheduledEnd);
        Assert.Equal(zone, displayed.TimeZone);
        Assert.Equal(stored, (await db.Recommendations.SingleAsync()).CandidatesJson);
        Assert.Null(await service.GetRecommendationAsync(session.RecommendationId, Guid.NewGuid()));
    }

    [Fact]
    [Trait("TestId", "PLANNER-DISPLAY-TIME-002")]
    public async Task New_recommendations_include_local_clocks_in_saved_and_returned_candidates()
    {
        await using var db = new CoastalPlannerDbContext(new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var start = DateTime.UtcNow.AddDays(1);
        var destination = Guid.NewGuid(); var activity = Guid.NewGuid(); var owner = Guid.NewGuid();
        var peer = new TestPeerServicesClient {
            CatalogueResult = ([new PeerCatalogueItem(destination, activity, Guid.NewGuid(), "Lagoon walk", "AVAILABLE", "PUBLISHED",
                start, start.AddHours(4), ["INTERMEDIATE"], DateTime.UtcNow, "Asia/Colombo")], true, null),
            MarineResult = (new(destination, activity, "SUITABLE", start, Guid.NewGuid(), null, IsFresh: true), true, null),
            OperationsResult = (new(destination, "OPEN", []), true, null)
        };
        var service = new CoastalPlannerService(db, peer, NullLogger<CoastalPlannerService>.Instance);
        var result = await service.GenerateRecommendationsAsync(new(destination, start, start.AddHours(4), 1, [activity], "INTERMEDIATE", false), owner);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(start.AddMinutes(330).ToString("yyyy-MM-dd'T'HH:mm:ss"), candidate.ScheduledStartLocal);
        Assert.Equal(start.AddHours(1).AddMinutes(330).ToString("yyyy-MM-dd'T'HH:mm:ss"), candidate.ScheduledEndLocal);
        Assert.Contains("ScheduledStartLocal", (await db.Recommendations.SingleAsync()).CandidatesJson!);
        Assert.Equal(candidate.ScheduledStartLocal, Assert.Single((await service.GetRecommendationAsync(result.RecommendationId, owner))!.Candidates).ScheduledStartLocal);
    }
}
