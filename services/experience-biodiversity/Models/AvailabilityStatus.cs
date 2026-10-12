namespace Blueverse.ExperienceBiodiversity.Models;

public static class AvailabilityStatus
{
    public const string Available = "AVAILABLE";
    public const string Unavailable = "UNAVAILABLE";
    public const string Unknown = "UNKNOWN";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Available,
        Unavailable,
        Unknown
    };
}
