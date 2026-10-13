using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Blueverse.CoastalPlanner.Integration;

public record PlanningCoordinationAvailability(string Status, bool Retryable);
public record PlanningCoordinationRequest(Guid WorkflowId, Guid RecommendationId, Guid DestinationId = default,
    DateTime StartsAt = default, DateTime EndsAt = default, IReadOnlyList<Guid>? PreferredActivityIds = null,
    string? Objective = null, string ExperienceLevel = "INTERMEDIATE", int DurationHours = 0,
    bool IncludeBiodiversityContext = true);
public record PlanningCoordinationOutcome(Guid WorkflowId, string Status, string? Message);
public interface IPlanningCoordinationClient
{
    Task<PlanningCoordinationAvailability> CheckAvailabilityAsync(CancellationToken ct);
    Task<PlanningCoordinationOutcome> DispatchAsync(PlanningCoordinationRequest request, CancellationToken ct);
}

public sealed class PlanningCoordinationClient(
    HttpClient http,
    IConfiguration config,
    IHttpContextAccessor? contextAccessor = null,
    ILogger<PlanningCoordinationClient>? logger = null) : IPlanningCoordinationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PlanningCoordinationAvailability> CheckAvailabilityAsync(CancellationToken ct)
    {
        var endpoint = config["AgenticAi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(endpoint)) return new("NOT_CONNECTED", false);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(root.UserInfo) || !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment))
            return new("UNAVAILABLE", false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, root.AbsoluteUri.TrimEnd('/') + "/internal/agentic/agents/planning-coordination/health");
            var key = config["AgenticAi:ServiceKey"];
            if (!string.IsNullOrWhiteSpace(key)) request.Headers.TryAddWithoutValidation("X-Blueverse-Service-Key", key);
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return new("UNAVAILABLE", true);
            var result = await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken: timeout.Token);
            return result?.Status switch
            {
                "AVAILABLE" => new("AVAILABLE", true),
                "NOT_CONNECTED" => new("NOT_CONNECTED", false),
                _ => new("UNAVAILABLE", true)
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new("UNAVAILABLE", true); }
        catch (HttpRequestException) { return new("UNAVAILABLE", true); }
        catch (JsonException) { return new("UNAVAILABLE", false); }
        catch (NotSupportedException) { return new("UNAVAILABLE", false); }
    }
    public async Task<PlanningCoordinationOutcome> DispatchAsync(PlanningCoordinationRequest request, CancellationToken ct)
    {
        if (!config.GetValue<bool>("AgenticAi:Enabled"))
            return new(request.WorkflowId, string.IsNullOrWhiteSpace(config["AgenticAi:BaseUrl"]) ? "NOT_CONNECTED" : "UNAVAILABLE",
                "Planning coordination is not enabled before the shared Agentic AI integration gate.");
        if (request.WorkflowId == Guid.Empty || request.RecommendationId == Guid.Empty || request.DestinationId == Guid.Empty ||
            request.PreferredActivityIds is null || request.PreferredActivityIds.Count > 50 ||
            request.PreferredActivityIds.Contains(Guid.Empty) || request.PreferredActivityIds.Distinct().Count() != request.PreferredActivityIds.Count)
            throw new ArgumentException("Validated business references and time range are required.");
        var startsAtUtc = request.StartsAt.ToUniversalTime();
        var endsAtUtc = request.EndsAt.ToUniversalTime();
        if (endsAtUtc <= startsAtUtc)
            throw new ArgumentException("Validated business references and time range are required.");
        var endpoint = config["AgenticAi:BaseUrl"]?.TrimEnd('/');
        var availability = await CheckAvailabilityAsync(ct);
        if (availability.Status != "AVAILABLE")
        {
            logger?.LogInformation("Planning agent dispatch skipped because availability is {Status}.", availability.Status);
            return new(request.WorkflowId, availability.Status, "Planning coordination is unavailable.");
        }

        var catalogueContext = await GetEligibleCatalogueContextAsync(request, startsAtUtc, endsAtUtc, ct);
        if (catalogueContext is null)
        {
            logger?.LogInformation("Planning agent dispatch skipped because validated catalogue context was unavailable.");
            return new(request.WorkflowId, "UNAVAILABLE", "Validated experience context is unavailable for this planning period.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        var body = new
        {
            contractVersion = 1,
            workflowId = request.WorkflowId,
            recommendationId = request.RecommendationId,
            workflowType = "TOURIST_RECOMMENDATION",
            objective = string.IsNullOrWhiteSpace(request.Objective)
                ? "Gather validated coastal destination and marine context for this recommendation workflow."
                : request.Objective.Trim(),
            destinationId = request.DestinationId,
            timeRange = new { startsAt = startsAtUtc, endsAt = endsAtUtc },
            agentContext = new
            {
                destinationId = catalogueContext.DestinationId,
                activityId = catalogueContext.ActivityId,
                activityName = catalogueContext.ActivityName,
                latitude = catalogueContext.Latitude,
                longitude = catalogueContext.Longitude,
                offeringId = catalogueContext.OfferingId
            },
            preferredActivityIds = request.PreferredActivityIds,
            constraints = new
            {
                experienceLevel = request.ExperienceLevel,
                durationHours = request.DurationHours,
                includeBiodiversityContext = request.IncludeBiodiversityContext
            }
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint + "/internal/agentic/agents/planning-coordination/dispatch")
        {
            // A byte-backed body declares Content-Length. The private Python
            // service deliberately rejects chunked requests before parsing.
            Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions))
        };
        message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var serviceKey = config["AgenticAi:ServiceKey"]!;
        message.Headers.TryAddWithoutValidation("X-Blueverse-Service-Key", serviceKey);
        message.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));
        try
        {
            using var response = await http.SendAsync(message, timeout.Token);
            var result = await response.Content.ReadFromJsonAsync<DispatchResponse>(cancellationToken: timeout.Token);
            if (result?.WorkflowId != request.WorkflowId || string.IsNullOrWhiteSpace(result.Status))
            {
                logger?.LogWarning("Planning agent returned an invalid dispatch envelope (HTTP {StatusCode}).", (int)response.StatusCode);
                return new(request.WorkflowId, "INVALID", "Planning coordination returned an invalid response.");
            }
            logger?.LogInformation("Planning agent dispatch completed with {Outcome} (HTTP {StatusCode}, error {ErrorCode}).",
                result.Status, (int)response.StatusCode, result.ErrorCode ?? "none");
            return new(request.WorkflowId, result.Status, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.LogInformation("Planning agent dispatch timed out before returning a result.");
            return new(request.WorkflowId, "UNAVAILABLE", "Planning coordination timed out.");
        }
        catch (HttpRequestException)
        {
            logger?.LogInformation("Planning agent service could not be reached.");
            return new(request.WorkflowId, "UNAVAILABLE", "Planning coordination is unavailable.");
        }
        catch (JsonException)
        {
            logger?.LogInformation("Planning agent response did not match its expected data contract.");
            return new(request.WorkflowId, "INVALID", "Planning coordination returned an invalid response.");
        }
    }

    private async Task<PeerCatalogueItem?> GetEligibleCatalogueContextAsync(
        PlanningCoordinationRequest request, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken ct)
    {
        var preferredActivityIds = request.PreferredActivityIds;
        if (preferredActivityIds is null) return null;

        var endpointRoot = config["PeerServices:ExperienceCatalogueUrl"];
        if (!Uri.TryCreate(endpointRoot, UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(root.UserInfo) || !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment))
        {
            logger?.LogInformation("Planning catalogue context was skipped because its service URL is not valid.");
            return null;
        }

        var query = "destinationId=" + Uri.EscapeDataString(request.DestinationId.ToString());
        if (preferredActivityIds.Count > 0)
            query += "&activityIds=" + Uri.EscapeDataString(string.Join(",", preferredActivityIds));
        var endpoint = new Uri(root.AbsoluteUri.TrimEnd('/') + "/api/experiences/catalogue?" + query);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var catalogueRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (!TryAttachCallerAuthorization(catalogueRequest))
            {
                logger?.LogInformation("Planning agent dispatch skipped because authenticated caller authorization was unavailable.");
                return null;
            }
            using var response = await http.SendAsync(catalogueRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger?.LogInformation("Planning catalogue request returned HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var items = await response.Content.ReadFromJsonAsync<List<PeerCatalogueItem>>(cancellationToken: timeout.Token);
            var now = DateTime.UtcNow;
            if (items is null || items.Count > 5000 ||
                items.Any(item => item is null || !IsValidCatalogueItem(item, request.DestinationId, now)))
            {
                logger?.LogInformation("Planning catalogue response failed deterministic validation (item count {ItemCount}).", items?.Count ?? -1);
                return null;
            }

            var activityOrder = preferredActivityIds
                .Select((activityId, index) => (activityId, index))
                .ToDictionary(item => item.activityId, item => item.index);
            var eligible = items
                .Where(item => IsEligibleForPlanning(item, request, startsAtUtc, endsAtUtc))
                .OrderBy(item => activityOrder.TryGetValue(item.ActivityId, out var index) ? index : int.MaxValue)
                .ThenBy(item => item.ActivityName, StringComparer.Ordinal)
                .ThenBy(item => item.Title, StringComparer.Ordinal)
                .ThenBy(item => item.OfferingId)
                .FirstOrDefault();
            if (eligible is null)
                logger?.LogInformation("Planning catalogue response contained {ItemCount} valid items but none were eligible for the requested time and experience level.", items.Count);
            return eligible;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.LogInformation("Planning catalogue request timed out before validated context was available.");
            return null;
        }
        catch (HttpRequestException)
        {
            logger?.LogInformation("Planning catalogue service could not be reached.");
            return null;
        }
        catch (JsonException)
        {
            logger?.LogInformation("Planning catalogue response did not match its expected data contract.");
            return null;
        }
        catch (NotSupportedException)
        {
            logger?.LogInformation("Planning catalogue response used an unsupported data contract.");
            return null;
        }
        catch (UriFormatException)
        {
            logger?.LogInformation("Planning catalogue request URI was not valid.");
            return null;
        }
    }

    private bool TryAttachCallerAuthorization(HttpRequestMessage request)
    {
        var context = contextAccessor?.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true) return false;

        var authorization = context.Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(authorization, out var parsed) &&
            string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            request.Headers.Authorization = parsed;
            return true;
        }

        if (context.Request.Cookies.TryGetValue("blueverse_access_token", out var cookieToken) &&
            AuthenticationHeaderValue.TryParse("Bearer " + cookieToken, out parsed) &&
            !string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            request.Headers.Authorization = parsed;
            return true;
        }

        return false;
    }

    private static bool IsValidCatalogueItem(PeerCatalogueItem item, Guid destinationId, DateTime now) =>
        item.DestinationId == destinationId &&
        !string.IsNullOrWhiteSpace(item.DestinationName) && item.DestinationName.Length <= 200 &&
        (item.Region is null || item.Region.Length <= 100) &&
        item.ActivityId != Guid.Empty && !string.IsNullOrWhiteSpace(item.ActivityName) && item.ActivityName.Length <= 100 &&
        item.OfferingId is { } offeringId && offeringId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(item.Title) && item.Title.Length <= 200 &&
        item.PublicationState == "PUBLISHED" && item.AvailabilityStatus is "AVAILABLE" or "UNAVAILABLE" &&
        item.Latitude is { } latitude && double.IsFinite(latitude) && latitude is >= -90 and <= 90 &&
        item.Longitude is { } longitude && double.IsFinite(longitude) && longitude is >= -180 and <= 180 &&
        item.CheckedAt is { Kind: DateTimeKind.Utc } checkedAt && checkedAt <= now.AddMinutes(1) && checkedAt >= now.AddMinutes(-15) &&
        ((item.AvailableFrom is null && item.AvailableUntil is null && item.TimeZone is null) ||
         (item.AvailableFrom is { Kind: DateTimeKind.Utc } from && item.AvailableUntil is { Kind: DateTimeKind.Utc } until &&
          from < until && ValidZone(item.TimeZone))) &&
        (item.ExperienceLevels is null || item.ExperienceLevels.Count <= 3 &&
            item.ExperienceLevels.All(level => level is "BEGINNER" or "INTERMEDIATE" or "ADVANCED") &&
            item.ExperienceLevels.Distinct(StringComparer.Ordinal).Count() == item.ExperienceLevels.Count);

    private static bool IsEligibleForPlanning(
        PeerCatalogueItem item, PlanningCoordinationRequest request, DateTime startsAtUtc, DateTime endsAtUtc) =>
        item.AvailabilityStatus == "AVAILABLE" &&
        item.AvailableFrom is { Kind: DateTimeKind.Utc } from && from <= startsAtUtc &&
        item.AvailableUntil is { Kind: DateTimeKind.Utc } until && until >= endsAtUtc &&
        (request.PreferredActivityIds!.Count == 0 || request.PreferredActivityIds.Contains(item.ActivityId)) &&
        (item.ExperienceLevels is null || item.ExperienceLevels.Contains(request.ExperienceLevel, StringComparer.Ordinal));

    private static bool ValidZone(string? zone) => !string.IsNullOrWhiteSpace(zone) && zone.Length <= 100 &&
        TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _);

    private record HealthResponse(string Status);
    private record DispatchResponse(Guid WorkflowId, string Status, string? ErrorCode = null);
}
