namespace Blueverse.MarineSafety.Providers;

/// <summary>
/// Configuration for the Open-Meteo provider adapter. Endpoints and freshness
/// limits are configuration, not hidden constants; changing them never
/// requires a code change.
/// </summary>
public sealed class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    private TimeSpan? _requestTimeout;
    private TimeSpan? _freshnessMaxAge;

    /// <summary>Open-Meteo Weather Forecast API base URL.</summary>
    public string WeatherBaseUrl { get; set; } = "https://api.open-meteo.com/";

    /// <summary>Open-Meteo Marine API base URL.</summary>
    public string MarineBaseUrl { get; set; } = "https://marine-api.open-meteo.com/";

    /// <summary>Per-attempt timeout in seconds for each provider call.</summary>
    public int RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>Bounded retry count for transient provider failures (0 = single attempt).</summary>
    public int RetryCount { get; set; } = 1;

    /// <summary>Maximum age in minutes of a snapshot still considered fresh.</summary>
    public int FreshnessMaxAgeMinutes { get; set; } = 60;

    /// <summary>Coordinate precision retained on read/write, minimizing location data shared with the provider.</summary>
    public int CoordinatePrecision { get; set; } = 3;

    /// <summary>Per-attempt provider timeout.</summary>
    public TimeSpan RequestTimeout =>
        _requestTimeout ??= TimeSpan.FromSeconds(Math.Max(1, RequestTimeoutSeconds));

    /// <summary>Configured freshness window.</summary>
    public TimeSpan FreshnessMaxAge =>
        _freshnessMaxAge ??= TimeSpan.FromMinutes(Math.Max(1, FreshnessMaxAgeMinutes));
}

/// <summary>
/// Freshness policy applied to snapshots. Exposed as a small policy object so
/// the suitability evaluator and snapshot service share one definition of
/// fresh/stale instead of duplicating the rule.
/// </summary>
public interface IFreshnessPolicy
{
    /// <summary>
    /// Classify a snapshot: FRESH when the retrieval happened within the
    /// configured maximum age of its own forecast time window; STALE when it
    /// is older; UNAVAILABLE is never returned here because a snapshot always
    /// has a retrieval timestamp.
    /// </summary>
    string Classify(Models.ConditionSnapshot snapshot, DateTime evaluatedAtUtc);
}

public sealed class FreshnessPolicy : IFreshnessPolicy
{
    private readonly OpenMeteoOptions _options;

    public FreshnessPolicy(Microsoft.Extensions.Options.IOptions<OpenMeteoOptions> options)
    {
        _options = options.Value;
    }

    public string Classify(Models.ConditionSnapshot snapshot, DateTime evaluatedAtUtc)
    {
        var retrievalLag = evaluatedAtUtc - snapshot.RetrievedAt;
        var forecastLag = evaluatedAtUtc - snapshot.ForecastTime;

        // A snapshot is fresh only when both the retrieval recency and the
        // forecast validity are inside the configured window. A snapshot
        // retrieved long after its forecast time is stale even if just fetched.
        return retrievalLag <= _options.FreshnessMaxAge && forecastLag <= _options.FreshnessMaxAge
            ? Models.FreshnessStatuses.Fresh
            : Models.FreshnessStatuses.Stale;
    }
}
