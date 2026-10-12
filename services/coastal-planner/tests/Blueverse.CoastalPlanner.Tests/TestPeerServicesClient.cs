using Blueverse.CoastalPlanner.Integration;

namespace Blueverse.CoastalPlanner.Tests;

internal sealed class TestPeerServicesClient : IPeerServicesClient
{
    public (List<PeerCatalogueItem> Items, bool Responded, string? Note) CatalogueResult { get; set; } =
        ([], false, "Experience Catalogue endpoint has not responded.");

    public (PeerSuitabilityResponse? Result, bool Responded, string? Note) MarineResult { get; set; } =
        (null, false, "Marine Conditions endpoint has not responded.");

    public Func<Guid, (PeerSuitabilityResponse? Result, bool Responded, string? Note)>? MarineResultForActivity { get; set; }

    public (PeerOperationStatusResponse? Result, bool Responded, string? Note) OperationsResult { get; set; } =
        (null, false, "Coastal Operations endpoint has not responded.");

    public (PeerBiodiversityInferenceResponse? Result, bool Responded, string? Note) BiodiversityResult { get; set; } =
        (null, false, "Biodiversity ML endpoint has not responded.");

    public int MarineCallCount { get; private set; }
    public int BiodiversityCallCount { get; private set; }

    public Task<(List<PeerCatalogueItem> Items, bool Responded, string? Note)> GetCatalogueOfferingsAsync(
        Guid destinationId, List<Guid>? preferredActivityIds, CancellationToken ct = default) =>
        Task.FromResult(CatalogueResult);

    public Task<(PeerSuitabilityResponse? Result, bool Responded, string? Note)> GetMarineSuitabilityAsync(
        Guid destinationId, Guid activityId, DateTime windowStart, DateTime windowEnd, CancellationToken ct = default)
    {
        MarineCallCount++;
        return Task.FromResult(MarineResultForActivity?.Invoke(activityId) ?? MarineResult);
    }

    public Task<(PeerOperationStatusResponse? Result, bool Responded, string? Note)> GetOperationalStatusAsync(
        Guid destinationId, CancellationToken ct = default) =>
        Task.FromResult(OperationsResult);

    public Task<(PeerBiodiversityInferenceResponse? Result, bool Responded, string? Note)> GetBiodiversityInferenceAsync(
        Guid destinationId, Guid? activityId, CancellationToken ct = default)
    {
        BiodiversityCallCount++;
        return Task.FromResult(BiodiversityResult);
    }
}
