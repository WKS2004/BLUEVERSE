using Blueverse.ExperienceBiodiversity.DTOs;

namespace Blueverse.ExperienceBiodiversity.Services;

public interface IDependenciesDiagnosticsService
{
    Task<MicroserviceDependenciesStatusDto> CheckDependenciesAsync(CancellationToken cancellationToken = default);
}

public sealed class DependenciesDiagnosticsService : IDependenciesDiagnosticsService
{
    private readonly IResilientHttpExecutor _resilientExecutor;
    private readonly string _marineSafetyUrl;
    private readonly string _plannerServiceUrl;
    private readonly string _operationsServiceUrl;
    private readonly string _mapProviderUrl;

    public DependenciesDiagnosticsService(
        IResilientHttpExecutor resilientExecutor,
        IConfiguration configuration)
    {
        _resilientExecutor = resilientExecutor;
        _marineSafetyUrl = configuration["MarineSafetyUrl"]
            ?? configuration["MARINE_SAFETY_URL"]
            ?? configuration["Services:MarineSafetyUrl"]
            ?? "http://marine-safety:8080";

        _plannerServiceUrl = configuration["PlannerServiceUrl"]
            ?? configuration["PLANNER_SERVICE_URL"]
            ?? configuration["Services:PlannerServiceUrl"]
            ?? "http://coastal-planner:8080";

        _operationsServiceUrl = configuration["OperationsServiceUrl"]
            ?? configuration["OPERATIONS_SERVICE_URL"]
            ?? configuration["Services:OperationsServiceUrl"]
            ?? "http://coastal-operations:8080";

        _mapProviderUrl = configuration["MapSearchUrl"]
            ?? configuration["MAP_SEARCH_URL"]
            ?? "https://photon.komoot.io/api";
    }

    public async Task<MicroserviceDependenciesStatusDto> CheckDependenciesAsync(CancellationToken cancellationToken = default)
    {
        var dependencyChecks = new List<(string Key, string Name, string TargetUrl, string Fallback)>
        {
            ("marine-safety", "Marine Conditions & Safety Intelligence (Member 2)", $"{_marineSafetyUrl.TrimEnd('/')}/api/marine/conditions", "Safe fallback to UNKNOWN safety level; standard coastal caution advised"),
            ("coastal-planner", "Smart Coastal Planner & Biodiversity ML (Member 3)", $"{_plannerServiceUrl.TrimEnd('/')}/api/planner/biodiversity", "Safe degradation to empty predictions (ADR-0019); discovery browsing continues normally"),
            ("coastal-operations", "Coastal Operations, Advisories & Alerts (Member 4)", $"{_operationsServiceUrl.TrimEnd('/')}/api/operations/restrictions/check", "Strict safety fallback to UNKNOWN / UNAVAILABLE; optimistic availability prevented"),
            ("map-provider", "Photon Geocoding Search Engine", $"{_mapProviderUrl}?q=Sri+Lanka&limit=1", "Local destination database substring search fallback")
        };

        var items = new List<DependencyHealthItemDto>();
        bool anyFailed = false;

        foreach (var dep in dependencyChecks)
        {
            var res = await _resilientExecutor.ExecuteGetAsync(
                dep.TargetUrl,
                perAttemptTimeout: TimeSpan.FromSeconds(1),
                maxRetries: 1,
                initialRetryDelay: TimeSpan.FromMilliseconds(50),
                cancellationToken: cancellationToken);

            if (!res.Responded)
            {
                anyFailed = true;
            }

            items.Add(new DependencyHealthItemDto(
                ServiceKey: dep.Key,
                ServiceName: dep.Name,
                TargetEndpoint: dep.TargetUrl,
                Responded: res.Responded,
                HttpStatusCode: res.StatusCode,
                AttemptsCount: res.AttemptsCount,
                LatencyMs: res.DurationMs,
                Status: res.RemoteStatus,
                FallbackStrategy: dep.Fallback,
                Message: res.Message));
        }

        var overallStatus = anyFailed ? "HEALTHY_DEGRADED" : "HEALTHY_CONNECTED";

        return new MicroserviceDependenciesStatusDto(
            Microservice: "Blueverse.ExperienceBiodiversity",
            Version: "1.0.0",
            OverallStatus: overallStatus,
            Timestamp: DateTimeOffset.UtcNow,
            Dependencies: items,
            ResilienceNote: "Blueverse.ExperienceBiodiversity runs as an independent microservice. In accordance with microservices architecture principles, unreachable peer microservices do not crash or disable core experience discovery, scheduling, or map features.");
    }
}
