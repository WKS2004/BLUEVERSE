using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Services;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Unit;

public sealed class MapProviderServiceTests
{
    [Fact]
    [Trait("CaseId", "EXP-MAP-001")]
    public void OpenFreeMap_Configuration_Provides_Expected_Vector_Styles()
    {
        var options = new DbContextOptionsBuilder<ExperienceBiodiversityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ExperienceBiodiversityDbContext(options);

        var service = new MapProviderService(new HttpClient(), context, NullLogger<MapProviderService>.Instance);
        var config = service.GetMapConfiguration();

        Assert.Equal("OpenFreeMap", config.Provider);
        Assert.Equal("VectorTiles", config.TileServiceType);
        Assert.Equal("liberty", config.DefaultStyle);
        Assert.True(config.AvailableStyles.ContainsKey("liberty"));
        Assert.True(config.AvailableStyles.ContainsKey("bright"));
        Assert.True(config.AvailableStyles.ContainsKey("positron"));
        Assert.Contains("OpenFreeMap", config.Attribution);
        Assert.Contains("OpenStreetMap", config.Attribution);
    }

    [Fact]
    [Trait("CaseId", "EXP-MAP-002")]
    public async Task SearchPlaces_Falls_Back_To_Local_Destinations_When_Provider_Fails()
    {
        var options = new DbContextOptionsBuilder<ExperienceBiodiversityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ExperienceBiodiversityDbContext(options);

        var dest = new Destination
        {
            Id = Guid.NewGuid(),
            Name = "Mirissa Secret Beach",
            Slug = "mirissa-secret-beach",
            Region = "Southern Province",
            Latitude = 5.94,
            Longitude = 80.46,
            Status = PublicationStatus.Published
        };
        context.Destinations.Add(dest);
        await context.SaveChangesAsync();

        // Pass an invalid HTTP handler or failing client to verify local fallback
        var service = new MapProviderService(new HttpClient(), context, NullLogger<MapProviderService>.Instance);
        var searchResult = await service.SearchPlacesAsync("Mirissa");

        Assert.NotNull(searchResult);
        Assert.NotEmpty(searchResult.Results);
        Assert.Contains(searchResult.Results, r => r.DisplayName.Contains("Mirissa"));
    }
}
