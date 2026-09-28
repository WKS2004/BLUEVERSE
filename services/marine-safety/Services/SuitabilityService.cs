using Microsoft.EntityFrameworkCore;
using Blueverse.MarineSafety.Data;
using Blueverse.MarineSafety.Dtos;
using Blueverse.MarineSafety.Models;
using Blueverse.MarineSafety.Providers;

namespace Blueverse.MarineSafety.Services;

/// <summary>
/// The Member 2 business operation: a deterministic, server-owned evaluation
/// of whether an activity is environmentally suitable for a location and time.
/// The rule engine is ordinary application logic — no AI participates in the
/// decision, and no provider content can alter a threshold.
///
/// Evaluation order is deliberate:
///   1. request validation and activity existence;
///   2. a configured safety profile must exist (missing profile is a
///      validation error, never an implicit "safe");
///   3. condition evidence must be fresh (stale evidence cannot support a
///      positive classification);
///   4. every required factor must be present (a missing factor yields
///      UNKNOWN — it is never converted to a safe value);
///   5. only then are deterministic limits applied.
/// </summary>
public sealed class SuitabilityService : ISuitabilityService
{
    private readonly MarineSafetyDbContext _db;
    private readonly IConditionService _conditions;
    private readonly IFreshnessPolicy _freshnessPolicy;
    private readonly ILogger<SuitabilityService> _logger;

    public SuitabilityService(
        MarineSafetyDbContext db,
        IConditionService conditions,
        IFreshnessPolicy freshnessPolicy,
        ILogger<SuitabilityService> logger)
    {
        _db = db;
        _conditions = conditions;
        _freshnessPolicy = freshnessPolicy;
        _logger = logger;
    }

    public async Task<SuitabilityResultDto> EvaluateAsync(EvaluateSuitabilityDto request, CancellationToken cancellationToken)
    {
        var activity = await _db.MarineActivities
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == request.ActivityId, cancellationToken);

        if (activity is null || !activity.IsActive)
        {
            throw new SuitabilityValidationException(
                "activity",
                $"Activity {request.ActivityId} does not exist or is not active.");
        }

