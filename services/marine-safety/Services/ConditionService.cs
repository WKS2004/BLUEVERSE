using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Blueverse.MarineSafety.Data;
using Blueverse.MarineSafety.Models;
using Blueverse.MarineSafety.Providers;

namespace Blueverse.MarineSafety.Services;

/// <summary>
/// Owns the condition snapshot lifecycle: reuse a fresh stored snapshot when
/// one exists for the rounded location and hour, otherwise acquire through the
/// Open-Meteo adapter, validate and persist the result. Provider failure is a
/// safe, explicit outcome — never a fabricated condition.
/// </summary>
public sealed class ConditionService : IConditionService
{
    private static readonly TimeSpan SnapshotReuseWindow = TimeSpan.FromMinutes(15);

    private readonly MarineSafetyDbContext _db;
    private readonly IOpenMeteoClient _openMeteo;
    private readonly IFreshnessPolicy _freshnessPolicy;
    private readonly OpenMeteoOptions _options;
    private readonly ILogger<ConditionService> _logger;

    public ConditionService(
        MarineSafetyDbContext db,
        IOpenMeteoClient openMeteo,
        IFreshnessPolicy freshnessPolicy,
        IOptions<OpenMeteoOptions> options,
        ILogger<ConditionService> logger)
    {
        _db = db;
        _openMeteo = openMeteo;
        _freshnessPolicy = freshnessPolicy;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ConditionSnapshot> GetConditionsAsync(
        decimal latitude,
        decimal longitude,
        DateTime? timeUtc,
        CancellationToken cancellationToken)
    {
        var requestedTime = MarineTime.ToUtc(timeUtc ?? DateTime.UtcNow);
        var snapshot = await FindReusableSnapshotAsync(latitude, longitude, requestedTime, cancellationToken);
        if (snapshot is not null)
        {
            snapshot.FreshnessStatus = _freshnessPolicy.Classify(snapshot, DateTime.UtcNow);
            return snapshot;
        }

        MarineConditionsResult acquired;
        try
        {
            acquired = await _openMeteo.GetConditionsAsync(latitude, longitude, requestedTime, cancellationToken);
        }
        catch (OpenMeteoUnavailableException exception)
        {
            _logger.LogWarning("Condition acquisition failed safely: {Reason}", exception.Reason);
            throw new ConditionsUnavailableException(exception.Reason, exception);
        }

        var stored = new ConditionSnapshot
        {
            Id = Guid.NewGuid(),
            Latitude = Math.Round(latitude, _options.CoordinatePrecision, MidpointRounding.AwayFromZero),
            Longitude = Math.Round(longitude, _options.CoordinatePrecision, MidpointRounding.AwayFromZero),
            ForecastTime = acquired.ForecastTimeUtc,
            RetrievedAt = acquired.RetrievedAtUtc,
            WindSpeed = acquired.WindSpeed,
            WaveHeight = acquired.WaveHeight,
            SwellHeight = acquired.SwellHeight,
            Rain = acquired.Rain,
            WeatherCode = acquired.WeatherCode,
            Source = acquired.Source,
            MissingFields = [.. acquired.MissingFields]
        };
        stored.FreshnessStatus = _freshnessPolicy.Classify(stored, DateTime.UtcNow);

        _db.ConditionSnapshots.Add(stored);
        await _db.SaveChangesAsync(cancellationToken);
        return stored;
    }

    private async Task<ConditionSnapshot?> FindReusableSnapshotAsync(
        decimal latitude,
        decimal longitude,
        DateTime requestedTime,
        CancellationToken cancellationToken)
    {
        var lat = Math.Round(latitude, _options.CoordinatePrecision, MidpointRounding.AwayFromZero);
        var lon = Math.Round(longitude, _options.CoordinatePrecision, MidpointRounding.AwayFromZero);
        var newestAllowed = requestedTime + SnapshotReuseWindow;

        var candidates = await _db.ConditionSnapshots
            .AsNoTracking()
            .Where(s => s.Latitude == lat && s.Longitude == lon && s.ForecastTime <= newestAllowed)
            .OrderByDescending(s => s.ForecastTime)
            .Take(1)
            .ToListAsync(cancellationToken);

        var candidate = candidates.FirstOrDefault();
        if (candidate is null)
        {
            return null;
        }

        // Reuse only when the snapshot represents the same requested hour and
        // its own retrieval happened recently enough to count as fresh.
        var sameHour = Math.Abs((candidate.ForecastTime - requestedTime).TotalMinutes) <= 60;
        var recentlyRetrieved = DateTime.UtcNow - candidate.RetrievedAt <= SnapshotReuseWindow;
        return sameHour && recentlyRetrieved ? candidate : null;
    }

    public async Task<ConditionSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await _db.ConditionSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (snapshot is not null)
        {
            snapshot.FreshnessStatus = _freshnessPolicy.Classify(snapshot, DateTime.UtcNow);
        }
        return snapshot;
    }

    public async Task<IReadOnlyList<ConditionSnapshot>> GetHistoryAsync(
        decimal? latitude,
        decimal? longitude,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var query = _db.ConditionSnapshots.AsNoTracking().AsQueryable();

        if (latitude.HasValue)
        {
            var lat = Math.Round(latitude.Value, _options.CoordinatePrecision, MidpointRounding.AwayFromZero);
            query = query.Where(s => s.Latitude == lat);
        }

        if (longitude.HasValue)
        {
            var lon = Math.Round(longitude.Value, _options.CoordinatePrecision, MidpointRounding.AwayFromZero);
            query = query.Where(s => s.Longitude == lon);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(s => s.ForecastTime >= MarineTime.ToUtc(fromUtc.Value));
        }

        if (toUtc.HasValue)
        {
            query = query.Where(s => s.ForecastTime <= MarineTime.ToUtc(toUtc.Value));
        }

        var results = await query
            .OrderByDescending(s => s.ForecastTime)
            .ThenByDescending(s => s.RetrievedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var evaluatedAt = DateTime.UtcNow;
        foreach (var snapshot in results)
        {
            snapshot.FreshnessStatus = _freshnessPolicy.Classify(snapshot, evaluatedAt);
        }

        return results;
    }
}

/// <summary>Signals that no usable condition data could be obtained for a request.</summary>
public sealed class ConditionsUnavailableException : Exception
{
    public ConditionsUnavailableException(string reason, Exception? innerException = null)
        : base($"Marine conditions are unavailable: {reason}", innerException)
    {
    }
}
