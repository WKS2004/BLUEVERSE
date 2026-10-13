using Blueverse.ExperienceBiodiversity.Models;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.ExperienceBiodiversity.Data;

public sealed record SyntheticDemoDataSeedResult(
    int DestinationsAdded,
    int ActivitiesAdded,
    int OfferingsAdded,
    int SchedulesAdded)
{
    public int TotalRecordsAdded =>
        DestinationsAdded + ActivitiesAdded + OfferingsAdded + SchedulesAdded;
}

/// <summary>
/// Adds a small, repeatable catalogue for explicitly requested local demos.
/// This seeder is deliberately not called during normal service startup.
/// </summary>
public static class SyntheticDemoDataSeeder
{
    private static readonly Guid[] DestinationIds =
    [
        Guid.Parse("d3000000-0000-4000-8000-000000000001"),
        Guid.Parse("d3000000-0000-4000-8000-000000000002"),
        Guid.Parse("d3000000-0000-4000-8000-000000000003"),
        Guid.Parse("d3000000-0000-4000-8000-000000000004")
    ];

    private static readonly Guid[] ActivityIds =
    [
        Guid.Parse("a3000000-0000-4000-8000-000000000001"),
        Guid.Parse("a3000000-0000-4000-8000-000000000002"),
        Guid.Parse("a3000000-0000-4000-8000-000000000003"),
        Guid.Parse("a3000000-0000-4000-8000-000000000004")
    ];

    private static readonly Guid[] OfferingIds =
    [
        Guid.Parse("f3000000-0000-4000-8000-000000000001"),
        Guid.Parse("f3000000-0000-4000-8000-000000000002"),
        Guid.Parse("f3000000-0000-4000-8000-000000000003"),
        Guid.Parse("f3000000-0000-4000-8000-000000000004"),
        Guid.Parse("f3000000-0000-4000-8000-000000000005"),
        Guid.Parse("f3000000-0000-4000-8000-000000000006"),
        Guid.Parse("f3000000-0000-4000-8000-000000000007"),
        Guid.Parse("f3000000-0000-4000-8000-000000000008")
    ];

    private static readonly Guid[] ScheduleIds =
    [
        Guid.Parse("53000000-0000-4000-8000-000000000001"),
        Guid.Parse("53000000-0000-4000-8000-000000000002"),
        Guid.Parse("53000000-0000-4000-8000-000000000003"),
        Guid.Parse("53000000-0000-4000-8000-000000000004"),
        Guid.Parse("53000000-0000-4000-8000-000000000005"),
        Guid.Parse("53000000-0000-4000-8000-000000000006"),
        Guid.Parse("53000000-0000-4000-8000-000000000007"),
        Guid.Parse("53000000-0000-4000-8000-000000000008")
    ];

    private sealed record DestinationSeed(
        Guid Id, string Name, string Slug, string Region, double Latitude, double Longitude, string Description);

    private sealed record ActivitySeed(Guid Id, string Code, string Name, string Category, string Description);

    private sealed record OfferingSeed(
        Guid Id,
        string Title,
        Guid DestinationId,
        Guid ActivityId,
        string Description,
        decimal Price,
        int DurationMinutes,
        int Capacity,
        string[] ExperienceLevels);

    private static readonly DestinationSeed[] Destinations =
    [
        new(DestinationIds[0], "Synthetic Bentota Mangrove Coast", "synthetic-bentota-mangrove-coast", "Western Province", 6.4210, 79.9950,
            "Synthetic demo destination for mangrove kayaking and calm-water nature trips."),
        new(DestinationIds[1], "Synthetic Rekawa Turtle Coast", "synthetic-rekawa-turtle-coast", "Southern Province", 6.0470, 80.8460,
            "Synthetic demo destination for guided shoreline observation and conservation education."),
        new(DestinationIds[2], "Synthetic Batticaloa Lagoon", "synthetic-batticaloa-lagoon", "Eastern Province", 7.7160, 81.7000,
            "Synthetic demo destination for lagoon paddling and low-impact bird watching."),
        new(DestinationIds[3], "Synthetic Mannar Sandbar Coast", "synthetic-mannar-sandbar-coast", "Northern Province", 9.0500, 79.8500,
            "Synthetic demo destination for coastal ecology walks and responsible marine observation.")
    ];

