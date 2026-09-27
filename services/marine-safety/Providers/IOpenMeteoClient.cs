namespace Blueverse.MarineSafety.Providers;

/// <summary>
/// Backend-mediated Open-Meteo access boundary. The frontend never calls the
/// provider; only this adapter does. Implementations must validate response
/// shape, ranges and timestamps and report every failure mode as either a
/// <see cref="MarineConditionsResult"/> with missing fields or an
/// <see cref="OpenMeteoUnavailableException"/> — never as a guessed value.
/// </summary>
public interface IOpenMeteoClient
{
    /// <summary>
    /// Acquire the weather + marine variables relevant to BLUEVERSE workflows
    /// for one location and UTC hour.
    /// </summary>
    Task<MarineConditionsResult> GetConditionsAsync(
        decimal latitude,
        decimal longitude,
        DateTime timeUtc,
        CancellationToken cancellationToken);
}
