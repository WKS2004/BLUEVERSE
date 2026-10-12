using Blueverse.CoastalOperations.Contracts;

namespace Blueverse.CoastalOperations.Application;

public static class TargetOperationalStateBootstrapPolicy
{
    /// <summary>
    /// OPEN is only the initial Coastal Operations restriction state. It is
    /// established only after Member 1 confirms the target identity; schedule
    /// availability and safety remain independently owned and evaluated.
    /// </summary>
    public static bool IsConfirmedTarget(ComponentDependencyResult? experienceResult) =>
        experienceResult is
        {
            Service: "member-1-experience",
            Status: "RESPONDED",
            Data.AvailabilityStatus: "AVAILABLE" or "UNAVAILABLE"
        };
}