    private static readonly ActivitySeed[] Activities =
    [
        new(ActivityIds[0], "DEMO_MANGROVE_KAYAK", "Synthetic Mangrove Kayaking", "Nature",
            "Synthetic guided kayak activity focused on mangrove habitats and low-impact access."),
        new(ActivityIds[1], "DEMO_COASTAL_ECO_WALK", "Synthetic Coastal Ecology Walk", "Nature",
            "Synthetic small-group walk covering shoreline habitats and responsible wildlife viewing."),
        new(ActivityIds[2], "DEMO_LAGOON_BIRDING", "Synthetic Lagoon Bird Watching", "Wildlife",
            "Synthetic dawn observation activity with a local nature guide."),
        new(ActivityIds[3], "DEMO_REEF_SNORKEL", "Synthetic Reef Snorkeling", "WaterSports",
            "Synthetic guided snorkeling activity with a conservation and safety briefing.")
    ];

    private static readonly OfferingSeed[] Offerings =
    [
        new(OfferingIds[0], "Synthetic Bentota Mangrove Kayak", DestinationIds[0], ActivityIds[0], "Demo listing with a calm-water route and a habitat briefing.", 24m, 120, 8, ["BEGINNER"]),
        new(OfferingIds[1], "Synthetic Bentota Estuary Bird Walk", DestinationIds[0], ActivityIds[2], "Demo listing with an early-morning estuary observation window.", 18m, 90, 10, ["BEGINNER", "INTERMEDIATE"]),
        new(OfferingIds[2], "Synthetic Rekawa Ecology Walk", DestinationIds[1], ActivityIds[1], "Demo listing with a guided shoreline and conservation interpretation route.", 16m, 75, 10, ["BEGINNER"]),
        new(OfferingIds[3], "Synthetic Rekawa Responsible Wildlife Watch", DestinationIds[1], ActivityIds[2], "Demo listing with a small group and a no-disturbance observation policy.", 22m, 100, 6, ["BEGINNER", "INTERMEDIATE"]),
        new(OfferingIds[4], "Synthetic Batticaloa Lagoon Paddle", DestinationIds[2], ActivityIds[0], "Demo listing for a sheltered lagoon paddle with a guide.", 20m, 110, 8, ["BEGINNER"]),
        new(OfferingIds[5], "Synthetic Batticaloa Birding Session", DestinationIds[2], ActivityIds[2], "Demo listing for a guided lagoon birding session.", 14m, 80, 8, ["BEGINNER", "INTERMEDIATE"]),
        new(OfferingIds[6], "Synthetic Mannar Coastal Nature Walk", DestinationIds[3], ActivityIds[1], "Demo listing for an interpreted coastal walk with a small group.", 17m, 90, 10, ["BEGINNER"]),
        new(OfferingIds[7], "Synthetic Mannar Reef Snorkel", DestinationIds[3], ActivityIds[3], "Demo listing for a guided reef session with a pre-trip safety briefing.", 32m, 150, 6, ["INTERMEDIATE", "ADVANCED"])
    ];

