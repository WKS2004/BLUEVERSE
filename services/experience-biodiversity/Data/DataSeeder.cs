using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Models;

namespace Blueverse.ExperienceBiodiversity.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(ExperienceBiodiversityDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Destinations.AnyAsync(cancellationToken))
        {
            return;
        }

        // Destinations
        var mirissa = new Destination
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
            Name = "Mirissa Coastal Haven",
            Slug = "mirissa-coastal-haven",
            Description = "Iconic southern bay known for gentle swells, whale watching excursions, and marine biodiversity.",
            Region = "Southern Province",
            Latitude = 5.9482,
            Longitude = 80.4716,
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var arugamBay = new Destination
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
            Name = "Arugam Bay Surf Point",
            Slug = "arugam-bay-surf-point",
            Description = "World-renowned right hand point break with vibrant coastal reef systems on the eastern shore.",
            Region = "Eastern Province",
            Latitude = 6.8415,
            Longitude = 81.8354,
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var trincomalee = new Destination
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
            Name = "Nilaveli & Pigeon Island",
            Slug = "nilaveli-pigeon-island",
            Description = "Pristine white sand beaches and protected marine national park featuring coral reefs and blacktip reef sharks.",
            Region = "Eastern Province",
            Latitude = 8.6833,
            Longitude = 81.1833,
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var hikkaduwa = new Destination
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
            Name = "Hikkaduwa Marine Sanctuary",
            Slug = "hikkaduwa-marine-sanctuary",
            Description = "Famed marine sanctuary with shallow coral reefs, sea turtle feeding zones, and coastal surfing.",
            Region = "Southern Province",
            Latitude = 6.1406,
            Longitude = 80.1005,
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var kalpitiya = new Destination
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
            Name = "Kalpitiya Lagoon & Bar Reef",
            Slug = "kalpitiya-lagoon-bar-reef",
            Description = "Peninsula famous for kite-surfing lagoons, spinner dolphin pods, and Sri Lanka's largest coral reef.",
            Region = "North Western Province",
            Latitude = 8.2307,
            Longitude = 79.7656,
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await context.Destinations.AddRangeAsync([mirissa, arugamBay, trincomalee, hikkaduwa, kalpitiya], cancellationToken);

        // Activities
        var whaleWatching = new Activity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222201"),
            Code = "WHALE_WATCHING",
            Name = "Whale & Dolphin Watching",
            Description = "Ethical boat tours to observe blue whales, sperm whales, and spinner dolphins in their natural migratory corridors.",
            Category = "Wildlife",
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var surfing = new Activity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222202"),
            Code = "SURFING",
            Name = "Coastal Surfing & Coaching",
            Description = "Guided surf sessions and clinics catering to beginner through advanced wave conditions.",
            Category = "WaterSports",
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var snorkeling = new Activity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222203"),
            Code = "SNORKELING",
            Name = "Reef Snorkeling Expedition",
            Description = "Guided snorkeling along shallow coral formations, observing sea turtles and reef fish.",
            Category = "WaterSports",
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var scubaDiving = new Activity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222204"),
            Code = "DIVING",
            Name = "Scuba Diving & Wreck Exploration",
            Description = "Open water dives exploring marine reefs, shipwrecks, and underwater topography with certified divemasters.",
            Category = "WaterSports",
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await context.Activities.AddRangeAsync([whaleWatching, surfing, snorkeling, scubaDiving], cancellationToken);

        // Offerings
        var mirissaWhales = new Offering
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333301"),
            DestinationId = mirissa.Id,
            ActivityId = whaleWatching.Id,
            Title = "Mirissa Morning Blue Whale Expedition",
            Description = "Early morning catamaran tour leaving from Mirissa Fishery Harbour into the deep southern trench.",
            Price = 65.00m,
            Currency = "USD",
            DurationMinutes = 240,
            MaxCapacity = 30,
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var arugamSurfing = new Offering
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333302"),
            DestinationId = arugamBay.Id,
            ActivityId = surfing.Id,
            Title = "Main Point Sunrise Surf Coaching",
            Description = "Two-hour coaching session covering wave entry, board positioning, and rip current awareness.",
            Price = 40.00m,
            Currency = "USD",
            DurationMinutes = 120,
            MaxCapacity = 8,
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var nilaveliSnorkeling = new Offering
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333303"),
            DestinationId = trincomalee.Id,
            ActivityId = snorkeling.Id,
            Title = "Pigeon Island Coral Snorkel Tour",
            Description = "Boat transfer from Nilaveli beach to Pigeon Island National Park with marine guide and gear.",
            Price = 45.00m,
            Currency = "USD",
            DurationMinutes = 180,
            MaxCapacity = 15,
            Status = PublicationStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await context.Offerings.AddRangeAsync([mirissaWhales, arugamSurfing, nilaveliSnorkeling], cancellationToken);

        // Schedules (Spanning across 2026 for continuous availability)
        var schedules = new List<Schedule>
        {
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444401"),
                OfferingId = mirissaWhales.Id,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-30),
                EndsAt = DateTimeOffset.UtcNow.AddDays(180),
                TimeZoneId = "Asia/Colombo",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444402"),
                OfferingId = arugamSurfing.Id,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-30),
                EndsAt = DateTimeOffset.UtcNow.AddDays(180),
                TimeZoneId = "Asia/Colombo",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444403"),
                OfferingId = nilaveliSnorkeling.Id,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-30),
                EndsAt = DateTimeOffset.UtcNow.AddDays(180),
                TimeZoneId = "Asia/Colombo",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        await context.Schedules.AddRangeAsync(schedules, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
