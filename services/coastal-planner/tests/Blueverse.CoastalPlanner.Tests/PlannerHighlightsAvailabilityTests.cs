using Blueverse.CoastalPlanner.Controllers;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class PlannerHighlightsAvailabilityTests
{
    private sealed class Catalogue(string status, List<PlannerDestinationOption> destinations, string? message = null)
        : IPlannerCatalogueClient
    {
        public Task<PlannerCatalogueResult> GetDestinationsAsync(CancellationToken ct) =>
            Task.FromResult(new PlannerCatalogueResult(status, destinations, message));
    }

    [Fact]
    public async Task Highlights_returns_service_unavailable_when_destination_catalogue_is_unavailable()
    {
        var controller = CreateController(new Catalogue("UNAVAILABLE", [], "Destinations are temporarily unavailable."));

        var response = await controller.GetHighlights(CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(503, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Coastal highlights unavailable", details.Title);
        Assert.Contains("Destinations are temporarily unavailable", details.Detail);
    }

    [Fact]
    public async Task Highlights_returns_empty_success_when_available_catalogue_has_no_destinations()
    {
        var controller = CreateController(new Catalogue("AVAILABLE", []));

        var response = await controller.GetHighlights(CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<RecommendationHighlightDto>>(result.Value));
    }

    [Fact]
    public async Task Highlights_returns_service_unavailable_when_all_experience_offerings_are_unavailable()
    {
        var destination = new PlannerDestinationOption(Guid.NewGuid(), "Mirissa", "Southern coast", "Asia/Colombo", []);
        var controller = CreateController(new Catalogue("AVAILABLE", [destination]));

        var response = await controller.GetHighlights(CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(503, problem.StatusCode);
        Assert.Contains("Experience Catalogue endpoint has not responded", Assert.IsType<ProblemDetails>(problem.Value).Detail);
    }

    private static RecommendationsController CreateController(IPlannerCatalogueClient catalogue) =>
        new(null!, new TestPeerServicesClient(), catalogue);
}
