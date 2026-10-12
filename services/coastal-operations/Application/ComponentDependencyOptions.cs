namespace Blueverse.CoastalOperations.Application;

public sealed class ComponentDependencyOptions
{
    public int TimeoutSeconds { get; set; } = 2;
    public int MaxRetries { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 150;
    public int MaxResponseBytes { get; set; } = 32 * 1024;
    public ComponentEndpointOptions Member1Experience { get; set; } = new()
    {
        BaseAddress = "http://experience-biodiversity:8080",
        AvailabilityPath = "/api/experiences/{targetType}/{targetId}/availability"
    };
    public ComponentEndpointOptions Member2MarineSafety { get; set; } = new()
    {
        BaseAddress = "http://marine-safety:8080",
        SuitabilityPath = "/api/marine-safety/suitability-assessments"
    };
    public ComponentEndpointOptions Member3CoastalPlanner { get; set; } = new()
    {
        BaseAddress = "http://coastal-planner:8080",
        WorkflowPath = "/api/coastal-planner/workflows/{workflowId}"
    };
}

public sealed class ComponentEndpointOptions
{
    public string? BaseAddress { get; set; }
    public string? AvailabilityPath { get; set; }
    public string? SuitabilityPath { get; set; }
    public string? WorkflowPath { get; set; }
}