    public static async Task<SyntheticDemoDataSeedResult> SeedAsync(
        ExperienceBiodiversityDbContext context,
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = (utcNow ?? DateTimeOffset.UtcNow).ToUniversalTime();

        var seededDestinationIds = Destinations.Select(item => item.Id).ToArray();
        var seededSlugs = Destinations.Select(item => item.Slug).ToArray();
        var existingDestinations = await context.Destinations
            .Where(item => seededDestinationIds.Contains(item.Id) || seededSlugs.Contains(item.Slug))
            .ToListAsync(cancellationToken);
        var destinationIdsBySlug = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var destinationsToAdd = new List<Destination>();
        foreach (var seed in Destinations)
        {
            var existingById = existingDestinations.SingleOrDefault(item => item.Id == seed.Id);
            var existingBySlug = existingDestinations.SingleOrDefault(item => item.Slug == seed.Slug);
            if (existingById is not null && !string.Equals(existingById.Slug, seed.Slug, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A synthetic demo destination identifier is already used by another catalogue record.");
            }

            var existing = existingBySlug ?? existingById;
            if (existing is null)
            {
                existing = new Destination
                {
                    Id = seed.Id,
                    Name = seed.Name,
                    Slug = seed.Slug,
                    Description = seed.Description,
                    Region = seed.Region,
                    Latitude = seed.Latitude,
                    Longitude = seed.Longitude,
                    Status = PublicationStatus.Published,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                destinationsToAdd.Add(existing);
            }

            destinationIdsBySlug.Add(seed.Slug, existing.Id);
        }

        var seededActivityIds = Activities.Select(item => item.Id).ToArray();
        var seededActivityCodes = Activities.Select(item => item.Code).ToArray();
        var existingActivities = await context.Activities
            .Where(item => seededActivityIds.Contains(item.Id) || seededActivityCodes.Contains(item.Code))
            .ToListAsync(cancellationToken);
        var activityIdsByCode = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var activitiesToAdd = new List<Activity>();
        foreach (var seed in Activities)
        {
            var existingById = existingActivities.SingleOrDefault(item => item.Id == seed.Id);
            var existingByCode = existingActivities.SingleOrDefault(item => item.Code == seed.Code);
            if (existingById is not null && !string.Equals(existingById.Code, seed.Code, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A synthetic demo activity identifier is already used by another catalogue record.");
            }

            var existing = existingByCode ?? existingById;
            if (existing is null)
            {
                existing = new Activity
                {
                    Id = seed.Id,
                    Code = seed.Code,
                    Name = seed.Name,
                    Description = seed.Description,
                    Category = seed.Category,
                    Status = PublicationStatus.Published,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                activitiesToAdd.Add(existing);
            }

            activityIdsByCode.Add(seed.Code, existing.Id);
        }

        var seededOfferingIds = Offerings.Select(item => item.Id).ToArray();
        var existingOfferings = await context.Offerings
            .Where(item => seededOfferingIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var offeringsToAdd = Offerings
            .Where(item => !existingOfferings.Contains(item.Id))
            .Select(seed => new Offering
            {
                Id = seed.Id,
                DestinationId = destinationIdsBySlug[Destinations.Single(destination => destination.Id == seed.DestinationId).Slug],
                ActivityId = activityIdsByCode[Activities.Single(activity => activity.Id == seed.ActivityId).Code],
                Title = seed.Title,
                Description = seed.Description,
                Price = seed.Price,
                Currency = "USD",
                DurationMinutes = seed.DurationMinutes,
                MaxCapacity = seed.Capacity,
                ExperienceLevels = [.. seed.ExperienceLevels],
                Status = PublicationStatus.Published,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        var seededScheduleIds = ScheduleIds;
        var existingScheduleIds = await context.Schedules
            .Where(item => seededScheduleIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var localStart = now.ToOffset(TimeSpan.FromHours(5.5)).Date.AddDays(-1).AddHours(6);
        var startsAt = new DateTimeOffset(localStart, TimeSpan.FromHours(5.5)).ToUniversalTime();
        var schedulesToAdd = Offerings
            .Select((offering, index) => (offering, index))
            .Where(pair => !existingScheduleIds.Contains(ScheduleIds[pair.index]))
            .Select(pair => new Schedule
            {
                Id = ScheduleIds[pair.index],
                OfferingId = pair.offering.Id,
                StartsAt = startsAt,
                EndsAt = startsAt.AddDays(180),
                TimeZoneId = "Asia/Colombo",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        await context.Destinations.AddRangeAsync(destinationsToAdd, cancellationToken);
        await context.Activities.AddRangeAsync(activitiesToAdd, cancellationToken);
        await context.Offerings.AddRangeAsync(offeringsToAdd, cancellationToken);
        await context.Schedules.AddRangeAsync(schedulesToAdd, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new SyntheticDemoDataSeedResult(
            destinationsToAdd.Count,
            activitiesToAdd.Count,
            offeringsToAdd.Count,
            schedulesToAdd.Count);
    }
}
