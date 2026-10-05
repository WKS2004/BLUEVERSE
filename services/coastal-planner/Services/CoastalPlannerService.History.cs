using System.Text.Json;
using Blueverse.CoastalPlanner.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalPlanner.Services;

public partial class CoastalPlannerService
{
    private async Task ValidateRecommendationSelectionAsync(Guid? recommendationId,
        List<CreateItineraryItemRequestDto> items, Guid ownerId, CancellationToken ct)
    {
        if (!recommendationId.HasValue) return; // Unchecked drafts retain UNKNOWN evidence.
        var recommendation = await GetRecommendationAsync(recommendationId.Value, ownerId, ct);
        if (recommendation is null || recommendation.GeneratedAt < DateTime.UtcNow.AddMinutes(-15))
            throw new ArgumentException("Your suggestions have expired or cannot be found. Refresh them before saving.");
        if (items.Count == 0 || items.Any(item => !recommendation.Candidates.Any(c => c.DestinationId == item.DestinationId &&
            c.ActivityId == item.ActivityId && c.OfferingId == item.OfferingId && c.Title == item.Title &&
            c.ScheduledStart == item.ScheduledStart && c.ScheduledEnd == item.ScheduledEnd && c.TimeZone == item.TimeZone)))
            throw new ArgumentException("Only the experiences and times from your suggestions can be saved with this search.");
    }

    public async Task<List<ItineraryReEvaluationResultDto>?> GetEvaluationHistoryAsync(Guid itineraryId,
        Guid ownerId, CancellationToken ct = default)
    {
        if (!await _db.Itineraries.AnyAsync(i => i.ItineraryId == itineraryId && i.OwnerUserId == ownerId, ct)) return null;
        var rows = await _db.ItineraryEvaluations.AsNoTracking().Where(e => e.ItineraryId == itineraryId)
            .OrderByDescending(e => e.EvaluatedAtUtc).Take(20).Select(e => e.ResultJson).ToListAsync(ct);
        return rows.Select(json => JsonSerializer.Deserialize<ItineraryReEvaluationResultDto>(json)!).ToList();
    }
}
