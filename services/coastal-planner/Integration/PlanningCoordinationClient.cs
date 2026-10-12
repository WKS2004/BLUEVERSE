using System.Net.Http.Json;
using System.Text.Json;

namespace Blueverse.CoastalPlanner.Integration;

public record PlanningCoordinationAvailability(string Status, bool Retryable);
public record PlanningCoordinationRequest(Guid WorkflowId, Guid RecommendationId, Guid DestinationId = default,
    DateTime StartsAt = default, DateTime EndsAt = default, IReadOnlyList<Guid>? PreferredActivityIds = null);
public record PlanningCoordinationOutcome(Guid WorkflowId, string Status, string? Message);
public interface IPlanningCoordinationClient
{
    Task<PlanningCoordinationAvailability> CheckAvailabilityAsync(CancellationToken ct);
    Task<PlanningCoordinationOutcome> DispatchAsync(PlanningCoordinationRequest request, CancellationToken ct);
}

public sealed class PlanningCoordinationClient(HttpClient http, IConfiguration config) : IPlanningCoordinationClient
{
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
            using var request = new HttpRequestMessage(HttpMethod.Get, root.AbsoluteUri.TrimEnd('/') + "/internal/agentic/health");
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
            request.EndsAt <= request.StartsAt || request.PreferredActivityIds is null || request.PreferredActivityIds.Count > 50 || request.PreferredActivityIds.Contains(Guid.Empty))
            throw new ArgumentException("Validated business references and time range are required.");
        var endpoint = config["AgenticAi:BaseUrl"]?.TrimEnd('/');
        var availability = await CheckAvailabilityAsync(ct);
        if (availability.Status != "AVAILABLE") return new(request.WorkflowId, availability.Status, "Planning coordination is unavailable.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        var body = new
        {
            contractVersion = 1,
            workflowId = request.WorkflowId,
            recommendationId = request.RecommendationId,
            workflowType = "TOURIST_RECOMMENDATION",
            objective = "Gather validated coastal destination and marine context for this recommendation workflow.",
            destinationId = request.DestinationId,
            timeRange = new { startsAt = request.StartsAt.ToUniversalTime(), endsAt = request.EndsAt.ToUniversalTime() },
            preferredActivityIds = request.PreferredActivityIds,
            constraints = new { }
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint + "/internal/agentic/planning-coordination/dispatch")
        {
            Content = JsonContent.Create(body)
        };
        var serviceKey = config["AgenticAi:ServiceKey"]!;
        message.Headers.TryAddWithoutValidation("X-Blueverse-Service-Key", serviceKey);
        message.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));
        try
        {
            using var response = await http.SendAsync(message, timeout.Token);
            var result = await response.Content.ReadFromJsonAsync<DispatchResponse>(cancellationToken: timeout.Token);
            if (result?.WorkflowId != request.WorkflowId || string.IsNullOrWhiteSpace(result.Status))
                return new(request.WorkflowId, "INVALID", "Planning coordination returned an invalid response.");
            return new(request.WorkflowId, result.Status, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(request.WorkflowId, "UNAVAILABLE", "Planning coordination timed out."); }
        catch (HttpRequestException) { return new(request.WorkflowId, "UNAVAILABLE", "Planning coordination is unavailable."); }
        catch (JsonException) { return new(request.WorkflowId, "INVALID", "Planning coordination returned an invalid response."); }
    }
    private record HealthResponse(string Status);
    private record DispatchResponse(Guid WorkflowId, string Status);
}
