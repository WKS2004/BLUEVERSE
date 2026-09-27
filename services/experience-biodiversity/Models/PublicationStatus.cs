namespace Blueverse.ExperienceBiodiversity.Models;

public static class PublicationStatus
{
    public const string Draft = "DRAFT";
    public const string Published = "PUBLISHED";
    public const string Archived = "ARCHIVED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft,
        Published,
        Archived
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());
}
