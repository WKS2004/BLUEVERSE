using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Blueverse.CoastalOperations.Contracts;

namespace Blueverse.CoastalOperations.Application;

public static partial class OperationsValidation
{
    private static readonly HashSet<string> TargetTypes = ["DESTINATION", "ACTIVITY", "OFFERING", "SESSION"];
    private static readonly HashSet<string> AlertSeverities = ["LOW", "MODERATE", "HIGH", "CRITICAL"];
    private static readonly HashSet<string> AlertVisibilities = ["OPERATIONS", "PUBLIC"];

    public static string NormalizeTargetType(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null || !TargetTypes.Contains(normalized))
        {
            throw Invalid("target_type_invalid", "Target type is invalid", "Use DESTINATION, ACTIVITY, OFFERING or SESSION.");
        }

        return normalized;
    }

    public static string NormalizeSeverity(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null || !AlertSeverities.Contains(normalized))
        {
            throw Invalid("alert_severity_invalid", "Alert severity is invalid", "Use LOW, MODERATE, HIGH or CRITICAL.");
        }

        return normalized;
    }

    public static string NormalizeAlertVisibility(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null || !AlertVisibilities.Contains(normalized))
        {
            throw Invalid("alert_visibility_invalid", "Alert visibility is invalid", "Use OPERATIONS or PUBLIC.");
        }

        return normalized;
    }

    public static (DateTimeOffset StartsAt, DateTimeOffset EndsAt) ParsePeriod(string? start, string? end)
    {
        if (!HasExplicitOffset(start) || !HasExplicitOffset(end) ||
            !DateTimeOffset.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startsAt) ||
            !DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endsAt))
        {
            throw Invalid("period_offset_required", "An explicit time-zone offset is required", "Provide RFC 3339 timestamps with Z or an explicit UTC offset.");
        }

        startsAt = startsAt.ToUniversalTime();
        endsAt = endsAt.ToUniversalTime();
        if (endsAt <= startsAt)
        {
            throw Invalid("period_invalid", "The time period is invalid", "The end time must be later than the start time.");
        }

        return (startsAt, endsAt);
    }

    public static bool IsAllowedTransition(string targetType, string currentState, string nextState)
    {
        if (targetType == "SESSION")
        {
            return currentState is "OPEN" or "CAUTION" or "TEMPORARILY_SUSPENDED" &&
                   nextState is "CANCELLED" or "COMPLETED" ||
                   targetType == "SESSION" && BaseTransition(currentState, nextState);
        }

        return BaseTransition(currentState, nextState);
    }

    public static bool IsHighImpactState(string targetType, string state) =>
        state == "TEMPORARILY_SUSPENDED" || (targetType == "SESSION" && state == "CANCELLED");

    public static string RequestDigest<T>(T request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    public static string NormalizeDecision(string? decision, params string[] allowed)
    {
        var normalized = decision?.Trim().ToUpperInvariant();
        if (normalized is null || !allowed.Contains(normalized, StringComparer.Ordinal))
        {
            throw Invalid("decision_invalid", "The decision is invalid", $"Use one of: {string.Join(", ", allowed)}.");
        }

        return normalized;
    }

    public static string ValidateIdempotencyKey(string? key)
    {
        var normalized = key?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 128 || !IdempotencyKeyPattern().IsMatch(normalized))
        {
            throw Invalid("idempotency_key_invalid", "A valid Idempotency-Key is required", "Use 1–128 letters, digits, dots, underscores, colons or hyphens.");
        }

        return normalized;
    }

    public static bool TryReadCursor(string? cursor, out Guid id)
    {
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return Guid.TryParseExact(decoded, "N", out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string EncodeCursor(Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString("N")));

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static CoastalOperationsException Invalid(string code, string title, string detail) =>
        new(StatusCodes.Status422UnprocessableEntity, code, title, detail);

    private static bool BaseTransition(string currentState, string nextState) =>
        (currentState, nextState) is
            ("OPEN", "CAUTION") or
            ("OPEN", "TEMPORARILY_SUSPENDED") or
            ("CAUTION", "OPEN") or
            ("CAUTION", "TEMPORARILY_SUSPENDED") or
            ("TEMPORARILY_SUSPENDED", "OPEN") or
            ("TEMPORARILY_SUSPENDED", "CAUTION");

    private static bool HasExplicitOffset(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ExplicitOffsetPattern().IsMatch(value);

    [GeneratedRegex(@"(?:Z|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffsetPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._:-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdempotencyKeyPattern();
}
