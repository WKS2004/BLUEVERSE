using System.Text.Json;
using Blueverse.CoastalOperations.Domain;

namespace Blueverse.CoastalOperations.Data;

public static class TimeZoneCatalogueSeed
{
    public static IReadOnlyList<TimeZoneLocation> Locations { get; } = Load();
    private static IReadOnlyList<TimeZoneLocation> Load()
    {
        using var stream = typeof(TimeZoneCatalogueSeed).Assembly.GetManifestResourceStream(
            "Blueverse.CoastalOperations.Data.ReferenceData.time-zone-locations.json")
            ?? throw new InvalidOperationException("The IANA selection catalogue is missing.");
        return JsonSerializer.Deserialize<List<TimeZoneLocation>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The IANA selection catalogue is invalid.");
    }
}
