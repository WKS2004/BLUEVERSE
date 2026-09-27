using System.Net;
using System.Net.Http.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class AvailabilityApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AvailabilityApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AVAIL-001")]
    public async Task EvaluateAvailability_Rejects_Invalid_Interval()
    {
        using var client = _factory.CreateClient();

        var request = new AvailabilityEvaluationRequest(
            OfferingId: Guid.NewGuid(),
            StartsAt: DateTimeOffset.UtcNow.AddHours(2),
            EndsAt: DateTimeOffset.UtcNow.AddHours(1)); // Invalid: endsAt < startsAt!

        using var response = await client.PostAsJsonAsync("/api/experiences/availability/evaluations", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AVAIL-002")]
    public async Task EvaluateAvailability_Returns_Expected_Structure_For_NonExistent_Offering()
    {
        using var client = _factory.CreateClient();

        var request = new AvailabilityEvaluationRequest(
            OfferingId: Guid.NewGuid(),
            StartsAt: DateTimeOffset.UtcNow,
            EndsAt: DateTimeOffset.UtcNow.AddHours(2));

        using var response = await client.PostAsJsonAsync("/api/experiences/availability/evaluations", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AvailabilityEvaluationResponse>();
        Assert.NotNull(result);
        Assert.Equal(AvailabilityStatus.Unavailable, result.Status);
        Assert.Contains(result.ReasonCodes, r => r.Contains("OFFERING_NOT_FOUND"));
    }
}
