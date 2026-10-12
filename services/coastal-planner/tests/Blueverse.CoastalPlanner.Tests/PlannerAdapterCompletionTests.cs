using System.Net;
using System.Net.Http.Json;
using Blueverse.CoastalPlanner.Integration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

public sealed class PlannerAdapterCompletionTests
{
    private static IConfiguration Config(string key, string? value) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();

    [Fact]
    [Trait("TestId", "PLANNER-CATALOGUE-001")]
    public async Task Named_catalogue_returns_canonical_choices_sorted_and_checks_the_source_path()
    {
        var destination = Guid.NewGuid();
        var activity = Guid.NewGuid();
        var offering = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var handler = new Handler((request, _) =>
        {
            Assert.Equal("/api/experiences/catalogue", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] {
                new PeerCatalogueItem(destination, activity, offering, "Reef snorkelling", "AVAILABLE", "PUBLISHED",
                    now.AddHours(1), now.AddHours(3), ["BEGINNER", "INTERMEDIATE"], now,
                    "Asia/Colombo", 5.94, 80.45, "Snorkelling", "Southern Province", "Mirissa") }) });
        });
        var result = await new PlannerCatalogueClient(new HttpClient(handler), Config("PeerServices:ExperienceCatalogueUrl", "http://catalogue.test")).GetDestinationsAsync(default);
        Assert.Equal("AVAILABLE", result.Status);
        Assert.Equal(destination, Assert.Single(result.Destinations).DestinationId);
        Assert.Equal("Snorkelling", result.Destinations[0].Activities[0].Name);
        Assert.Equal(5.94, result.Destinations[0].Latitude);
        Assert.Null(result.Message);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("zone")]
    [InlineData("coordinates")]
    [InlineData("refused")]
    [Trait("TestId", "PLANNER-CATALOGUE-002")]
    public async Task Malformed_or_missing_catalogue_is_explicitly_unavailable_without_substitute_data(string condition)
    {
        var destination = new PlannerDestinationOption(Guid.NewGuid(), "Mirissa", "South", "Asia/Colombo", []);
        var handler = new Handler((_, _) => Task.FromResult(condition switch {
            "malformed" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") },
            "refused" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "zone" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Item() with { TimeZone = "invalid-zone" } }) },
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Item() with { Latitude = 95 } }) } }));
        var result = await new PlannerCatalogueClient(new HttpClient(handler), Config("PeerServices:ExperienceCatalogueUrl", "http://catalogue.test")).GetDestinationsAsync(default);
        Assert.Equal("UNAVAILABLE", result.Status);
        Assert.Empty(result.Destinations);
        Assert.DoesNotContain("catalogue.test", result.Message);
        Assert.Contains("saved trips", result.Message);

        PeerCatalogueItem Item() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Offering", "AVAILABLE", "PUBLISHED",
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), ["INTERMEDIATE"], DateTime.UtcNow,
            "Asia/Colombo", 5.94, 80.45, "Snorkelling", "Southern", "Mirissa");
    }

    [Fact]
    [Trait("TestId", "PLANNER-CATALOGUE-003")]
    public async Task Schedule_rows_are_grouped_into_one_destination_and_unique_activity_choices()
    {
        var destinationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var first = new PeerCatalogueItem(destinationId, activityId, Guid.NewGuid(), "Morning", "AVAILABLE", "PUBLISHED",
            now.AddHours(1), now.AddHours(2), ["BEGINNER"], now, "Asia/Colombo", 5.94, 80.45, "Snorkelling", "Southern", "Mirissa");
        var second = first with { OfferingId = Guid.NewGuid(), Title = "Afternoon", AvailableFrom = now.AddHours(3), AvailableUntil = now.AddHours(4) };
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { first, second }) }));
        var result = await new PlannerCatalogueClient(new HttpClient(handler), Config("PeerServices:ExperienceCatalogueUrl", "http://catalogue.test")).GetDestinationsAsync(default);
        var destination = Assert.Single(result.Destinations);
        Assert.Equal(destinationId, destination.DestinationId);
        Assert.Equal(activityId, Assert.Single(destination.Activities).ActivityId);
    }

    [Fact]
    [Trait("TestId", "PLANNER-AI-SEAM-001")]
    public async Task Unconfigured_agentic_seam_performs_no_network_or_agent_execution()
    {
        var calls = 0;
        var client = new PlanningCoordinationClient(new HttpClient(new Handler((_, _) => { calls++; throw new InvalidOperationException(); })), Config("AgenticAi:BaseUrl", null));
        Assert.Equal("NOT_CONNECTED", (await client.CheckAvailabilityAsync(default)).Status);
        var workflow = Guid.NewGuid();
        var outcome = await client.DispatchAsync(new(workflow, Guid.NewGuid()), default);
        Assert.Equal("NOT_CONNECTED", outcome.Status);
        Assert.Equal(workflow, outcome.WorkflowId);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("AVAILABLE", "AVAILABLE")]
    [InlineData("invented", "UNAVAILABLE")]
    [InlineData("{bad", "UNAVAILABLE")]
    [Trait("TestId", "PLANNER-AI-SEAM-002")]
    public async Task Configured_probe_validates_health_but_never_dispatches_before_G07(string response, string expected)
    {
        var calls = new List<string>();
        var client = new PlanningCoordinationClient(new HttpClient(new Handler((request, _) => {
            calls.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = response == "{bad" ? new StringContent(response) : JsonContent.Create(new { status = response }) });
        })), Config("AgenticAi:BaseUrl", "http://agentic.test"));
        Assert.Equal(expected, (await client.CheckAvailabilityAsync(default)).Status);
        var outcome = await client.DispatchAsync(new(Guid.NewGuid(), Guid.NewGuid()), default);
        Assert.Equal("UNAVAILABLE", outcome.Status);
        Assert.All(calls, path => Assert.Equal("/internal/agentic/health", path));
        Assert.Contains("not enabled", outcome.Message);
    }

    [Fact]
    [Trait("TestId", "PLANNER-AI-SEAM-003")]
    public async Task Caller_cancellation_is_propagated_and_unreachable_probe_is_safe()
    {
        var client = new PlanningCoordinationClient(new HttpClient(new Handler((_, _) => throw new HttpRequestException("private detail"))), Config("AgenticAi:BaseUrl", "http://agentic.test"));
        var unavailable = await client.CheckAvailabilityAsync(default);
        Assert.Equal("UNAVAILABLE", unavailable.Status);
        Assert.True(unavailable.Retryable);
        var cancelling = new PlanningCoordinationClient(new HttpClient(new Handler(async (_, ct) => {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct); return new(HttpStatusCode.OK);
        })), Config("AgenticAi:BaseUrl", "http://agentic.test"));
        using var token = new CancellationTokenSource(); token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelling.CheckAvailabilityAsync(token.Token));
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
}