        var profile = await _db.SafetyProfiles
            .AsNoTracking()
            .Where(p => p.ActivityId == activity.Id && p.IsActive)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            throw new SuitabilityValidationException(
                "activityId",
                $"No active safety profile is configured for activity '{activity.Name}'.");
        }

        var requestedTime = MarineTime.ToUtc(request.DateTime ?? DateTime.UtcNow);
        var conditions = await _conditions.GetConditionsAsync(
            request.Latitude, request.Longitude, requestedTime, cancellationToken);
        var evaluatedAt = DateTime.UtcNow;
        var freshness = _freshnessPolicy.Classify(conditions, evaluatedAt);

        var violations = new List<string>();
        var cautionFactors = new List<string>();
        var missing = new List<string>(conditions.MissingFields);
        string status;

        if (freshness != FreshnessStatuses.Fresh)
        {
            // Stale evidence cannot silently pass as current.
            status = SuitabilityResults.Unknown;
            missing.Add("freshConditions");
        }
        else
        {
            status = EvaluateFactors(profile, conditions, violations, cautionFactors);
        }

        var record = new SuitabilityAssessmentRecord
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            ProfileVersion = profile.Version,
            Latitude = Math.Round(request.Latitude, 3, MidpointRounding.AwayFromZero),
            Longitude = Math.Round(request.Longitude, 3, MidpointRounding.AwayFromZero),
            RequestedTime = requestedTime,
            EvaluatedAt = evaluatedAt,
            Result = status,
            Violations = [.. violations],
            CautionFactors = [.. cautionFactors],
            MissingFields = [.. missing],
            Source = conditions.Source,
            FreshnessStatus = freshness,
            ConditionSnapshotId = conditions.Id,
            SafetyProfileId = profile.Id,
            CreatedAt = evaluatedAt
        };

        _db.SuitabilityAssessments.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Suitability for activity {Activity} at {Latitude},{Longitude} ({RequestedTime:O}): {Status} (violations: {Violations}, caution: {Caution})",
            activity.Name, record.Latitude, record.Longitude, requestedTime, status,
            string.Join(",", violations), string.Join(",", cautionFactors));

        return new SuitabilityResultDto(
            status,
            activity.Id,
            activity.Name,
            new LocationDto(record.Latitude, record.Longitude),
            requestedTime,
            evaluatedAt,
            new ConditionsDto(
                conditions.WindSpeed,
                conditions.WaveHeight,
                conditions.SwellHeight,
                conditions.Rain,
                conditions.WeatherCode),
            conditions.Source,
            conditions.RetrievedAt,
            freshness,
            missing,
            violations,
            cautionFactors,
            record.Id,
            conditions.Id);
    }

    /// <summary>
    /// Read-only history over the persisted assessments. Newest first with a
    /// bounded page (the same shape as condition history), so an unfiltered
    /// caller cannot walk an unbounded result. The from/to window filters on
    /// the requested (forecast) time — the domain-meaningful axis — not the
    /// wall-clock moment the evaluation ran. Every row keeps the evidence and
    /// profile-version references that explain the stored result.
    /// </summary>
    public async Task<IReadOnlyList<AssessmentHistoryDto>> GetAssessmentsAsync(
        Guid? activityId,
        string? result,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var query = _db.SuitabilityAssessments.AsNoTracking().AsQueryable();

        if (activityId.HasValue)
        {
            query = query.Where(r => r.ActivityId == activityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            var normalized = result.Trim().ToUpperInvariant();
            query = query.Where(r => r.Result == normalized);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(r => r.RequestedTime >= MarineTime.ToUtc(fromUtc.Value));
        }

        if (toUtc.HasValue)
        {
            query = query.Where(r => r.RequestedTime <= MarineTime.ToUtc(toUtc.Value));
        }

        var records = await query
            .OrderByDescending(r => r.EvaluatedAt)
            .ThenByDescending(r => r.Id)
            .Take(200)
            .Include(r => r.Activity)
            .ToListAsync(cancellationToken);

        return records.Select(ToHistoryDto).ToList();
    }

    public Task<AssessmentHistoryDto?> GetAssessmentAsync(Guid id, CancellationToken cancellationToken) =>
        _db.SuitabilityAssessments
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Include(r => r.Activity)
            .Select(r => ToHistoryDto(r))
            .SingleOrDefaultAsync(cancellationToken);

    private static AssessmentHistoryDto ToHistoryDto(SuitabilityAssessmentRecord record) => new(
        record.Id,
        record.ActivityId,
        record.Activity?.Name ?? string.Empty,
        record.ConditionSnapshotId,
        record.SafetyProfileId,
        record.ProfileVersion,
        record.Latitude,
        record.Longitude,
        record.RequestedTime,
        record.EvaluatedAt,
        record.Result,
        record.Violations,
        record.CautionFactors,
        record.MissingFields,
        record.Source,
        record.FreshnessStatus,
        record.CreatedAt);

    private static string EvaluateFactors(
        SafetyProfile profile,
        ConditionSnapshot conditions,
        List<string> violations,
        List<string> cautionFactors)
    {
        // Any unavailable required factor makes the whole assessment UNKNOWN:
        // absent evidence must not default to safe.
        if (conditions.WindSpeed is null || conditions.WaveHeight is null || conditions.SwellHeight is null)
        {
            return SuitabilityResults.Unknown;
        }

        if (conditions.WindSpeed > profile.MaxWindSpeed)
        {
            violations.Add($"windSpeed {conditions.WindSpeed:0.##} km/h exceeds maximum {profile.MaxWindSpeed:0.##} km/h");
        }
        else if (profile.CautionWindSpeed.HasValue && conditions.WindSpeed > profile.CautionWindSpeed)
        {
            cautionFactors.Add("windSpeed");
        }

        if (conditions.WaveHeight > profile.MaxWaveHeight)
        {
            violations.Add($"waveHeight {conditions.WaveHeight:0.##} m exceeds maximum {profile.MaxWaveHeight:0.##} m");
        }
        else if (profile.CautionWaveHeight.HasValue && conditions.WaveHeight > profile.CautionWaveHeight)
        {
            cautionFactors.Add("waveHeight");
        }

        if (conditions.SwellHeight > profile.MaxSwellHeight)
        {
            violations.Add($"swellHeight {conditions.SwellHeight:0.##} m exceeds maximum {profile.MaxSwellHeight:0.##} m");
        }
        else if (profile.CautionSwellHeight.HasValue && conditions.SwellHeight > profile.CautionSwellHeight)
        {
            cautionFactors.Add("swellHeight");
        }

        if (violations.Count > 0)
        {
            return SuitabilityResults.Unsuitable;
        }

        return cautionFactors.Count > 0 ? SuitabilityResults.Caution : SuitabilityResults.Suitable;
    }
}

/// <summary>Request or configuration problem that maps to a 4xx response.</summary>
public sealed class SuitabilityValidationException : Exception
{
    public string Field { get; }

    public SuitabilityValidationException(string field, string message) : base(message)
    {
        Field = field;
    }
}
