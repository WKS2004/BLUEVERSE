using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blueverse.MarineSafety.Tests.Unit;

/// <summary>
/// Direct adapter tests (M2-PROVIDER-*). These exercise the real
/// <see cref="OpenMeteoClient"/> over a scripted HTTP handler, pinning the G00
/// provider-seam contract: typed unavailability for deterministic provider
/// rejections, bounded retry only for transient transport failures, missing
/// fields instead of fabricated values, and UTC-normalized request windows.
/// </summary>
public sealed class OpenMeteoClientTests
{
    internal const string WeatherHost = "weather-api.test";
    internal const string MarineHost = "marine-api.test";

    private static readonly DateTime RequestTime = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);

    private static OpenMeteoClient CreateClient(StubHandler handler, OpenMeteoOptions? options = null) =>
        new(
            new TestHttpClientFactory(handler),
            Options.Create(options ?? new OpenMeteoOptions()),
            NullLogger<OpenMeteoClient>.Instance);

    private static string Times(params DateTime[] times) =>
        "[" + string.Join(",", times.Select(t => "\"" + t.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "\"")) + "]";

    private static string Num(params double?[] values) =>
        "[" + string.Join(",", values.Select(v => v?.ToString(CultureInfo.InvariantCulture) ?? "null")) + "]";

    private static string Codes(params int?[] values) =>
        "[" + string.Join(",", values.Select(v => v?.ToString(CultureInfo.InvariantCulture) ?? "null")) + "]";

    private static string Hourly(string times, string? wind, string? precip, string? code, string? wave, string? swell)
    {
        var properties = "\"time\":" + times;
        if (wind is not null)
        {
            properties += ",\"wind_speed_10m\":" + wind;
        }

        if (precip is not null)
        {
            properties += ",\"precipitation\":" + precip;
        }

        if (code is not null)
        {
            properties += ",\"weather_code\":" + code;
        }

        if (wave is not null)
        {
            properties += ",\"wave_height\":" + wave;
        }

        if (swell is not null)
        {
            properties += ",\"swell_wave_height\":" + swell;
        }

        return "{" + properties + "}";
    }

    private static string WeatherBody(string hourly) => "{\"hourly\":" + hourly + "}";

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string FullWeatherHourly() => Hourly(
        Times(RequestTime.AddHours(-1), RequestTime, RequestTime.AddHours(1)),
        Num(null, 15, 20),
        Num(0, 0.5, 1),
        Codes(1, 3, 803),
        null,
        null);

    private static string FullMarineHourly() => Hourly(
        Times(RequestTime.AddHours(-1), RequestTime, RequestTime.AddHours(1)),
        null,
        null,
        null,
        Num(null, 1.25, 2),
        Num(null, 1, 1.5));

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-001")]
    public async Task M2_PROVIDER_001_weather_http_error_is_typed_unavailable_without_retry()
    {
        var weatherCalls = 0;
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.Host == WeatherHost)
            {
                weatherCalls++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return Json(WeatherBody(FullMarineHourly()));
        });

        var client = CreateClient(handler);
        var exception = await Assert.ThrowsAsync<OpenMeteoUnavailableException>(
            () => client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None));

        Assert.Contains("HTTP 500", exception.Reason);
        Assert.Equal(1, weatherCalls); // deterministic status failures are never retried
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-002")]
    public async Task M2_PROVIDER_002_marine_outage_degrades_marine_fields_only()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == WeatherHost
            ? Json(WeatherBody(FullWeatherHourly()))
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var client = CreateClient(handler);
        var result = await client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None);

        Assert.Equal(15m, result.WindSpeed);
        Assert.Equal(0.5m, result.Rain);
        Assert.Equal(3, result.WeatherCode);
        Assert.Null(result.WaveHeight);
        Assert.Null(result.SwellHeight);
        Assert.Contains("waveHeight", result.MissingFields);
        Assert.Contains("swellHeight", result.MissingFields);
        Assert.DoesNotContain("windSpeed", result.MissingFields);
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-003")]
    public async Task M2_PROVIDER_003_malformed_weather_json_is_unavailable()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == WeatherHost
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>not json</html>", Encoding.UTF8, "application/json")
            }
            : Json(WeatherBody(FullMarineHourly())));

        var client = CreateClient(handler);
        var exception = await Assert.ThrowsAsync<OpenMeteoUnavailableException>(
            () => client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None));

        Assert.Contains("malformed JSON", exception.Reason);
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-004")]
    public async Task M2_PROVIDER_004_transient_transport_failure_is_retried_once()
    {
        var weatherCalls = 0;
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.Host != WeatherHost)
            {
                return Json(WeatherBody(FullMarineHourly()));
            }

            weatherCalls++;
            if (weatherCalls == 1)
            {
                throw new HttpRequestException("connection reset by peer");
            }

            return Json(WeatherBody(FullWeatherHourly()));
        });

        var client = CreateClient(handler);
        var result = await client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None);

        Assert.Equal(15m, result.WindSpeed);
        Assert.Equal(2, weatherCalls); // RetryCount=1 means exactly one retry
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-005")]
    public async Task M2_PROVIDER_005_per_attempt_timeout_exhausts_retries_and_reports_timeout()
    {
        var options = new OpenMeteoOptions { RequestTimeoutSeconds = 1, RetryCount = 1 };
        var handler = new StubHandler(_ => Json(WeatherBody(FullWeatherHourly())), weatherDelay: TimeSpan.FromSeconds(5));

        var client = CreateClient(handler, options);
        var exception = await Assert.ThrowsAsync<OpenMeteoUnavailableException>(
            () => client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None));

        Assert.Contains("timed out", exception.Reason);
        Assert.Equal(2, handler.Requests.Count(r => r.RequestUri!.Host == WeatherHost)); // both attempts ran, each bounded by its own timeout
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-006")]
    public async Task M2_PROVIDER_006_empty_time_series_is_unavailable()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == WeatherHost
            ? Json("{\"hourly\":{\"time\":[]}}")
            : Json(WeatherBody(FullMarineHourly())));

        var client = CreateClient(handler);
        var exception = await Assert.ThrowsAsync<OpenMeteoUnavailableException>(
            () => client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None));

        Assert.Contains("no time series", exception.Reason);
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-007")]
    public async Task M2_PROVIDER_007_absent_variables_are_reported_as_missing_never_zero()
    {
        // Weather payload without weather_code; marine payload without
        // swell_wave_height. Both absences must appear in MissingFields while
        // the supplied variables keep their values.
        var weatherHourly = Hourly(
            Times(RequestTime.AddHours(-1), RequestTime, RequestTime.AddHours(1)),
            Num(10, 12, 14),
            Num(0, 0, 0),
            null,
            null,
            null);
        var marineHourly = Hourly(
            Times(RequestTime.AddHours(-1), RequestTime, RequestTime.AddHours(1)),
            null,
            null,
            null,
            Num(1, 1, 1),
            null);

        var handler = new StubHandler(request => request.RequestUri!.Host == WeatherHost
            ? Json(WeatherBody(weatherHourly))
            : Json(WeatherBody(marineHourly)));

        var client = CreateClient(handler);
        var result = await client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None);

        Assert.Equal(12m, result.WindSpeed);
        Assert.Equal(1m, result.WaveHeight);
        Assert.Null(result.WeatherCode);
        Assert.Null(result.SwellHeight);
        Assert.Contains("weatherCode", result.MissingFields);
        Assert.Contains("swellHeight", result.MissingFields);
        Assert.DoesNotContain("windSpeed", result.MissingFields);
        Assert.DoesNotContain("waveHeight", result.MissingFields);
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-008")]
    public async Task M2_PROVIDER_008_requests_target_the_documented_endpoints_with_rounded_coordinates()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == WeatherHost
            ? Json(WeatherBody(FullWeatherHourly()))
            : Json(WeatherBody(FullMarineHourly())));

        var client = CreateClient(handler);
        await client.GetConditionsAsync(6.0254m, 80.21649m, RequestTime, CancellationToken.None);

        var weatherQuery = Uri.UnescapeDataString(
            handler.Requests.Single(r => r.RequestUri!.Host == WeatherHost).RequestUri!.Query);
        Assert.Contains("latitude=6.025", weatherQuery);
        Assert.Contains("longitude=80.216", weatherQuery);
        Assert.Contains("hourly=wind_speed_10m,precipitation,weather_code", weatherQuery);
        Assert.Contains("start_hour=2026-09-26T07:00:00", weatherQuery);
        Assert.Contains("end_hour=2026-09-26T09:00:00", weatherQuery);
        Assert.Contains("timezone=UTC", weatherQuery);
        Assert.Contains("wind_speed_unit=kmh", weatherQuery);

        var marineQuery = Uri.UnescapeDataString(
            handler.Requests.Single(r => r.RequestUri!.Host == MarineHost).RequestUri!.Query);
        Assert.Contains("latitude=6.025", marineQuery);
        Assert.Contains("longitude=80.216", marineQuery);
        Assert.Contains("hourly=wave_height,swell_wave_height", marineQuery);
        Assert.Contains("start_hour=2026-09-26T07:00:00", marineQuery);
        Assert.Contains("timezone=UTC", marineQuery);
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-009")]
    public async Task M2_PROVIDER_009_out_of_range_coordinates_are_rejected_before_any_request()
    {
        var handler = new StubHandler(_ => Json("{}"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.GetConditionsAsync(91m, 80m, RequestTime, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.GetConditionsAsync(6m, 181m, RequestTime, CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-010")]
    public async Task M2_PROVIDER_010_values_are_aligned_to_the_forecast_hour_and_rounded()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == WeatherHost
            ? Json(WeatherBody(Hourly(
                Times(RequestTime.AddHours(-1), RequestTime, RequestTime.AddHours(1)),
                Num(null, 14.567, 20),
                Num(0, 0.5, 1),
                Codes(1, 3, 803),
                null,
                null)))
            : Json(WeatherBody(Hourly(
                Times(RequestTime.AddHours(-1), RequestTime, RequestTime.AddHours(1)),
                null,
                null,
                null,
                Num(null, 1.234, 2),
                Num(null, 0.987, 1.5)))));

        var client = CreateClient(handler);
        var result = await client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None);

        Assert.Equal(RequestTime, result.ForecastTimeUtc);
        Assert.Equal(14.57m, result.WindSpeed);
        Assert.Equal(0.5m, result.Rain);
        Assert.Equal(3, result.WeatherCode);
        Assert.Equal(1.23m, result.WaveHeight);
        Assert.Equal(0.99m, result.SwellHeight);
        Assert.Empty(result.MissingFields);
        Assert.True(
            (result.RetrievedAtUtc - DateTime.UtcNow).Duration() < TimeSpan.FromMinutes(1),
            "RetrievedAtUtc must be the acquisition moment in UTC");
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-011")]
    public async Task M2_PROVIDER_011_missing_requested_hour_falls_forward_to_the_next_available_hour()
    {
        // Only 07:00 and 09:00 are supplied for an 08:00 request: the adapter
        // resolves the first hourly stamp at or after the 30-minute window and
        // extracts every variable from that same index.
        var hours = Times(RequestTime.AddHours(-1), RequestTime.AddHours(1));
        var handler = new StubHandler(request => request.RequestUri!.Host == WeatherHost
            ? Json(WeatherBody(Hourly(hours, Num(10, 20), Num(0, 2), Codes(1, 2), null, null)))
            : Json(WeatherBody(Hourly(hours, null, null, null, Num(0.5, 1.5), Num(0.4, 1.4)))));

        var client = CreateClient(handler);
        var result = await client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None);

        Assert.Equal(RequestTime.AddHours(1), result.ForecastTimeUtc);
        Assert.Equal(20m, result.WindSpeed);
        Assert.Equal(2m, result.Rain);
        Assert.Equal(2, result.WeatherCode);
        Assert.Equal(1.5m, result.WaveHeight);
        Assert.Equal(1.4m, result.SwellHeight);
        Assert.Empty(result.MissingFields);
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-012")]
    public async Task M2_PROVIDER_012_negative_values_are_rejected_as_missing()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host == WeatherHost
            ? Json(WeatherBody(Hourly(
                Times(RequestTime.AddHours(-1), RequestTime, RequestTime.AddHours(1)),
                Num(15, -5, 15),
                Num(0, 0.5, 1),
                Codes(1, 3, 803),
                null,
                null)))
            : Json(WeatherBody(FullMarineHourly())));

        var client = CreateClient(handler);
        var result = await client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None);

        Assert.Null(result.WindSpeed);
        Assert.Equal(1.25m, result.WaveHeight);
        Assert.Contains("windSpeed", result.MissingFields);
        Assert.DoesNotContain("waveHeight", result.MissingFields);
        Assert.DoesNotContain("swellHeight", result.MissingFields);
    }

    [Fact]
    [Trait("CaseId", "M2-PROVIDER-013")]
    public async Task M2_PROVIDER_013_rate_limiting_is_deterministic_and_not_retried()
    {
        var weatherCalls = 0;
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.Host == WeatherHost)
            {
                weatherCalls++;
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }

            return Json(WeatherBody(FullMarineHourly()));
        });

        var client = CreateClient(handler);
        var exception = await Assert.ThrowsAsync<OpenMeteoUnavailableException>(
            () => client.GetConditionsAsync(6.025m, 80.216m, RequestTime, CancellationToken.None));

        Assert.Contains("rate limited", exception.Reason);
        Assert.Equal(1, weatherCalls);
    }
}

/// <summary>
/// Scriptable message handler capturing every request so tests can assert the
/// exact provider URLs the adapter builds. The optional weather delay simulates
/// a slow provider for per-attempt timeout testing.
/// </summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    private readonly TimeSpan? _weatherDelay;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder, TimeSpan? weatherDelay = null)
    {
        _responder = responder;
        _weatherDelay = weatherDelay;
    }

    public List<HttpRequestMessage> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_weatherDelay.HasValue && request.RequestUri!.Host == OpenMeteoClientTests.WeatherHost)
        {
            await Task.Delay(_weatherDelay.Value, cancellationToken);
        }

        return _responder(request);
    }
}

/// <summary>
/// Returns clients wired to the shared stub handler, with separate base
/// addresses for the weather and marine datasets exactly like production.
/// </summary>
public sealed class TestHttpClientFactory(StubHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        var client = new HttpClient(handler);
        client.BaseAddress = name == "OpenMeteoMarine"
            ? new Uri("https://marine-api.test/")
            : new Uri("https://weather-api.test/");
        return client;
    }
}
