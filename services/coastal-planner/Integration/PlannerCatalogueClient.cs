using System.Net.Http.Json;
using System.Text.Json;

namespace Blueverse.CoastalPlanner.Integration;

public record PlannerActivityOption(Guid ActivityId, string Name);
public record PlannerDestinationOption(Guid DestinationId, string Name, string Region, string TimeZone,
    List<PlannerActivityOption> Activities);
public record PlannerCatalogueResult(string Status, List<PlannerDestinationOption> Destinations, string? Message);

public interface IPlannerCatalogueClient
{
    Task<PlannerCatalogueResult> GetDestinationsAsync(CancellationToken ct);
}

// A read projection of Member 1's canonical catalogue; the planner owns no destination data.
public sealed class PlannerCatalogueClient(HttpClient http, IConfiguration config) : IPlannerCatalogueClient
{
    public async Task<PlannerCatalogueResult> GetDestinationsAsync(CancellationToken ct)
    {
        var baseUrl = config["PeerServices:ExperienceCatalogueUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(root.UserInfo) || !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment))
            return Unavailable();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var response = await http.GetAsync(new Uri(root.AbsoluteUri.TrimEnd('/') + "/api/experiences/destinations"),
                HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return Unavailable();
            var destinations = await response.Content.ReadFromJsonAsync<List<PlannerDestinationOption>>(cancellationToken: timeout.Token);
            if (destinations is null || destinations.Count > 500 || destinations.Any(d => d is null ||
                d.DestinationId == Guid.Empty || string.IsNullOrWhiteSpace(d.Name) || d.Name.Length > 150 ||
                string.IsNullOrWhiteSpace(d.Region) || d.Region.Length > 150 || !ValidZone(d.TimeZone) ||
                d.Activities is null || d.Activities.Count > 100 || d.Activities.Any(a => a is null ||
                    a.ActivityId == Guid.Empty || string.IsNullOrWhiteSpace(a.Name) || a.Name.Length > 150) ||
                d.Activities.Select(a => a.ActivityId).Distinct().Count() != d.Activities.Count) ||
                destinations.Select(d => d.DestinationId).Distinct().Count() != destinations.Count)
                return Unavailable();
            return new("AVAILABLE", destinations.OrderBy(d => d.Name, StringComparer.Ordinal).ToList(), null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        catch (HttpRequestException) { return Unavailable(); }
        catch (JsonException) { return Unavailable(); }
        catch (NotSupportedException) { return Unavailable(); }
    }

    private static bool ValidZone(string? zone) => !string.IsNullOrWhiteSpace(zone) && zone.Length <= 100 &&
        TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _);
    private static PlannerCatalogueResult Unavailable() => new("UNAVAILABLE", [],
        "Destinations are temporarily unavailable. Your saved trips are still available. Please try again later.");
}
