using System.Text.Json.Serialization;

namespace Blueverse.MarineSafety.Providers;

/// <summary>
/// Hourly variable arrays. Any array may be missing, shorter than the time
/// array, or contain nulls — all three mean "unavailable", never zero.
/// </summary>
internal sealed class OpenMeteoHourlyBlock
{
    [JsonPropertyName("time")]
    public List<string?>? Time { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public List<double?>? WindSpeed10m { get; set; }

    [JsonPropertyName("precipitation")]
    public List<double?>? Precipitation { get; set; }

    [JsonPropertyName("weather_code")]
    public List<int?>? WeatherCode { get; set; }

    [JsonPropertyName("wave_height")]
    public List<double?>? WaveHeight { get; set; }

    [JsonPropertyName("swell_wave_height")]
    public List<double?>? SwellWaveHeight { get; set; }
}

/// <summary>
/// Provider-independent result of one acquisition attempt. Values are null
/// when the provider did not supply them; the missing-field list explains why.
/// A synthetic zero is never substituted.
/// </summary>
public sealed record MarineConditionsResult(
    DateTime ForecastTimeUtc,
    DateTime RetrievedAtUtc,
    decimal? WindSpeed,
    decimal? WaveHeight,
    decimal? SwellHeight,
    decimal? Rain,
    int? WeatherCode,
    string Source,
    IReadOnlyList<string> MissingFields);

/// <summary>
/// Signals that the provider could not deliver usable data (transport,
/// timeout, rate limit, malformed or structurally invalid response). The
/// service layer converts this into an explicit unavailable outcome rather
/// than a fabricated condition.
/// </summary>
public sealed class OpenMeteoUnavailableException : Exception
{
    public string Reason { get; }

    public OpenMeteoUnavailableException(string reason, Exception? innerException = null)
        : base($"Open-Meteo data is unavailable: {reason}", innerException)
    {
        Reason = reason;
    }
}
