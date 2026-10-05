using Blueverse.CoastalPlanner.Models.Dtos;

namespace Blueverse.CoastalPlanner.Services;

public interface ICoastalPlannerService
{
    Task<RecommendationResultDto> GenerateRecommendationsAsync(RecommendationRequestDto request, Guid userId, CancellationToken ct = default);
    Task<RecommendationResultDto?> GetRecommendationAsync(Guid recommendationId, Guid userId, CancellationToken ct = default);
    Task<WorkflowStatusDto?> GetWorkflowStatusAsync(Guid workflowId, Guid userId, CancellationToken ct = default);

    Task<ItineraryDto> CreateItineraryAsync(CreateItineraryRequestDto request, Guid userId, CancellationToken ct = default);
    Task<List<ItineraryDto>> ListItinerariesAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<ItineraryDto?> GetItineraryAsync(Guid itineraryId, Guid userId, CancellationToken ct = default);
    Task<ItineraryDto?> UpdateItineraryAsync(Guid itineraryId, UpdateItineraryRequestDto request, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteItineraryAsync(Guid itineraryId, Guid userId, CancellationToken ct = default);

    Task<ItineraryReEvaluationResultDto?> ReEvaluateItineraryAsync(Guid itineraryId, Guid userId, ItineraryReEvaluationRequestDto request, CancellationToken ct = default);

    Task<BiodiversityPredictionDto> GetBiodiversityPredictionsAsync(Guid destinationId, Guid? activityId, CancellationToken ct = default);
}
