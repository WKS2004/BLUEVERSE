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

public sealed class CoastalPlannerServiceDomainTests
{
    [Fact]
    [Trait("TestId", "PLANNER-VAL-TITLE-001")]
    public async Task CreateItinerary_throws_ArgumentException_when_title_is_empty_whitespace_or_exceeds_150_chars()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddHours(2);
        var validItem = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);

        var emptyTitleReq = new CreateItineraryRequestDto("", null, startsAt, endsAt, [validItem]);
        var whitespaceTitleReq = new CreateItineraryRequestDto("   ", null, startsAt, endsAt, [validItem]);
        var tooLongTitleReq = new CreateItineraryRequestDto(new string('A', 151), null, startsAt, endsAt, [validItem]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(emptyTitleReq, ownerId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(whitespaceTitleReq, ownerId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(tooLongTitleReq, ownerId));
    }

    [Fact]
    [Trait("TestId", "PLANNER-VAL-DESC-001")]
    public async Task CreateItinerary_throws_ArgumentException_when_description_exceeds_500_chars()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddHours(2);
        var validItem = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);

        var tooLongDescReq = new CreateItineraryRequestDto("Valid Title", new string('D', 501), startsAt, endsAt, [validItem]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(tooLongDescReq, ownerId));
    }

    [Fact]
    [Trait("TestId", "PLANNER-VAL-DATES-001")]
    public async Task CreateItinerary_throws_ArgumentException_when_dates_are_invalid_or_non_utc()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var validItem = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), now.AddDays(1), now.AddDays(1).AddHours(1), 0);

        // Non-UTC start
        var nonUtcStart = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Local);
        var nonUtcReq = new CreateItineraryRequestDto("Title", null, nonUtcStart, now.AddDays(2), [validItem]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(nonUtcReq, ownerId));

        // Inverted dates (end <= start)
        var invertedReq = new CreateItineraryRequestDto("Title", null, now.AddDays(2), now.AddDays(1), [validItem]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(invertedReq, ownerId));

        // Past dates
        var pastReq = new CreateItineraryRequestDto("Title", null, now.AddDays(-2), now.AddDays(-1), [validItem]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(pastReq, ownerId));

        // Dates beyond 30 days
        var farFutureReq = new CreateItineraryRequestDto("Title", null, now.AddDays(31), now.AddDays(32), [validItem]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(farFutureReq, ownerId));
    }

    [Fact]
    [Trait("TestId", "PLANNER-VAL-ITEMS-001")]
    public async Task CreateItinerary_throws_ArgumentException_when_items_contain_invalid_fields()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddHours(4);

        // Null items
        var nullItemsReq = new CreateItineraryRequestDto("Title", null, startsAt, endsAt, null!);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(nullItemsReq, ownerId));

        // Empty GUID destination
        var emptyDestItem = new CreateItineraryItemRequestDto(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Act", 0, startsAt, startsAt.AddHours(1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(
            new CreateItineraryRequestDto("Title", null, startsAt, endsAt, [emptyDestItem]), ownerId));

        // Empty GUID activity
        var emptyActItem = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "Act", 0, startsAt, startsAt.AddHours(1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(
            new CreateItineraryRequestDto("Title", null, startsAt, endsAt, [emptyActItem]), ownerId));

        // Empty title
        var emptyTitleItem = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  ", 0, startsAt, startsAt.AddHours(1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(
            new CreateItineraryRequestDto("Title", null, startsAt, endsAt, [emptyTitleItem]), ownerId));

        // Item scheduled outside itinerary window (starts before itinerary start)
        var outsideItem = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Act", 0, startsAt.AddHours(-1), startsAt.AddHours(1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(
            new CreateItineraryRequestDto("Title", null, startsAt, endsAt, [outsideItem]), ownerId));

        // Item scheduled end before item scheduled start
        var invertedItem = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Act", 0, startsAt.AddHours(2), startsAt.AddHours(1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(
            new CreateItineraryRequestDto("Title", null, startsAt, endsAt, [invertedItem]), ownerId));

        // Negative order index
        var negativeOrderItem = new CreateItineraryItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Act", -1, startsAt, startsAt.AddHours(1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(
            new CreateItineraryRequestDto("Title", null, startsAt, endsAt, [negativeOrderItem]), ownerId));
    }

    [Fact]
    [Trait("TestId", "PLANNER-VAL-ACTOR-001")]
    public async Task CreateItinerary_throws_ArgumentException_when_owner_is_empty_or_request_null()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var startsAt = DateTime.UtcNow.AddDays(1);
        var validItem = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);
        var request = new CreateItineraryRequestDto("Coastal Plan", "Notes", startsAt, startsAt.AddHours(2), [validItem]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItineraryAsync(request, Guid.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateItineraryAsync(null!, Guid.NewGuid()));
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-DOMAIN-001")]
    public async Task CreateItinerary_creates_record_with_version_one_and_maps_all_properties()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddHours(5);
        var destId = Guid.NewGuid();
        var actId = Guid.NewGuid();
        var offId = Guid.NewGuid();
        var item1 = new CreateItineraryItemRequestDto(destId, actId, offId, "Snorkeling", 0, startsAt, startsAt.AddHours(2));
        var item2 = new CreateItineraryItemRequestDto(destId, Guid.NewGuid(), null, "Beach Walk", 1, startsAt.AddHours(2), startsAt.AddHours(4));
        var request = new CreateItineraryRequestDto("Tropical Holiday", "2 activities planned", startsAt, endsAt, [item1, item2]);

        var result = await service.CreateItineraryAsync(request, ownerId);

        Assert.NotEqual(Guid.Empty, result.ItineraryId);
        Assert.Equal(ownerId, result.OwnerUserId);
        Assert.Equal("Tropical Holiday", result.Title);
        Assert.Equal("2 activities planned", result.Description);
        Assert.Equal(startsAt, result.StartsAt);
        Assert.Equal(endsAt, result.EndsAt);
        Assert.Equal(1, result.ConcurrencyVersion);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal("Snorkeling", result.Items[0].Title);
        Assert.Equal(0, result.Items[0].OrderIndex);
        Assert.Equal("UNKNOWN", result.Items[0].LastSuitabilityStatus);
        Assert.Equal("UNKNOWN", result.Items[0].LastAvailabilityStatus);
        Assert.Equal("UNKNOWN", result.Items[0].LastOperationalStatus);

        Assert.Equal("Beach Walk", result.Items[1].Title);
        Assert.Equal(1, result.Items[1].OrderIndex);

        var persisted = await db.Itineraries.Include(i => i.Items).SingleAsync(i => i.ItineraryId == result.ItineraryId);
        Assert.Equal(1, persisted.ConcurrencyVersion);
        Assert.Equal(2, persisted.Items.Count);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-LIST-001")]
    public async Task ListItineraries_paginates_and_scopes_to_owner()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);

        for (var i = 0; i < 5; i++)
        {
            var item = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);
            await service.CreateItineraryAsync(new CreateItineraryRequestDto($"Trip {i}", null, startsAt, startsAt.AddHours(2), [item]), ownerId);
        }

        var otherItem = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);
        await service.CreateItineraryAsync(new CreateItineraryRequestDto("Other's Trip", null, startsAt, startsAt.AddHours(2), [otherItem]), otherOwnerId);

        var page1 = await service.ListItinerariesAsync(ownerId, page: 1, pageSize: 3);
        var page2 = await service.ListItinerariesAsync(ownerId, page: 2, pageSize: 3);

        Assert.Equal(3, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.DoesNotContain(page1, i => i.OwnerUserId != ownerId);
        Assert.DoesNotContain(page2, i => i.OwnerUserId != ownerId);

        var otherList = await service.ListItinerariesAsync(otherOwnerId, page: 1, pageSize: 10);
        Assert.Single(otherList);
        Assert.Equal("Other's Trip", otherList[0].Title);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-GET-001")]
    public async Task GetItinerary_returns_dto_with_ordered_items_when_found_and_null_for_other_owner()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);

        var item0 = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);
        var item1 = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt.AddHours(1), startsAt.AddHours(2), 1);
        var created = await service.CreateItineraryAsync(new CreateItineraryRequestDto("Ordered Trip", null, startsAt, startsAt.AddHours(3), [item1, item0]), ownerId);

        var found = await service.GetItineraryAsync(created.ItineraryId, ownerId);
        Assert.NotNull(found);
        Assert.Equal(created.ItineraryId, found.ItineraryId);
        Assert.Equal(0, found.Items[0].OrderIndex);
        Assert.Equal(1, found.Items[1].OrderIndex);

        var notFoundForOther = await service.GetItineraryAsync(created.ItineraryId, Guid.NewGuid());
        Assert.Null(notFoundForOther);

        var notFoundForRandom = await service.GetItineraryAsync(Guid.NewGuid(), ownerId);
        Assert.Null(notFoundForRandom);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-UPDATE-001")]
    public async Task UpdateItinerary_updates_fields_and_increments_concurrency_version()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddHours(3);

        var item = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);
        var created = await service.CreateItineraryAsync(new CreateItineraryRequestDto("Initial Trip", "Desc", startsAt, endsAt, [item]), ownerId);
        Assert.Equal(1, created.ConcurrencyVersion);

        var newItem = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt.AddHours(1), startsAt.AddHours(2), 0);
        var updateReq = new UpdateItineraryRequestDto("Updated Trip", "New Desc", startsAt, endsAt.AddHours(1), 1, [newItem]);

        var updated = await service.UpdateItineraryAsync(created.ItineraryId, updateReq, ownerId);
        Assert.NotNull(updated);
        Assert.Equal("Updated Trip", updated.Title);
        Assert.Equal("New Desc", updated.Description);
        Assert.Equal(endsAt.AddHours(1), updated.EndsAt);
        Assert.Equal(2, updated.ConcurrencyVersion);
        Assert.Single(updated.Items);
        Assert.Equal(newItem.ScheduledStart, updated.Items[0].ScheduledStart);

        // Nonexistent returns null
        var nonExistent = await service.UpdateItineraryAsync(Guid.NewGuid(), updateReq with { ConcurrencyVersion = 1 }, ownerId);
        Assert.Null(nonExistent);

        // Wrong owner returns null
        var wrongOwner = await service.UpdateItineraryAsync(created.ItineraryId, updateReq with { ConcurrencyVersion = 2 }, Guid.NewGuid());
        Assert.Null(wrongOwner);
    }

    [Fact]
    [Trait("TestId", "PLANNER-ITIN-DELETE-001")]
    public async Task DeleteItinerary_removes_itinerary_and_cascade_deletes_items()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var ownerId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);

        var item = CreateItineraryItem(Guid.NewGuid(), Guid.NewGuid(), startsAt, startsAt.AddHours(1), 0);
        var created = await service.CreateItineraryAsync(new CreateItineraryRequestDto("To Delete", null, startsAt, startsAt.AddHours(2), [item]), ownerId);

        var wrongOwnerDelete = await service.DeleteItineraryAsync(created.ItineraryId, Guid.NewGuid());
        Assert.False(wrongOwnerDelete);
        Assert.Equal(1, await db.Itineraries.CountAsync());

        var successDelete = await service.DeleteItineraryAsync(created.ItineraryId, ownerId);
        Assert.True(successDelete);
        Assert.Equal(0, await db.Itineraries.CountAsync());
        Assert.Equal(0, await db.ItineraryItems.CountAsync());
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-VAL-001")]
    public async Task GenerateRecommendations_validates_inputs_strictly()
    {
        await using var db = CreateDatabase();
        var service = CreateService(db, new TestPeerServicesClient());
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Empty destination
        var emptyDest = new RecommendationRequestDto(Guid.Empty, now.AddDays(1), now.AddDays(1).AddHours(4), 2, null, "INTERMEDIATE", false);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateRecommendationsAsync(emptyDest, actorId));

        // Past dates
        var pastDates = new RecommendationRequestDto(Guid.NewGuid(), now.AddDays(-1), now.AddHours(2), 2, null, "INTERMEDIATE", false);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateRecommendationsAsync(pastDates, actorId));

        // Far future dates (> 30 days)
        var farFuture = new RecommendationRequestDto(Guid.NewGuid(), now.AddDays(31), now.AddDays(31).AddHours(4), 2, null, "INTERMEDIATE", false);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateRecommendationsAsync(farFuture, actorId));

        // Empty GUID in preferred activities
        var emptyPrefGuid = new RecommendationRequestDto(Guid.NewGuid(), now.AddDays(1), now.AddDays(1).AddHours(4), 2, [Guid.Empty], "INTERMEDIATE", false);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateRecommendationsAsync(emptyPrefGuid, actorId));
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-FILTER-001")]
    public async Task GenerateRecommendations_filters_to_preferred_activities_when_specified()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var preferredActivityId = Guid.NewGuid();
        var otherActivityId = Guid.NewGuid();
        var peer = new TestPeerServicesClient
        {
            CatalogueResult =
            (
                [
                    Offering(destinationId, preferredActivityId, "AVAILABLE", "PUBLISHED"),
                    Offering(destinationId, otherActivityId, "AVAILABLE", "PUBLISHED")
                ],
                true,
                null
            ),
            OperationsResult = (new PeerOperationStatusResponse(destinationId, "OPEN", []), true, null),
            MarineResultForActivity = _ => (Suitable(destinationId, preferredActivityId), true, null)
        };
        var service = CreateService(db, peer);
        var startsAt = DateTime.UtcNow.AddDays(1);
        var request = new RecommendationRequestDto(destinationId, startsAt, startsAt.AddHours(4), 2, [preferredActivityId], "INTERMEDIATE", false);

        var result = await service.GenerateRecommendationsAsync(request, Guid.NewGuid());

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(preferredActivityId, candidate.ActivityId);
        Assert.Contains(candidate.Reasons, r => r.Contains("Matches a preferred activity", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-FIT-001")]
    public async Task GenerateRecommendations_assigns_fit_score_70_for_caution_conditions()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var peer = new TestPeerServicesClient
        {
            CatalogueResult = ([Offering(destinationId, activityId, "AVAILABLE", "PUBLISHED")], true, null),
            OperationsResult = (new PeerOperationStatusResponse(destinationId, "CAUTION", []), true, null),
            MarineResult = (new PeerSuitabilityResponse(destinationId, activityId, "CAUTION", DateTime.UtcNow, Guid.NewGuid(), "High wind caution"), true, null)
        };
        var service = CreateService(db, peer);
        var startsAt = DateTime.UtcNow.AddDays(1);
        var request = new RecommendationRequestDto(destinationId, startsAt, startsAt.AddHours(4), 2, null, "INTERMEDIATE", false);

        var result = await service.GenerateRecommendationsAsync(request, Guid.NewGuid());

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("CAUTION", candidate.Suitability.Status);
        Assert.Equal("CAUTION", candidate.OperationalStatus);
        Assert.Equal(0.70, candidate.FitScore);
        Assert.Contains(candidate.Reasons, r => r.Contains("caution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-EXCLUDE-001")]
    public async Task GenerateRecommendations_excludes_suspended_or_cancelled_operations()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var peer = new TestPeerServicesClient
        {
            CatalogueResult = ([Offering(destinationId, activityId, "AVAILABLE", "PUBLISHED")], true, null),
            OperationsResult = (new PeerOperationStatusResponse(destinationId, "TEMPORARILY_SUSPENDED", []), true, null),
            MarineResult = (Suitable(destinationId, activityId), true, null)
        };
        var service = CreateService(db, peer);
        var startsAt = DateTime.UtcNow.AddDays(1);
        var request = new RecommendationRequestDto(destinationId, startsAt, startsAt.AddHours(4), 2, null, "INTERMEDIATE", false);

        var result = await service.GenerateRecommendationsAsync(request, Guid.NewGuid());

        Assert.Empty(result.Candidates);
        Assert.Contains(result.UncertaintyNotes, note => note.Contains("TEMPORARILY_SUSPENDED", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestId", "PLANNER-REC-BIO-001")]
    public async Task GenerateRecommendations_attaches_biodiversity_context_when_enabled()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();
        var peer = new TestPeerServicesClient
        {
            CatalogueResult = ([Offering(destinationId, activityId, "AVAILABLE", "PUBLISHED")], true, null),
            OperationsResult = (new PeerOperationStatusResponse(destinationId, "OPEN", []), true, null),
            MarineResult = (Suitable(destinationId, activityId), true, null),
            BiodiversityResult =
            (
                new PeerBiodiversityInferenceResponse(destinationId, activityId, "AVAILABLE",
                    [new PeerSpeciesInference(speciesId, "Dugong dugon", "Dugong", 0.91, "HIGH")],
                    "model-v2", DateTime.UtcNow, "Contextual"),
                true,
                null
            )
        };
        var service = CreateService(db, peer);
        var startsAt = DateTime.UtcNow.AddDays(1);
        var request = new RecommendationRequestDto(destinationId, startsAt, startsAt.AddHours(4), 2, null, "INTERMEDIATE", IncludeBiodiversityContext: true);

        var result = await service.GenerateRecommendationsAsync(request, Guid.NewGuid());

        var candidate = Assert.Single(result.Candidates);
        Assert.NotNull(candidate.BiodiversityContext);
        Assert.Equal("Dugong dugon (Dugong)", candidate.BiodiversityContext.SpeciesName);
        Assert.Equal(0.91, candidate.BiodiversityContext.Probability);
        Assert.Equal("HIGH", candidate.BiodiversityContext.Uncertainty);
    }

    [Fact]
    [Trait("TestId", "PLANNER-REEVAL-KEEP-001")]
    public async Task ReEvaluateItinerary_returns_KEEP_when_all_conditions_are_suitable_and_available()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var offeringId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var ownerId = Guid.NewGuid();

        var peer = new TestPeerServicesClient
        {
            CatalogueResult = ([new PeerCatalogueItem(destinationId, activityId, offeringId, "Snorkel", "AVAILABLE", "PUBLISHED")], true, null),
            OperationsResult = (new PeerOperationStatusResponse(destinationId, "OPEN", []), true, null),
            MarineResult = (new PeerSuitabilityResponse(destinationId, activityId, "SUITABLE", startsAt, Guid.NewGuid(), "Clear waters"), true, null)
        };
        var service = CreateService(db, peer);

        var item = new CreateItineraryItemRequestDto(destinationId, activityId, offeringId, "Snorkel", 0, startsAt, startsAt.AddHours(2));
        var itinerary = await service.CreateItineraryAsync(new CreateItineraryRequestDto("Trip", null, startsAt, startsAt.AddHours(3), [item]), ownerId);

        var reeval = await service.ReEvaluateItineraryAsync(itinerary.ItineraryId, ownerId, new ItineraryReEvaluationRequestDto());

        Assert.NotNull(reeval);
        Assert.False(reeval.HasChanges);
        Assert.Contains("All planned items remain suitable", reeval.Summary, StringComparison.Ordinal);
        var resultItem = Assert.Single(reeval.Items);
        Assert.Equal("KEEP", resultItem.SuggestedAction);
        Assert.Equal("AVAILABLE", resultItem.CurrentAvailability);
        Assert.Equal("SUITABLE", resultItem.CurrentSuitability);
        Assert.Equal("OPEN", resultItem.CurrentOperationalStatus);
    }

    [Fact]
    [Trait("TestId", "PLANNER-REEVAL-ACTION-001")]
    public async Task ReEvaluateItinerary_marks_CANCEL_OR_RESCHEDULE_when_unsuitable_or_unpublished()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var offeringId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var ownerId = Guid.NewGuid();

        var peer = new TestPeerServicesClient
        {
            CatalogueResult = ([new PeerCatalogueItem(destinationId, activityId, offeringId, "Snorkel", "AVAILABLE", "DRAFT")], true, null),
            OperationsResult = (new PeerOperationStatusResponse(destinationId, "OPEN", []), true, null),
            MarineResult = (new PeerSuitabilityResponse(destinationId, activityId, "UNSUITABLE", startsAt, null, "Storm warning"), true, null)
        };
        var service = CreateService(db, peer);

        var item = new CreateItineraryItemRequestDto(destinationId, activityId, offeringId, "Snorkel", 0, startsAt, startsAt.AddHours(2));
        var itinerary = await service.CreateItineraryAsync(new CreateItineraryRequestDto("Trip", null, startsAt, startsAt.AddHours(3), [item]), ownerId);

        var reeval = await service.ReEvaluateItineraryAsync(itinerary.ItineraryId, ownerId, new ItineraryReEvaluationRequestDto());

        Assert.NotNull(reeval);
        Assert.True(reeval.HasChanges);
        var resultItem = Assert.Single(reeval.Items);
        Assert.Equal("CANCEL_OR_RESCHEDULE", resultItem.SuggestedAction);
        Assert.Equal("NOT_PUBLISHED", resultItem.CurrentAvailability);
        Assert.Equal("UNSUITABLE", resultItem.CurrentSuitability);
    }

    [Fact]
    [Trait("TestId", "PLANNER-REEVAL-CAUTION-001")]
    public async Task ReEvaluateItinerary_marks_REVIEW_CONDITIONS_when_caution_present()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var ownerId = Guid.NewGuid();

        var peer = new TestPeerServicesClient
        {
            CatalogueResult = ([new PeerCatalogueItem(destinationId, activityId, Guid.NewGuid(), "Snorkel", "AVAILABLE", "PUBLISHED")], true, null),
            OperationsResult = (new PeerOperationStatusResponse(destinationId, "CAUTION", []), true, null),
            MarineResult = (new PeerSuitabilityResponse(destinationId, activityId, "SUITABLE", startsAt, Guid.NewGuid(), "Choppy water"), true, null)
        };
        var service = CreateService(db, peer);

        var item = CreateItineraryItem(destinationId, activityId, startsAt, startsAt.AddHours(2), 0);
        var itinerary = await service.CreateItineraryAsync(new CreateItineraryRequestDto("Trip", null, startsAt, startsAt.AddHours(3), [item]), ownerId);

        var reeval = await service.ReEvaluateItineraryAsync(itinerary.ItineraryId, ownerId, new ItineraryReEvaluationRequestDto());

        Assert.NotNull(reeval);
        Assert.True(reeval.HasChanges);
        var resultItem = Assert.Single(reeval.Items);
        Assert.Equal("REVIEW_CONDITIONS", resultItem.SuggestedAction);
        Assert.Equal("CAUTION", resultItem.CurrentOperationalStatus);
    }

    [Fact]
    [Trait("TestId", "PLANNER-BIO-EDGE-001")]
    public async Task GetBiodiversityPredictions_rejects_malformed_peer_species_and_stale_timestamps()
    {
        await using var db = CreateDatabase();
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        // Stale timestamp (> 6 hours old)
        var peer = new TestPeerServicesClient
        {
            BiodiversityResult =
            (
                new PeerBiodiversityInferenceResponse(destinationId, activityId, "AVAILABLE",
                    [new PeerSpeciesInference(Guid.NewGuid(), "Species", "Common", 0.5, "LOW")],
                    "v1", DateTime.UtcNow.AddHours(-7), null),
                true,
                null
            )
        };
        var service = CreateService(db, peer);

        var staleResult = await service.GetBiodiversityPredictionsAsync(destinationId, activityId);
        Assert.Equal("UNAVAILABLE", staleResult.Status);
        Assert.Empty(staleResult.PredictedSpecies);

        // Species with empty GUID
        peer.BiodiversityResult =
        (
            new PeerBiodiversityInferenceResponse(destinationId, activityId, "AVAILABLE",
                [new PeerSpeciesInference(Guid.Empty, "Species", "Common", 0.5, "LOW")],
                "v1", DateTime.UtcNow, null),
            true,
            null
        );
        var emptySpeciesGuidResult = await service.GetBiodiversityPredictionsAsync(destinationId, activityId);
        Assert.Equal("UNAVAILABLE", emptySpeciesGuidResult.Status);

        // Model version exceeding 64 chars
        peer.BiodiversityResult =
        (
            new PeerBiodiversityInferenceResponse(destinationId, activityId, "AVAILABLE",
                [new PeerSpeciesInference(Guid.NewGuid(), "Species", "Common", 0.5, "LOW")],
                new string('M', 65), DateTime.UtcNow, null),
            true,
            null
        );
        var longModelResult = await service.GetBiodiversityPredictionsAsync(destinationId, activityId);
        Assert.Equal("UNAVAILABLE", longModelResult.Status);
    }

    private static CoastalPlannerDbContext CreateDatabase() => new(
        new DbContextOptionsBuilder<CoastalPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static CoastalPlannerService CreateService(CoastalPlannerDbContext db, IPeerServicesClient peer) =>
        new(db, peer, NullLogger<CoastalPlannerService>.Instance);

    private static PeerCatalogueItem Offering(Guid destinationId, Guid activityId, string availability, string publication) =>
        new(destinationId, activityId, Guid.NewGuid(), $"Activity {activityId:N}", availability, publication);

    private static PeerSuitabilityResponse Suitable(Guid destinationId, Guid activityId) =>
        new(destinationId, activityId, "SUITABLE", DateTime.UtcNow, Guid.NewGuid(), null);

    private static CreateItineraryItemRequestDto CreateItineraryItem(
        Guid destinationId, Guid activityId, DateTime startsAt, DateTime endsAt, int order) =>
        new(destinationId, activityId, Guid.NewGuid(), "Coastal activity", order, startsAt, endsAt);
}
