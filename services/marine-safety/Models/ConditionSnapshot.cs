namespace Blueverse.MarineSafety.Models;

/// <summary>Where a condition snapshot's numbers came from.</summary>
public static class ConditionSources
{
    /// <summary>Open-Meteo Weather + Marine API composite result (the v1 provider).</summary>
    public const string OpenMeteo = "Open-Meteo";
}

/// <summary>
/// Freshness classification of a condition snapshot relative to the moment it
/// was evaluated. Stale data remains visible as history but must never pass as
/// a fresh decision input. The maximum accepted age is configuration
/// (<c>MarineConditions:FreshnessMaxAgeMinutes</c>), not a hidden constant.
/// </summary>
public static class FreshnessStatuses
{
    public const string Fresh = "FRESH";
    public const string Stale = "STALE";
    public const string Unavailable = "UNAVAILABLE";
}

/// <summary>
/// A point-in-time record of marine/weather conditions for one location and
/// forecast/observation time, with provider provenance, retrieval metadata and
/// an explicit list of expected fields the provider did not supply. Missing
/// fields stay missing: they are never converted to zero or another safe value.
/// Rows are provider-derived and immutable through the public API.
/// </summary>
public class ConditionSnapshot
{
    public Guid Id { get; set; }

    /// <summary>Requested latitude in decimal degrees (WGS84), -90..90.</summary>
    public decimal Latitude { get; set; }

    /// <summary>Requested longitude in decimal degrees (WGS84), -180..180.</summary>
    public decimal Longitude { get; set; }

    /// <summary>
    /// Forecast or observation time the values apply to, normalized to UTC.
    /// Open-Meteo is requested with UTC so no local time-zone conversion is
    /// applied on write.
    /// </summary>
    public DateTime ForecastTime { get; set; }

    /// <summary>When the backend retrieved the data from the provider (UTC).</summary>
    public DateTime RetrievedAt { get; set; }

    /// <summary>Wind speed at 10 m in km/h; null when the provider did not supply it.</summary>
    public decimal? WindSpeed { get; set; }

    /// <summary>Significant wave height in metres; null when unavailable.</summary>
    public decimal? WaveHeight { get; set; }

    /// <summary>Swell wave height in metres; null when unavailable.</summary>
    public decimal? SwellHeight { get; set; }

    /// <summary>Precipitation over the previous hour in millimetres; null when unavailable.</summary>
    public decimal? Rain { get; set; }

    /// <summary>Weather condition code reported by the provider; null when unavailable.</summary>
    public int? WeatherCode { get; set; }

    /// <summary>Provider/source identity (see <see cref="ConditionSources"/>).</summary>
    public string Source { get; set; } = ConditionSources.OpenMeteo;

    /// <summary>Freshness classification at retrieval/evaluation time.</summary>
    public string FreshnessStatus { get; set; } = FreshnessStatuses.Unavailable;

    /// <summary>
    /// Ordered names of expected fields the provider did not supply
    /// (e.g. "waveHeight", "swellHeight").
    /// </summary>
    public string[] MissingFields { get; set; } = [];
}
