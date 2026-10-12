namespace Blueverse.CoastalOperations.Application;

internal static class CoastalRecordQueries
{
    public static string? Search(string? search, int pageSize)
    {
        if (pageSize is < 1 or > 100 || search?.Length > 160)
            throw new CoastalOperationsException(422, "query_invalid", "Search filters are invalid", "Use 1–100 records per page and a search of at most 160 characters.");
        return string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
    }

    public static string? Filter(string? value, string field, params string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToUpperInvariant();
        if (!allowed.Contains(normalized))
            throw new CoastalOperationsException(422, "filter_invalid", "A search filter is invalid", $"Use a documented {field} value.");
        return normalized;
    }
}
