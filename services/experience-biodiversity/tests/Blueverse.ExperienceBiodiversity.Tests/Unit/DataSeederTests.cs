using Blueverse.ExperienceBiodiversity.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Unit;

public sealed class DataSeederTests
{
    [Fact]
    [Trait("CaseId", "EXP-UNIT-SEED-001")]
    public async Task SeedAsync_OnEmptyDatabase_SeedsCanonicalData()
    {
        var options = new DbContextOptionsBuilder<ExperienceBiodiversityDbContext>()
            .UseInMemoryDatabase(databaseName: $"SeedDb_{Guid.NewGuid():N}")
            .Options;

        await using var context = new ExperienceBiodiversityDbContext(options);

        await DataSeeder.SeedAsync(context);

        var destCount = await context.Destinations.CountAsync();
        var actCount = await context.Activities.CountAsync();
        var offCount = await context.Offerings.CountAsync();
        var schCount = await context.Schedules.CountAsync();

        Assert.Equal(5, destCount);
        Assert.Equal(4, actCount);
        Assert.Equal(3, offCount);
        Assert.Equal(3, schCount);
    }

    [Fact]
    [Trait("CaseId", "EXP-UNIT-SEED-002")]
    public async Task SeedAsync_WhenDestinationsAlreadyExist_IsIdempotent()
    {
        var options = new DbContextOptionsBuilder<ExperienceBiodiversityDbContext>()
            .UseInMemoryDatabase(databaseName: $"SeedDb_{Guid.NewGuid():N}")
            .Options;

        await using var context = new ExperienceBiodiversityDbContext(options);

        // First run
        await DataSeeder.SeedAsync(context);
        var initialDestCount = await context.Destinations.CountAsync();

        // Second run
        await DataSeeder.SeedAsync(context);
        var secondDestCount = await context.Destinations.CountAsync();

        Assert.Equal(initialDestCount, secondDestCount);
    }
}
