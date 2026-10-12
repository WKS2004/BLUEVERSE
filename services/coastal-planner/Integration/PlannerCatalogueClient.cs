using System.Net.Http.Json;
using System.Text.Json;

namespace Blueverse.CoastalPlanner.Integration;

public record PlannerActivityOption(Guid ActivityId, string Name);
public record PlannerDestinationOption(Guid DestinationId, string Name, string? Region, string? TimeZone,
    List<PlannerActivityOption> Activities, double? Latitude = null, double? Longitude = null);
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
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var endpoint = new Uri(root.AbsoluteUri.TrimEnd('/') + "/api/experiences/catalogue");
            using var response = await http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return Unavailable();
            var offerings = await response.Content.ReadFromJsonAsync<List<PeerCatalogueItem>>(cancellationToken: timeout.Token);
            if (offerings is null || offerings.Count > 5000 || offerings.Any(item => !IsValid(item))) return Unavailable();

            var destinations = offerings
                .GroupBy(item => item.DestinationId)
                .Select(group =>
                {
                    var first = group.First();
                    var timeZones = group.Select(item => item.TimeZone).Where(zone => !string.IsNullOrWhiteSpace(zone))
                        .Distinct(StringComparer.Ordinal).ToArray();
                    var timeZone = timeZones.Length == 1 ? timeZones[0] : null;
                    var activities = group.GroupBy(item => item.ActivityId)
                        .Select(activity => new PlannerActivityOption(activity.Key, activity.First().ActivityName!))
                        .OrderBy(activity => activity.Name, StringComparer.Ordinal).ToList();
                    return new PlannerDestinationOption(first.DestinationId, first.DestinationName!, first.Region, timeZone,
                        activities, first.Latitude, first.Longitude);
                })
                .OrderBy(destination => destination.Name, StringComparer.Ordinal)
                .ToList();

            return new("AVAILABLE", destinations, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        catch (HttpRequestException) { return Unavailable(); }
        catch (JsonException) { return Unavailable(); }
        catch (NotSupportedException) { return Unavailable(); }
    }

    private static bool IsValid(PeerCatalogueItem item) =>
        item.DestinationId != Guid.Empty && !string.IsNullOrWhiteSpace(item.DestinationName) && item.DestinationName.Length <= 200 &&
        (item.Region is null || item.Region.Length <= 100) &&
        item.ActivityId != Guid.Empty && !string.IsNullOrWhiteSpace(item.ActivityName) && item.ActivityName.Length <= 100 &&
        item.OfferingId is { } offeringId && offeringId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(item.Title) && item.Title.Length <= 200 &&
        item.PublicationState == "PUBLISHED" && item.AvailabilityStatus is "AVAILABLE" or "UNAVAILABLE" &&
        item.Latitude is { } latitude && double.IsFinite(latitude) && latitude is >= -90 and <= 90 &&
        item.Longitude is { } longitude && double.IsFinite(longitude) && longitude is >= -180 and <= 180 &&
        item.CheckedAt is { Kind: DateTimeKind.Utc } checkedAt && checkedAt <= DateTime.UtcNow.AddMinutes(1) && checkedAt >= DateTime.UtcNow.AddMinutes(-15) &&
        ((item.AvailableFrom is null && item.AvailableUntil is null && item.TimeZone is null) ||
         (item.AvailableFrom is { Kind: DateTimeKind.Utc } from && item.AvailableUntil is { Kind: DateTimeKind.Utc } until && from < until &&
          ValidZone(item.TimeZone))) &&
        (item.ExperienceLevels is null || item.ExperienceLevels.Count <= 3 &&
            item.ExperienceLevels.All(level => level is "BEGINNER" or "INTERMEDIATE" or "ADVANCED") &&
            item.ExperienceLevels.Distinct(StringComparer.Ordinal).Count() == item.ExperienceLevels.Count);

    private static bool ValidZone(string? zone) => !string.IsNullOrWhiteSpace(zone) && zone.Length <= 100 &&
        TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _);

    private static PlannerCatalogueResult Unavailable() => new("UNAVAILABLE", [],
        "Experience data is temporarily unavailable. Your saved trips are still available. Please try again later.");
}
