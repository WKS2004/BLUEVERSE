using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Blueverse.MarineSafety.Models;

namespace Blueverse.MarineSafety.Providers;

/// <summary>
/// HTTP adapter for the Open-Meteo Weather and Marine APIs. Handles HTTP
/// errors, timeouts, unavailability, rate limiting, malformed JSON, unexpected
/// structure, missing variables and out-of-range values by surfacing them as
/// explicit missing fields or a typed unavailable exception. It never guesses
/// a value and never lets provider content change application state.
/// </summary>
public sealed class OpenMeteoClient : IOpenMeteoClient
{
    private const string Source = ConditionSources.OpenMeteo;

    private readonly HttpClient _weatherHttpClient;
    private readonly HttpClient _marineHttpClient;
    private readonly OpenMeteoOptions _options;
    private readonly ILogger<OpenMeteoClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OpenMeteoClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenMeteoOptions> options,
        ILogger<OpenMeteoClient> logger)
    {
        _options = options.Value;
        _weatherHttpClient = httpClientFactory.CreateClient("OpenMeteoWeather");
        _marineHttpClient = httpClientFactory.CreateClient("OpenMeteoMarine");
        _logger = logger;
    }

    public async Task<MarineConditionsResult> GetConditionsAsync(
        decimal latitude,
        decimal longitude,
        DateTime timeUtc,
        CancellationToken cancellationToken)
    {
        var time = timeUtc.ToUniversalTime();
        var start = time.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var end = time.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var normalizedLatitude = Normalize(latitude, "latitude", -90, 90);
        var normalizedLongitude = Normalize(longitude, "longitude", -180, 180);

        var weatherTask = FetchHourlyAsync(
            _weatherHttpClient,
            BuildWeatherUrl(normalizedLatitude, normalizedLongitude, start, end),
            "weather",
            cancellationToken);
        var marineTask = FetchHourlyAsync(
            _marineHttpClient,
            BuildMarineUrl(normalizedLatitude, normalizedLongitude, start, end),
            "marine",
            cancellationToken);

        // Weather and Marine are independent sources (G00 §provider seam):
        // a marine outage degrades only marine fields instead of discarding
        // the weather evidence. Weather failure still surfaces unavailable.
        OpenMeteoHourlyBlock? weather;
        OpenMeteoHourlyBlock? marine;
        try
        {
            weather = await weatherTask;
        }
        catch (OpenMeteoUnavailableException)
        {
            // Weather evidence is required for a usable snapshot; without it
            // the acquisition is unavailable as a whole.
            throw;
        }

        try
        {
            marine = await marineTask;
        }
        catch (OpenMeteoUnavailableException exception)
        {
            marine = null;
            _logger.LogWarning(
                "Open-Meteo Marine dataset unavailable ({Reason}); marine fields are reported as missing.",
                exception.Reason);
        }

        var forecastTime = ResolveForecastTime(weather, marine, time);
        var wind = ExtractSeries(weather.WindSpeed10m, weather.Time, forecastTime);
        var rain = ExtractSeries(weather.Precipitation, weather.Time, forecastTime);
        var weatherCode = ExtractIntSeries(weather.WeatherCode, weather.Time, forecastTime);
        var waveHeight = marine is null ? null : ExtractSeries(marine.WaveHeight, marine.Time, forecastTime);
        var swellHeight = marine is null ? null : ExtractSeries(marine.SwellWaveHeight, marine.Time, forecastTime);

        var missingFields = new List<string>();
        if (wind is null)
        {
            missingFields.Add("windSpeed");
        }

        if (marine is null)
        {
            missingFields.Add("waveHeight");
            missingFields.Add("swellHeight");
        }
        else
        {
            if (waveHeight is null)
            {
                missingFields.Add("waveHeight");
            }

            if (swellHeight is null)
            {
                missingFields.Add("swellHeight");
            }
        }

        if (rain is null)
        {
            missingFields.Add("rain");
        }

        _logger.LogInformation(
            "Open-Meteo acquisition for {Latitude},{Longitude} at {ForecastTime:O} returned wind={HasWind}, wave={HasWave}, swell={HasSwell}, missing={MissingFields}",
            latitude, longitude, forecastTime, wind is not null, waveHeight is not null, swellHeight is not null, string.Join(",", missingFields));

        return new MarineConditionsResult(
            forecastTime,
            DateTime.UtcNow,
            wind,
            waveHeight,
            swellHeight,
            rain,
            weatherCode,
            Source,
            missingFields);
    }

    private static decimal Normalize(decimal value, string name, decimal min, decimal max)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} is outside the valid range [{min}, {max}].");
        }

        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private static string BuildWeatherUrl(decimal latitude, decimal longitude, string start, string end) =>
        $"v1/forecast?latitude={latitude.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}" +
        $"&longitude={longitude.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}" +
        "&hourly=wind_speed_10m,precipitation,weather_code" +
        $"&start_hour={Uri.EscapeDataString(start)}&end_hour={Uri.EscapeDataString(end)}" +
        "&timezone=UTC&wind_speed_unit=kmh";

    private static string BuildMarineUrl(decimal latitude, decimal longitude, string start, string end) =>
        $"v1/marine?latitude={latitude.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}" +
        $"&longitude={longitude.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}" +
        "&hourly=wave_height,swell_wave_height" +
        $"&start_hour={Uri.EscapeDataString(start)}&end_hour={Uri.EscapeDataString(end)}" +
        "&timezone=UTC";

    private async Task<OpenMeteoHourlyBlock> FetchHourlyAsync(
        HttpClient client,
        string relativeUrl,
        string datasetName,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, _options.RetryCount + 1);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.RequestTimeout);

                using var response = await client.GetAsync(relativeUrl, timeoutCts.Token);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new OpenMeteoUnavailableException("rate limited by the provider");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new OpenMeteoUnavailableException($"provider returned HTTP {(int)response.StatusCode}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                OpenMeteoHourlyBlock? hourly;
                try
                {
                    var document = await JsonSerializer.DeserializeAsync<JsonDocument>(
                        stream,
                        JsonOptions,
                        timeoutCts.Token);
                    if (document is null)
                    {
                        throw new OpenMeteoUnavailableException("provider returned an empty body");
                    }

                    hourly = document.RootElement.TryGetProperty("hourly", out var hourlyElement)
                        ? hourlyElement.Deserialize<OpenMeteoHourlyBlock>(JsonOptions)
                        : null;
                }
                catch (JsonException)
                {
                    throw new OpenMeteoUnavailableException("provider returned malformed JSON");
                }

                if (hourly?.Time is null || hourly.Time.Count == 0)
                {
                    throw new OpenMeteoUnavailableException($"provider {datasetName} response has no time series");
                }

                return hourly;
            }
            catch (OpenMeteoUnavailableException)
            {
                // Deterministic provider rejections (rate limit, HTTP status,
                // malformed body) are not transient: do not retry them.
                throw;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException or OperationCanceledException
                && !cancellationToken.IsCancellationRequested)
            {
                lastError = exception;
                _logger.LogWarning(
                    exception,
                    "Open-Meteo {Dataset} request attempt {Attempt}/{Attempts} failed.",
                    datasetName,
                    attempt,
                    attempts);
            }
        }

        throw new OpenMeteoUnavailableException(
            lastError is TaskCanceledException ? "provider request timed out" : "provider is unreachable",
            lastError);
    }

    private static DateTime ResolveForecastTime(
        OpenMeteoHourlyBlock weather,
        OpenMeteoHourlyBlock? marine,
        DateTime requested)
    {
        var series = weather.Time is { Count: > 0 } weatherTime
            ? weatherTime
            : marine?.Time;
        if (series is null || series.Count == 0)
        {
            return requested;
        }

        // Open-Meteo hourly timestamps are naive local strings; because the
        // request pinned timezone=UTC they are UTC values.
        foreach (var candidate in series)
        {
            if (candidate is not null &&
                DateTime.TryParse(
                    candidate,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var parsed) &&
                parsed >= requested.AddMinutes(-30))
        {
                return parsed;
            }
        }

        return requested;
    }

    private static decimal? ExtractSeries(List<double?>? values, List<string?>? times, DateTime forecastTime)
    {
        var raw = ExtractRaw(values, times, forecastTime);
        if (raw is null)
        {
            return null;
        }

        // Reject non-finite and negative values: they are malformed evidence,
        // not zero. Environmental values in scope are all non-negative.
        if (!double.IsFinite(raw.Value) || raw.Value < 0)
        {
            return null;
        }

        return Math.Round((decimal)raw.Value, 2, MidpointRounding.AwayFromZero);
    }

    private static int? ExtractIntSeries(List<int?>? values, List<string?>? times, DateTime forecastTime)
    {
        if (values is null || times is null || values.Count == 0)
        {
            return null;
        }

        var index = FindHourIndex(times, forecastTime);
        if (index < 0 || index >= values.Count || values[index] is null)
        {
            return null;
        }

        return (int?)Math.Round((double)values[index]!.Value, System.MidpointRounding.AwayFromZero);
    }

    private static double? ExtractRaw(List<double?>? values, List<string?>? times, DateTime forecastTime)
    {
        if (values is null || times is null || values.Count == 0)
        {
            return null;
        }

        var index = FindHourIndex(times, forecastTime);
        if (index < 0 || index >= values.Count)
        {
            return null;
        }

        return values[index];
    }

    private static int FindHourIndex(List<string?> times, DateTime forecastTime)
    {
        for (var index = 0; index < times.Count; index++)
        {
            if (times[index] is not null &&
                DateTime.TryParse(
                    times[index],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var parsed) &&
                parsed == forecastTime)
            {
                return index;
            }
        }

        return -1;
    }
}
