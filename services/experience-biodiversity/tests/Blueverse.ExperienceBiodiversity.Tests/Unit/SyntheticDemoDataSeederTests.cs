using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Unit;

public sealed class SyntheticDemoDataSeederTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("CaseId", "EXP-DEMO-SEED-001")]
    public async Task SeedAsync_OnEmptyDatabase_AddsTwentyFourSyntheticLinkedRecords()
    {
        await using var context = CreateContext();

        var result = await SyntheticDemoDataSeeder.SeedAsync(context, FixedNow);

        Assert.Equal(new SyntheticDemoDataSeedResult(4, 4, 8, 8), result);
        Assert.Equal(24, result.TotalRecordsAdded);
        Assert.Equal(4, await context.Destinations.CountAsync());
        Assert.Equal(4, await context.Activities.CountAsync());
        Assert.Equal(8, await context.Offerings.CountAsync());
        Assert.Equal(8, await context.Schedules.CountAsync());
        var destinationsData = await context.Destinations.ToListAsync();
        Assert.All(destinationsData, item =>
        {
            Assert.StartsWith("synthetic-", item.Slug);
            Assert.InRange(item.Name.Length, 2, 200);
            Assert.InRange(item.Slug.Length, 1, 200);
            Assert.InRange(item.Description!.Length, 1, 2000);
            Assert.InRange(item.Region!.Length, 1, 100);
            Assert.InRange(item.Latitude, -90, 90);
            Assert.InRange(item.Longitude, -180, 180);
            Assert.Equal(PublicationStatus.Published, item.Status);
        });

        var activitiesData = await context.Activities.ToListAsync();
        Assert.All(activitiesData, item =>
        {
            Assert.StartsWith("DEMO_", item.Code);
            Assert.InRange(item.Code.Length, 1, 50);
            Assert.InRange(item.Name.Length, 1, 100);
            Assert.InRange(item.Description!.Length, 1, 1000);
            Assert.InRange(item.Category!.Length, 1, 100);
            Assert.Equal(PublicationStatus.Published, item.Status);
        });

        var destinations = destinationsData.Select(item => item.Id).ToHashSet();
        var activities = activitiesData.Select(item => item.Id).ToHashSet();
        var offerings = await context.Offerings.ToListAsync();
        Assert.All(offerings, item =>
        {
            Assert.Contains(item.DestinationId, destinations);
            Assert.Contains(item.ActivityId, activities);
            Assert.InRange(item.Title.Length, 2, 200);
            Assert.InRange(item.Description!.Length, 1, 2000);
            Assert.InRange(item.Price!.Value, 0m, 1_000_000m);
            Assert.InRange(item.Currency!.Length, 1, 10);
            Assert.InRange(item.DurationMinutes!.Value, 1, 1440);
            Assert.InRange(item.MaxCapacity!.Value, 1, 1000);
            Assert.Equal(PublicationStatus.Published, item.Status);
            Assert.NotNull(item.ExperienceLevels);
            Assert.InRange(item.ExperienceLevels!.Length, 1, 3);
            Assert.Equal(item.ExperienceLevels.Length, item.ExperienceLevels.Distinct(StringComparer.Ordinal).Count());
            Assert.All(item.ExperienceLevels, level => Assert.Contains(level, new[] { "BEGINNER", "INTERMEDIATE", "ADVANCED" }));
        });

        var schedules = await context.Schedules.ToListAsync();
        Assert.All(schedules, item =>
        {
            Assert.Contains(offerings, offering => offering.Id == item.OfferingId);
            Assert.True(item.StartsAt <= FixedNow);
            Assert.True(item.EndsAt >= FixedNow.AddDays(120));
            Assert.True(item.EndsAt > item.StartsAt);
            Assert.Equal("Asia/Colombo", item.TimeZoneId);
            Assert.True(item.IsActive);
        });
    }

    [Fact]
    [Trait("CaseId", "EXP-DEMO-SEED-002")]
    public async Task SeedAsync_WhenRunRepeatedly_IsIdempotentAndPreservesRows()
    {
        await using var context = CreateContext();

        var firstResult = await SyntheticDemoDataSeeder.SeedAsync(context, FixedNow);
        var firstNames = await context.Destinations.OrderBy(item => item.Id).Select(item => item.Name).ToListAsync();
        var secondResult = await SyntheticDemoDataSeeder.SeedAsync(context, FixedNow.AddHours(1));

        Assert.Equal(24, firstResult.TotalRecordsAdded);
        Assert.Equal(0, secondResult.TotalRecordsAdded);
        Assert.Equal(4, await context.Destinations.CountAsync());
        Assert.Equal(4, await context.Activities.CountAsync());
        Assert.Equal(8, await context.Offerings.CountAsync());
        Assert.Equal(8, await context.Schedules.CountAsync());
        Assert.Equal(firstNames, await context.Destinations.OrderBy(item => item.Id).Select(item => item.Name).ToListAsync());
        Assert.All(await context.Destinations.ToListAsync(), item => Assert.Equal(FixedNow, item.CreatedAt));
    }

    [Fact]
    [Trait("CaseId", "EXP-DEMO-SEED-003")]
    public async Task SeedAsync_WhenCatalogueHasOtherRows_PreservesThemAndAddsLinkedDemoData()
    {
        await using var context = CreateContext();
        var existing = new Destination
        {
            Id = Guid.Parse("ec000000-0000-4000-8000-000000000001"),
            Name = "Locally managed destination",
            Slug = "locally-managed-destination",
            Region = "Western Province",
            Latitude = 6.2,
            Longitude = 80.1,
            Status = PublicationStatus.Draft,
            CreatedAt = FixedNow.AddDays(-1),
            UpdatedAt = FixedNow.AddDays(-1)
        };
        context.Destinations.Add(existing);
        await context.SaveChangesAsync();

        var result = await SyntheticDemoDataSeeder.SeedAsync(context, FixedNow);

        var preserved = await context.Destinations.SingleAsync(item => item.Id == existing.Id);
        Assert.Equal("Locally managed destination", preserved.Name);
        Assert.Equal(PublicationStatus.Draft, preserved.Status);
        Assert.Equal(4, result.DestinationsAdded);
        Assert.Equal(24, result.TotalRecordsAdded);
        Assert.Equal(5, await context.Destinations.CountAsync());
    }

    private static ExperienceBiodiversityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ExperienceBiodiversityDbContext>()
            .UseInMemoryDatabase($"SyntheticDemoSeed_{Guid.NewGuid():N}")
            .Options;
        return new ExperienceBiodiversityDbContext(options);
    }
}
