namespace Blueverse.CoastalOperations.Domain;

public sealed class TimeZoneLocation
{
    public string Id { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Coordinates { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
