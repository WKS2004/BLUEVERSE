using System.Globalization;
using Blueverse.CoastalOperations.Data;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Application;

public static class OperationsTimeZones
{
    public static string Title(string? title, string objective)
    {
        var value = title is null ? objective[..Math.Min(objective.Length, 160)] : title.Trim();
        if (value.Length is < 1 or > 160)
            throw Invalid("title_invalid", "Add a title", "Use a title of 1–160 characters.");
        return value;
    }

    public static async Task<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)> ParsePeriodAsync(
        CoastalOperationsDbContext db, string? zoneId, string start, string end, CancellationToken cancellationToken)
    {
        if (zoneId is null) return OperationsValidation.ParsePeriod(start, end);
        if (!await db.TimeZoneLocations.AnyAsync(x => x.Id == zoneId && x.IsActive, cancellationToken))
            throw Invalid("time_zone_invalid", "Choose a time zone", "Select an active location from the time-zone list.");
        TimeZoneInfo zone;
        try { zone = Zone(zoneId); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new CoastalOperationsException(503, "time_zone_unavailable", "Time-zone rules are unavailable", "Try another location or contact the operations team."); }
        var first = Resolve(zone, start);
        var last = Resolve(zone, end);
        if (last <= first) throw Invalid("period_invalid", "The time period is invalid", "The end time must be later than the start time.");
        return (first, last);
    }

    public static TimeZoneInfo Zone(string id) => id == "Etc/UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id);

    public static string LocalValue(DateTimeOffset value, string? zoneId)
    {
        try { return TimeZoneInfo.ConvertTime(value, Zone(zoneId ?? "Etc/UTC")).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture); }
    }

    private static DateTimeOffset Resolve(TimeZoneInfo zone, string value)
    {
        if (!DateTime.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            throw Invalid("local_time_invalid", "Check the date and time", "Use a local date and time with the selected zone, without a typed offset.");
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) throw Invalid("local_time_nonexistent", "This local time does not exist", "Choose a time outside the daylight-saving clock change.");
        if (zone.IsAmbiguousTime(local)) throw Invalid("local_time_ambiguous", "This local time occurs twice", "Choose an unambiguous time outside the daylight-saving clock change.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
    private static CoastalOperationsException Invalid(string code, string title, string detail) => new(422, code, title, detail);
}
