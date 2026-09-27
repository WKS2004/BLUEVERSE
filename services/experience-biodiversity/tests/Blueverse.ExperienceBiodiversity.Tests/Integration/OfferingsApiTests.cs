using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class OfferingsApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OfferingsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-001")]
    public async Task GetAllOfferings_Returns_Ok_With_Items()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/experiences/offerings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-002")]
    public async Task GetOfferingById_Unknown_Returns_NotFound_ProblemDetails()
    {
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        using var response = await client.GetAsync($"/api/experiences/offerings/{unknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Offering Not Found", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-003")]
    public async Task CreateOffering_InvalidDestination_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();

        var invalidReq = new CreateOfferingRequest(
            DestinationId: Guid.NewGuid(),
            ActivityId: Guid.NewGuid(),
            Title: "Invalid Offering",
            Description: "Should fail validation",
            Price: 50.00m,
            Currency: "USD",
            DurationMinutes: 60,
            MaxCapacity: 10);

        using var response = await client.PostAsJsonAsync("/api/experiences/offerings", invalidReq);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Destination", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-004")]
    public async Task Schedule_Create_And_Delete_Lifecycle()
    {
        using var client = _factory.CreateClient();

        // 1. Create destination
        var destReq = new CreateDestinationRequest("Arugam Test Bay", $"arugam-bay-{Guid.NewGuid():N}"[..20], "Surf bay", "Eastern Province", 6.84, 81.83);
        using var destRes = await client.PostAsJsonAsync("/api/experiences/destinations", destReq);
        Assert.Equal(HttpStatusCode.Created, destRes.StatusCode);
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(dest);

        // 2. Create activity
        var actReq = new CreateActivityRequest($"ACT_{Guid.NewGuid():N}"[..15], "Surf Test", "Surfing", "WaterSports");
        using var actRes = await client.PostAsJsonAsync("/api/experiences/activities", actReq);
        Assert.Equal(HttpStatusCode.Created, actRes.StatusCode);
        var act = await actRes.Content.ReadFromJsonAsync<ActivityDto>();
        Assert.NotNull(act);

        // 3. Create offering
        var offReq = new CreateOfferingRequest(dest.Id, act.Id, "Morning Surf Lesson", "Beginner lesson", 45m, "USD", 120, 6);
        using var offRes = await client.PostAsJsonAsync("/api/experiences/offerings", offReq);
        Assert.Equal(HttpStatusCode.Created, offRes.StatusCode);
        var off = await offRes.Content.ReadFromJsonAsync<OfferingDto>();
        Assert.NotNull(off);

        // 4. Add a schedule
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var end = start.AddHours(4);
        var scheduleReq = new CreateScheduleRequest(
            StartsAt: start,
            EndsAt: end,
            TimeZoneId: "Asia/Colombo",
            IsActive: true);

        using var addRes = await client.PostAsJsonAsync($"/api/experiences/offerings/{off.Id}/schedules", scheduleReq);
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);

        var createdSchedule = await addRes.Content.ReadFromJsonAsync<ScheduleDto>();
        Assert.NotNull(createdSchedule);
        Assert.Equal(off.Id, createdSchedule.OfferingId);

        // 5. Delete the schedule
        using var delRes = await client.DeleteAsync($"/api/experiences/offerings/{off.Id}/schedules/{createdSchedule.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-005")]
    public async Task CreateOffering_InvalidActivity_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();

        // 1. Create a valid destination
        var destReq = new CreateDestinationRequest("Valid Bay", null, "Valid", "Southern", 6.0, 80.0);
        using var destRes = await client.PostAsJsonAsync("/api/experiences/destinations", destReq);
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(dest);

        // 2. Submit offering with non-existent activity
        var invalidReq = new CreateOfferingRequest(
            DestinationId: dest.Id,
            ActivityId: Guid.NewGuid(),
            Title: "Invalid Activity Offering",
            Description: "Should fail",
            Price: 20m,
            Currency: "USD",
            DurationMinutes: 60,
            MaxCapacity: 5);

        using var response = await client.PostAsJsonAsync("/api/experiences/offerings", invalidReq);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Activity", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-006")]
    public async Task UpdateOffering_UpdatesProperties_And_Returns_UpdatedDto()
    {
        using var client = _factory.CreateClient();

        // 1. Setup destination and activity
        var destRes = await client.PostAsJsonAsync("/api/experiences/destinations",
            new CreateDestinationRequest("Off Bay", null, "Off Desc", "Southern", 6.1, 80.1));
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();

        var actRes = await client.PostAsJsonAsync("/api/experiences/activities",
            new CreateActivityRequest($"ACT_U_{Guid.NewGuid():N}"[..15], "Off Act", "Desc", "Sport"));
        var act = await actRes.Content.ReadFromJsonAsync<ActivityDto>();

        // 2. Create offering
        var createRes = await client.PostAsJsonAsync("/api/experiences/offerings",
            new CreateOfferingRequest(dest!.Id, act!.Id, "Initial Offering", "Initial desc", 30m, "USD", 60, 4));
        var off = await createRes.Content.ReadFromJsonAsync<OfferingDto>();
        Assert.NotNull(off);

        // 3. Update offering
        var updateReq = new UpdateOfferingRequest("Updated Offering Title", "Updated description", 55m, "EUR", 90, 8);
        using var updateRes = await client.PutAsJsonAsync($"/api/experiences/offerings/{off.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<OfferingDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Offering Title", updated.Title);
        Assert.Equal(55m, updated.Price);
        Assert.Equal("EUR", updated.Currency);
        Assert.Equal(90, updated.DurationMinutes);
        Assert.Equal(8, updated.MaxCapacity);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-007")]
    public async Task UpdateOffering_UnknownId_Returns_NotFound_ProblemDetails()
    {
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        var updateReq = new UpdateOfferingRequest("Unknown", "Desc", 10m, "USD", 30, 2);
        using var response = await client.PutAsJsonAsync($"/api/experiences/offerings/{unknownId}", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Offering Not Found", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-008")]
    public async Task AddSchedule_InvalidInterval_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();

        // Setup parent offering
        var destRes = await client.PostAsJsonAsync("/api/experiences/destinations",
            new CreateDestinationRequest("Bay Inter", null, "Desc", "South", 6.2, 80.2));
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();

        var actRes = await client.PostAsJsonAsync("/api/experiences/activities",
            new CreateActivityRequest($"ACT_I_{Guid.NewGuid():N}"[..15], "Inter Act", "Desc", "Sport"));
        var act = await actRes.Content.ReadFromJsonAsync<ActivityDto>();

        var offRes = await client.PostAsJsonAsync("/api/experiences/offerings",
            new CreateOfferingRequest(dest!.Id, act!.Id, "Interval Off", "Desc", 25m, "USD", 60, 5));
        var off = await offRes.Content.ReadFromJsonAsync<OfferingDto>();

        // Schedule with EndsAt <= StartsAt
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var end = start.AddHours(-1); // Invalid!
        var scheduleReq = new CreateScheduleRequest(start, end, "Asia/Colombo", true);

        using var response = await client.PostAsJsonAsync($"/api/experiences/offerings/{off!.Id}/schedules", scheduleReq);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Schedule Interval", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-009")]
    public async Task UpdateSchedule_UpdatesProperties_And_Returns_Ok()
    {
        using var client = _factory.CreateClient();

        // 1. Setup offering
        var destRes = await client.PostAsJsonAsync("/api/experiences/destinations",
            new CreateDestinationRequest("Upd Sch Bay", null, "Desc", "South", 6.3, 80.3));
        var dest = await destRes.Content.ReadFromJsonAsync<DestinationDto>();

        var actRes = await client.PostAsJsonAsync("/api/experiences/activities",
            new CreateActivityRequest($"ACT_S_{Guid.NewGuid():N}"[..15], "Sch Act", "Desc", "Sport"));
        var act = await actRes.Content.ReadFromJsonAsync<ActivityDto>();

        var offRes = await client.PostAsJsonAsync("/api/experiences/offerings",
            new CreateOfferingRequest(dest!.Id, act!.Id, "Sch Off", "Desc", 35m, "USD", 90, 10));
        var off = await offRes.Content.ReadFromJsonAsync<OfferingDto>();

        // 2. Create schedule
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var end = start.AddHours(2);
        var createSchRes = await client.PostAsJsonAsync($"/api/experiences/offerings/{off!.Id}/schedules",
            new CreateScheduleRequest(start, end, "Asia/Colombo", true));
        var createdSch = await createSchRes.Content.ReadFromJsonAsync<ScheduleDto>();
        Assert.NotNull(createdSch);

        // 3. Update schedule
        var newStart = start.AddHours(1);
        var newEnd = newStart.AddHours(3);
        var updateSchReq = new UpdateScheduleRequest(newStart, newEnd, "UTC", false);

        using var updateSchRes = await client.PutAsJsonAsync($"/api/experiences/offerings/{off.Id}/schedules/{createdSch.Id}", updateSchReq);
        Assert.Equal(HttpStatusCode.OK, updateSchRes.StatusCode);

        var updatedSch = await updateSchRes.Content.ReadFromJsonAsync<ScheduleDto>();
        Assert.NotNull(updatedSch);
        Assert.False(updatedSch.IsActive);
        Assert.Equal("UTC", updatedSch.TimeZoneId);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-010")]
    public async Task UpdateSchedule_InvalidInterval_Returns_BadRequest()
    {
        using var client = _factory.CreateClient();
        var offeringId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var end = start.AddMinutes(-30);
        var updateSchReq = new UpdateScheduleRequest(start, end, "UTC", true);

        using var response = await client.PutAsJsonAsync($"/api/experiences/offerings/{offeringId}/schedules/{scheduleId}", updateSchReq);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid Schedule Interval", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-011")]
    public async Task UpdateSchedule_UnknownSchedule_Returns_NotFound()
    {
        using var client = _factory.CreateClient();
        var offeringId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var end = start.AddHours(1);
        var updateSchReq = new UpdateScheduleRequest(start, end, "UTC", true);

        using var response = await client.PutAsJsonAsync($"/api/experiences/offerings/{offeringId}/schedules/{scheduleId}", updateSchReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Schedule Not Found", title.GetString());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-OFF-012")]
    public async Task DeleteSchedule_UnknownSchedule_Returns_NotFound()
    {
        using var client = _factory.CreateClient();
        var offeringId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        using var response = await client.DeleteAsync($"/api/experiences/offerings/{offeringId}/schedules/{scheduleId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Schedule Not Found", title.GetString());
    }
}
