using Microsoft.AspNetCore.Mvc;

namespace Blueverse.MarineSafety.Dtos;

/// <summary>Unavailable data for this operation; it does not describe service-wide readiness.</summary>
public sealed class DependencyUnavailableProblemDetails : ProblemDetails
{
    public string Availability => "UNAVAILABLE";
    public required string Dependency { get; init; }
    public required string Code { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<string> MissingFields { get; init; }
    public bool Retryable => true;
    public object? Data => null;

    public static DependencyUnavailableProblemDetails Catalogue(string reason) => new()
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Title = "Activity Data Unavailable",
        Detail = "The activity catalogue is unavailable. Retry to refresh activity data.",
        Dependency = "experience-biodiversity",
        Code = "ACTIVITY_DATA_UNAVAILABLE",
        Reason = reason,
        MissingFields = ["activityCatalogue"]
    };

    public static DependencyUnavailableProblemDetails Conditions(string detail) => new()
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Title = "Marine Conditions Unavailable",
        Detail = detail,
        Dependency = "open-meteo",
        Code = "CONDITION_DATA_UNAVAILABLE",
        Reason = "UNAVAILABLE",
        MissingFields = ["windSpeed", "waveHeight", "swellHeight", "rain", "weatherCode"]
    };
}
