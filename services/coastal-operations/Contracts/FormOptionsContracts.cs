namespace Blueverse.CoastalOperations.Contracts;

public sealed record NamedCoastalReference(Guid Id, string Title, string? TargetType = null, Guid? TargetId = null);
public sealed record CoastalReferenceOptions(string Status, IReadOnlyList<NamedCoastalReference> Items);
public sealed record TimeZoneChoice(string Id, string Country, string Location, string Description,
    string SourceVersion, int? CurrentOffsetMinutes, bool SupportsDaylightSavingTime, bool RulesAvailable);
public sealed record OperationsFormOptions(IReadOnlyList<TimeZoneChoice> TimeZones,
    CoastalReferenceOptions Targets, CoastalReferenceOptions Plans, IReadOnlyList<NamedCoastalReference> Assessments);
