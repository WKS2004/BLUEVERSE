using System.Net.Http.Json;
using System.Text.Json;

namespace Blueverse.CoastalPlanner.Integration;

public record PlanningCoordinationAvailability(string Status, bool Retryable);
public record PlanningCoordinationRequest(Guid WorkflowId, Guid RecommendationId);
public record PlanningCoordinationOutcome(Guid WorkflowId, string Status, string? Message);
public interface IPlanningCoordinationClient
{
    Task<PlanningCoordinationAvailability> CheckAvailabilityAsync(CancellationToken ct);
    Task<PlanningCoordinationOutcome> DispatchAsync(PlanningCoordinationRequest request, CancellationToken ct);
}

// Pre-G07 access seam only. There are no agents, tools, model calls or AI execution records.
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
            using var response = await http.GetAsync(root.AbsoluteUri.TrimEnd('/') + "/internal/agentic/health", timeout.Token);
            if (!response.IsSuccessStatusCode) return new("UNAVAILABLE", true);
            var result = await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken: timeout.Token);
            return new(result?.Status == "AVAILABLE" ? "AVAILABLE" : "UNAVAILABLE", true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new("UNAVAILABLE", true); }
        catch (HttpRequestException) { return new("UNAVAILABLE", true); }
        catch (JsonException) { return new("UNAVAILABLE", false); }
        catch (NotSupportedException) { return new("UNAVAILABLE", false); }
    }
    public async Task<PlanningCoordinationOutcome> DispatchAsync(PlanningCoordinationRequest request, CancellationToken ct)
    {
        if (request.WorkflowId == Guid.Empty || request.RecommendationId == Guid.Empty) throw new ArgumentException("Business references are required.");
        var available = await CheckAvailabilityAsync(ct);
        return new(request.WorkflowId, available.Status == "NOT_CONNECTED" ? "NOT_CONNECTED" : "UNAVAILABLE",
            "Planning coordination is not enabled before the shared Agentic AI integration gate.");
    }
    private record HealthResponse(string Status);
}
