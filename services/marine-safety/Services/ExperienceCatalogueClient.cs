using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blueverse.MarineSafety.Dtos;

namespace Blueverse.MarineSafety.Services;

public interface IExperienceCatalogueClient
{
    Task<IReadOnlyList<MarineActivityDto>> GetPublishedAsync(CancellationToken cancellationToken);
}
public sealed class CatalogueUnavailableException(string reason = "UNREACHABLE") : Exception
{
    public string Reason { get; } = reason;
}

public sealed class ExperienceCatalogueClient(HttpClient client) : IExperienceCatalogueClient
{
    private sealed record Activity(Guid Id, string? Code, string? Name, string? Category, string? Status,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    public async Task<IReadOnlyList<MarineActivityDto>> GetPublishedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (client.BaseAddress is null) throw new CatalogueUnavailableException("UNCONFIGURED");
        try
        {
            using var response = await client.GetAsync("api/experiences/activities", cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK) throw new CatalogueUnavailableException("UPSTREAM_UNAVAILABLE");
            var rows = await response.Content.ReadFromJsonAsync<List<Activity>>(cancellationToken);
            if (rows is null || rows.Any(r => r is null) || rows.Count > 10000 || rows.Select(r => r.Id).Distinct().Count() != rows.Count ||
                rows.Any(r => r.Id == Guid.Empty || string.IsNullOrWhiteSpace(r.Code) ||
                    string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 100 || r.Status != "PUBLISHED" ||
                    r.Category?.Length > 100 || r.CreatedAt == default || r.UpdatedAt < r.CreatedAt))
                throw new CatalogueUnavailableException("INVALID_RESPONSE");
            return rows.OrderBy(r => r.Name).Select(r => new MarineActivityDto(
                r.Id, r.Name!, r.Category ?? "Uncategorized", true, r.CreatedAt.UtcDateTime, r.UpdatedAt.UtcDateTime)).ToList();
        }
        catch (HttpRequestException) { throw new CatalogueUnavailableException(); }
        catch (JsonException) { throw new CatalogueUnavailableException("INVALID_RESPONSE"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new CatalogueUnavailableException("TIMEOUT"); }
    }
}
