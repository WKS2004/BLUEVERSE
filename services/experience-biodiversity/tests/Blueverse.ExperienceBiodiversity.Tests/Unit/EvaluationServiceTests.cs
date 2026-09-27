using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Services;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Unit;

public sealed class EvaluationServiceTests
{
    private static ExperienceBiodiversityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ExperienceBiodiversityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ExperienceBiodiversityDbContext(options);
    }

    private sealed class MockOperationalStatusConsumer : IOperationalStatusConsumerService
    {
        public bool HasRestriction { get; set; }
        public string SourceStatus { get; set; } = "LOCAL_DEFAULT";
        public string? Reason { get; set; }

        public Task<OperationalRestrictionContextDto> GetOperationalStatusAsync(Guid destinationId, Guid? offeringId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OperationalRestrictionContextDto(
                HasRestriction: HasRestriction,
                RestrictionType: HasRestriction ? "WeatherClosure" : null,
                Severity: HasRestriction ? "High" : null,
                Reason: Reason,
                EffectiveUntil: null,
                SourceStatus: SourceStatus));
        }

        public Task<OperationalAdvisoriesResponseDto> GetOperationalAdvisoriesAsync(Guid destinationId, string destinationName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OperationalAdvisoriesResponseDto(
                DestinationId: destinationId,
                DestinationName: destinationName,
                Responded: true,
                TargetEndpoint: "http://mock-operations/advisories",
                AttemptsCount: 1,
                LatencyMs: 5,
                RemoteStatus: "RESPONDED",
                Advisories: Array.Empty<ActiveAdvisoryItemDto>(),
                FallbackUsed: false,
                Message: "Mock advisories"));
        }
    }

    [Fact]
    [Trait("CaseId", "EXP-EVAL-001")]
    public async Task Destination_Draft_To_Published_Is_Allowed()
    {
        using var context = CreateContext();
        var dest = new Destination
        {
            Id = Guid.NewGuid(),
            Name = "Arugam Bay",
            Slug = "arugam-bay",
            Latitude = 6.84,
            Longitude = 81.83,
            Status = PublicationStatus.Draft
        };
        context.Destinations.Add(dest);
        await context.SaveChangesAsync();

        var service = new EvaluationService(context, new MockOperationalStatusConsumer(), NullLogger<EvaluationService>.Instance);
        var result = await service.EvaluateDestinationPublicationAsync(dest.Id, PublicationStatus.Published);

        Assert.True(result.CanTransition);
        Assert.Equal(PublicationStatus.Published, result.RequestedStatus);
    }

    [Fact]
    [Trait("CaseId", "EXP-EVAL-002")]
    public async Task Offering_Publication_Blocked_When_Parent_Destination_Is_Draft()
    {
        using var context = CreateContext();
        var dest = new Destination
        {
            Id = Guid.NewGuid(),
            Name = "Unawatuna",
            Slug = "unawatuna",
            Latitude = 6.01,
            Longitude = 80.25,
            Status = PublicationStatus.Draft // Draft!
        };
        var act = new Activity
        {
            Id = Guid.NewGuid(),
            Code = "DIVING",
            Name = "Scuba Diving",
            Status = PublicationStatus.Published
        };
        var offering = new Offering
        {
            Id = Guid.NewGuid(),
            DestinationId = dest.Id,
            ActivityId = act.Id,
            Title = "Coral Reef Dive",
            Status = PublicationStatus.Draft
        };
        context.Destinations.Add(dest);
        context.Activities.Add(act);
        context.Offerings.Add(offering);
        await context.SaveChangesAsync();

        var service = new EvaluationService(context, new MockOperationalStatusConsumer(), NullLogger<EvaluationService>.Instance);
        var result = await service.EvaluateOfferingPublicationAsync(offering.Id, PublicationStatus.Published);

        Assert.False(result.CanTransition);
        Assert.Contains(result.Reasons, r => r.Contains("must be PUBLISHED"));
    }

    [Fact]
    [Trait("CaseId", "EXP-EVAL-003")]
    public async Task Archived_State_Is_Terminal_And_Cannot_Transition()
    {
        using var context = CreateContext();
        var dest = new Destination
        {
            Id = Guid.NewGuid(),
            Name = "Old Harbor",
            Slug = "old-harbor",
            Status = PublicationStatus.Archived
        };
        context.Destinations.Add(dest);
        await context.SaveChangesAsync();

        var service = new EvaluationService(context, new MockOperationalStatusConsumer(), NullLogger<EvaluationService>.Instance);
        var result = await service.EvaluateDestinationPublicationAsync(dest.Id, PublicationStatus.Published);

        Assert.False(result.CanTransition);
        Assert.Contains(result.Reasons, r => r.Contains("Archived items are terminal"));
    }

    [Fact]
    [Trait("CaseId", "EXP-EVAL-004")]
    public async Task Availability_Evaluation_Returns_Available_When_All_Criteria_Pass()
    {
        using var context = CreateContext();
        var dest = new Destination
        {
            Id = Guid.NewGuid(),
            Name = "Mirissa",
            Slug = "mirissa",
            Status = PublicationStatus.Published
        };
        var act = new Activity
        {
            Id = Guid.NewGuid(),
            Code = "WHALES",
            Name = "Whale Watching",
            Status = PublicationStatus.Published
        };
        var offering = new Offering
        {
            Id = Guid.NewGuid(),
            DestinationId = dest.Id,
            ActivityId = act.Id,
            Title = "Morning Whale Tour",
            Status = PublicationStatus.Published
        };
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            OfferingId = offering.Id,
            StartsAt = DateTimeOffset.UtcNow.AddHours(-1),
            EndsAt = DateTimeOffset.UtcNow.AddHours(5),
            IsActive = true
        };

        context.Destinations.Add(dest);
        context.Activities.Add(act);
        context.Offerings.Add(offering);
        context.Schedules.Add(schedule);
        await context.SaveChangesAsync();

        var opsConsumer = new MockOperationalStatusConsumer { HasRestriction = false };
        var service = new EvaluationService(context, opsConsumer, NullLogger<EvaluationService>.Instance);

        var request = new AvailabilityEvaluationRequest(
            offering.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(2));

        var response = await service.EvaluateAvailabilityAsync(request);

        Assert.Equal(AvailabilityStatus.Available, response.Status);
        Assert.Empty(response.ReasonCodes);
    }

    [Fact]
    [Trait("CaseId", "EXP-EVAL-005")]
    public async Task Availability_Evaluation_Returns_Unavailable_When_Offering_Is_Draft()
    {
        using var context = CreateContext();
        var dest = new Destination { Id = Guid.NewGuid(), Name = "Mirissa", Status = PublicationStatus.Published };
        var act = new Activity { Id = Guid.NewGuid(), Code = "WHALES", Name = "Whales", Status = PublicationStatus.Published };
        var offering = new Offering
        {
            Id = Guid.NewGuid(),
            DestinationId = dest.Id,
            ActivityId = act.Id,
            Title = "Draft Whale Tour",
            Status = PublicationStatus.Draft // Draft!
        };
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            OfferingId = offering.Id,
            StartsAt = DateTimeOffset.UtcNow.AddHours(-1),
            EndsAt = DateTimeOffset.UtcNow.AddHours(5),
            IsActive = true
        };

        context.Destinations.Add(dest);
        context.Activities.Add(act);
        context.Offerings.Add(offering);
        context.Schedules.Add(schedule);
        await context.SaveChangesAsync();

        var service = new EvaluationService(context, new MockOperationalStatusConsumer(), NullLogger<EvaluationService>.Instance);

        var request = new AvailabilityEvaluationRequest(
            offering.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(2));

        var response = await service.EvaluateAvailabilityAsync(request);

        Assert.Equal(AvailabilityStatus.Unavailable, response.Status);
        Assert.Contains(response.ReasonCodes, r => r.Contains("OFFERING_NOT_PUBLISHED"));
    }

    [Fact]
    [Trait("CaseId", "EXP-EVAL-006")]
    public async Task Availability_Evaluation_Returns_Unavailable_When_No_Schedule_Covers_Interval()
    {
        using var context = CreateContext();
        var dest = new Destination { Id = Guid.NewGuid(), Name = "Mirissa", Status = PublicationStatus.Published };
        var act = new Activity { Id = Guid.NewGuid(), Code = "WHALES", Name = "Whales", Status = PublicationStatus.Published };
        var offering = new Offering
        {
            Id = Guid.NewGuid(),
            DestinationId = dest.Id,
            ActivityId = act.Id,
            Title = "Morning Tour",
            Status = PublicationStatus.Published
        };
        // Schedule ended yesterday!
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            OfferingId = offering.Id,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-2),
            EndsAt = DateTimeOffset.UtcNow.AddDays(-1),
            IsActive = true
        };

        context.Destinations.Add(dest);
        context.Activities.Add(act);
        context.Offerings.Add(offering);
        context.Schedules.Add(schedule);
        await context.SaveChangesAsync();

        var service = new EvaluationService(context, new MockOperationalStatusConsumer(), NullLogger<EvaluationService>.Instance);

        var request = new AvailabilityEvaluationRequest(
            offering.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(2));

        var response = await service.EvaluateAvailabilityAsync(request);

        Assert.Equal(AvailabilityStatus.Unavailable, response.Status);
        Assert.Contains(response.ReasonCodes, r => r.Contains("NO_SCHEDULE_COVERAGE"));
    }

    [Fact]
    [Trait("CaseId", "EXP-EVAL-007")]
    public async Task Availability_Evaluation_Returns_Unavailable_When_Operational_Restriction_Active()
    {
        using var context = CreateContext();
        var dest = new Destination { Id = Guid.NewGuid(), Name = "Mirissa", Status = PublicationStatus.Published };
        var act = new Activity { Id = Guid.NewGuid(), Code = "WHALES", Name = "Whales", Status = PublicationStatus.Published };
        var offering = new Offering
        {
            Id = Guid.NewGuid(),
            DestinationId = dest.Id,
            ActivityId = act.Id,
            Title = "Morning Tour",
            Status = PublicationStatus.Published
        };
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            OfferingId = offering.Id,
            StartsAt = DateTimeOffset.UtcNow.AddHours(-1),
            EndsAt = DateTimeOffset.UtcNow.AddHours(5),
            IsActive = true
        };

        context.Destinations.Add(dest);
        context.Activities.Add(act);
        context.Offerings.Add(offering);
        context.Schedules.Add(schedule);
        await context.SaveChangesAsync();

        var opsConsumer = new MockOperationalStatusConsumer
        {
            HasRestriction = true,
            Reason = "Severe marine swell alert issued by coastal authority."
        };
        var service = new EvaluationService(context, opsConsumer, NullLogger<EvaluationService>.Instance);

        var request = new AvailabilityEvaluationRequest(
            offering.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(2));

        var response = await service.EvaluateAvailabilityAsync(request);

        Assert.Equal(AvailabilityStatus.Unavailable, response.Status);
        Assert.Contains(response.ReasonCodes, r => r.Contains("OPERATIONAL_RESTRICTION_ACTIVE"));
    }

    [Fact]
    [Trait("CaseId", "EXP-EVAL-008")]
    public async Task Availability_Evaluation_Returns_Unknown_When_Operations_Service_Is_Unavailable()
    {
        using var context = CreateContext();
        var dest = new Destination { Id = Guid.NewGuid(), Name = "Mirissa", Status = PublicationStatus.Published };
        var act = new Activity { Id = Guid.NewGuid(), Code = "WHALES", Name = "Whales", Status = PublicationStatus.Published };
        var offering = new Offering
        {
            Id = Guid.NewGuid(),
            DestinationId = dest.Id,
            ActivityId = act.Id,
            Title = "Morning Tour",
            Status = PublicationStatus.Published
        };
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            OfferingId = offering.Id,
            StartsAt = DateTimeOffset.UtcNow.AddHours(-1),
            EndsAt = DateTimeOffset.UtcNow.AddHours(5),
            IsActive = true
        };

        context.Destinations.Add(dest);
        context.Activities.Add(act);
        context.Offerings.Add(offering);
        context.Schedules.Add(schedule);
        await context.SaveChangesAsync();

        var opsConsumer = new MockOperationalStatusConsumer
        {
            HasRestriction = false,
            SourceStatus = "UNAVAILABLE" // External service failure
        };
        var service = new EvaluationService(context, opsConsumer, NullLogger<EvaluationService>.Instance);

        var request = new AvailabilityEvaluationRequest(
            offering.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(2));

        var response = await service.EvaluateAvailabilityAsync(request);

        Assert.Equal(AvailabilityStatus.Unknown, response.Status);
        Assert.Contains(response.ReasonCodes, r => r.Contains("OPERATIONAL_SERVICE_UNAVAILABLE"));
    }
}
