namespace Blueverse.MarineSafety.Tests.Fixtures;

/// <summary>
/// Deterministic transport double for the Open-Meteo adapter. It returns the
/// configured result or throws the configured failure; tests assert how the
/// service reacts, never that the double itself is the provider.
/// </summary>
public sealed class StubOpenMeteoClient : IOpenMeteoClient
{
    private readonly Func<MarineConditionsResult> _resultFactory;
    private readonly Func<OpenMeteoUnavailableException?> _failureFactory;

    public int CallCount { get; private set; }

    public StubOpenMeteoClient(
        Func<MarineConditionsResult>? resultFactory = null,
        Func<OpenMeteoUnavailableException?>? failureFactory = null)
    {
        _resultFactory = resultFactory ?? DefaultResult;
        _failureFactory = failureFactory ?? (() => null);
    }

    public static MarineConditionsResult DefaultResult() => new(
        DateTime.UtcNow,
        DateTime.UtcNow,
        WindSpeed: 15m,
        WaveHeight: 0.8m,
        SwellHeight: 0.7m,
        Rain: 0m,
        WeatherCode: 0,
        Source: ConditionSources.OpenMeteo,
        MissingFields: []);

    public Task<MarineConditionsResult> GetConditionsAsync(
        decimal latitude,
        decimal longitude,
        DateTime timeUtc,
        CancellationToken cancellationToken)
    {
        CallCount++;
        var failure = _failureFactory();
        if (failure is not null)
        {
            throw failure;
        }

        return Task.FromResult(_resultFactory());
    }

    public static StubOpenMeteoClient Unavailable(string reason = "provider is unreachable") =>
        new(failureFactory: () => new OpenMeteoUnavailableException(reason));

    public static StubOpenMeteoClient RateLimited() =>
        new(failureFactory: () => new OpenMeteoUnavailableException("rate limited by the provider"));
}
