using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Models.Dtos;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class PlannerNewRoutesTests(PlannerApiFactory factory) : IClassFixture<PlannerApiFactory>
{
    // Start the shared host with its fixture configuration before any cloned
    // host can restore the process environment during disposal.
    private readonly HttpClient _client = factory.CreateClient();
    private sealed class Catalogue : IPlannerCatalogueClient
    {
        public Task<PlannerCatalogueResult> GetDestinationsAsync(CancellationToken ct) => Task.FromResult(
            new PlannerCatalogueResult("AVAILABLE", [new(Guid.NewGuid(), "Mirissa", "Southern coast", "Asia/Colombo", [new(Guid.NewGuid(), "Lagoon walk")])], null));
    }
    private HttpRequestMessage Get(string path, Guid owner, string permission)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(owner, permission));
        return request;
    }
    [Fact]
    [Trait("TestId", "PLANNER-CATALOGUE-HTTP-001")]
    public async Task Catalogue_requires_create_permission_and_returns_named_canonical_choices()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPlannerCatalogueClient>(); services.AddSingleton<IPlannerCatalogueClient>(new Catalogue());
        }));
        using var client = configured.CreateClient();
        using var anonymous = await client.GetAsync("/api/planner/catalogue");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Contains("Bearer", anonymous.Headers.WwwAuthenticate.ToString());
        using var deniedRequest = Get("/api/planner/catalogue", Guid.NewGuid(), "planner.recommendations.read");
        using var denied = await client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var allowedRequest = Get("/api/planner/catalogue", Guid.NewGuid(), "planner.recommendations.create");
        using var allowed = await client.SendAsync(allowedRequest);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal("application/json", allowed.Content.Headers.ContentType!.MediaType);
        var result = await allowed.Content.ReadFromJsonAsync<PlannerCatalogueResult>();
        Assert.Equal("AVAILABLE", result!.Status);
        var destination = Assert.Single(result.Destinations);
        Assert.Equal("Mirissa", destination.Name);
        Assert.Equal("Asia/Colombo", destination.TimeZone);
        Assert.NotEqual(Guid.Empty, destination.DestinationId);
        Assert.Equal("Lagoon walk", Assert.Single(destination.Activities).Name);
    }
    [Fact]
    [Trait("TestId", "PLANNER-HISTORY-HTTP-001")]
    public async Task History_returns_an_empty_array_for_owner_and_does_not_disclose_another_owners_trip()
    {
        var client = _client;
        var owner = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(1);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/planner/itineraries")
        {
            Content = JsonContent.Create(new CreateItineraryRequestDto("History test", null, start, start.AddHours(2), []))
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(owner, "planner.itineraries.manage"));
        using var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var trip = (await created.Content.ReadFromJsonAsync<ItineraryDto>())!;
        var path = $"/api/planner/itineraries/{trip.ItineraryId}/re-evaluations";
        using var anonymous = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var deniedRequest = Get(path, owner, "planner.recommendations.read");
        using var denied = await client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var otherRequest = Get(path, Guid.NewGuid(), "planner.itineraries.manage");
        using var other = await client.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var ownRequest = Get(path, owner, "planner.itineraries.manage");
        using var own = await client.SendAsync(ownRequest);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Empty((await own.Content.ReadFromJsonAsync<List<ItineraryReEvaluationResultDto>>())!);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoastalPlannerDbContext>();
        Assert.True(await db.Itineraries.AnyAsync(i => i.ItineraryId == trip.ItineraryId && i.OwnerUserId == owner));
        Assert.False(await db.ItineraryEvaluations.AnyAsync(e => e.ItineraryId == trip.ItineraryId));
    }
}
