import '../models/experience_models.dart';
import '../services/experience_api_service.dart';

class ExperienceRepository {
  ExperienceRepository({required this.apiService});

  final ExperienceApiService apiService;

  // ----------------- Destinations -----------------

  Future<PagedResult<DestinationDto>> getDestinations({
    String? query,
    String? region,
    String? status,
    int? page,
    int? pageSize,
  }) =>
      apiService.getDestinations(
        query: query,
        region: region,
        status: status,
        page: page,
        pageSize: pageSize,
      );

  Future<DestinationDto> getDestinationById(String id) =>
      apiService.getDestinationById(id);

  Future<DestinationDto> createDestination(CreateDestinationRequest request) =>
      apiService.createDestination(request);

  Future<DestinationDto> updateDestination(
    String id,
    UpdateDestinationRequest request,
  ) =>
      apiService.updateDestination(id, request);

  Future<void> deleteDestination(String id) =>
      apiService.deleteDestination(id);

  Future<PublicationEvaluationResponse> evaluateDestinationPublication(
    String id,
    String status,
  ) =>
      apiService.evaluateDestinationPublication(id, status);

  Future<DestinationDto> updateDestinationPublication(
    String id,
    String status,
  ) =>
      apiService.updateDestinationPublication(id, status);

  Future<MarineConditionsContextDto> getDestinationMarineConditions(
    String id,
  ) =>
      apiService.getDestinationMarineConditions(id);

  Future<OperationalAdvisoriesResponseDto> getDestinationOperationalAdvisories(
    String id,
  ) =>
      apiService.getDestinationOperationalAdvisories(id);

  Future<BiodiversityContextResponseDto> getDestinationBiodiversity(
    String id,
  ) =>
      apiService.getDestinationBiodiversity(id);

  // ----------------- Activities -----------------

  Future<PagedResult<ActivityDto>> getActivities({
    String? destinationId,
    String? category,
    String? query,
    String? status,
    int? page,
    int? pageSize,
  }) =>
      apiService.getActivities(
        destinationId: destinationId,
        category: category,
        query: query,
        status: status,
        page: page,
        pageSize: pageSize,
      );

  Future<ActivityDto> getActivityById(String id) =>
      apiService.getActivityById(id);

  Future<ActivityDto> createActivity(CreateActivityRequest request) =>
      apiService.createActivity(request);

  Future<ActivityDto> updateActivity(
    String id,
    UpdateActivityRequest request,
  ) =>
      apiService.updateActivity(id, request);

  Future<void> deleteActivity(String id) =>
      apiService.deleteActivity(id);

  Future<PublicationEvaluationResponse> evaluateActivityPublication(
    String id,
    String status,
  ) =>
      apiService.evaluateActivityPublication(id, status);

  Future<ActivityDto> updateActivityPublication(
    String id,
    String status,
  ) =>
      apiService.updateActivityPublication(id, status);

  // ----------------- Offerings -----------------

  Future<PagedResult<OfferingDto>> getOfferings({
    String? activityId,
    String? destinationId,
    String? status,
    int? page,
    int? pageSize,
  }) =>
      apiService.getOfferings(
        activityId: activityId,
        destinationId: destinationId,
        status: status,
        page: page,
        pageSize: pageSize,
      );

  Future<OfferingDto> getOfferingById(String id) =>
      apiService.getOfferingById(id);

  Future<OfferingDto> createOffering(CreateOfferingRequest request) =>
      apiService.createOffering(request);

  Future<OfferingDto> updateOffering(
    String id,
    UpdateOfferingRequest request,
  ) =>
      apiService.updateOffering(id, request);

  Future<void> deleteOffering(String id) =>
      apiService.deleteOffering(id);

  Future<PublicationEvaluationResponse> evaluateOfferingPublication(
    String id,
    String status,
  ) =>
      apiService.evaluateOfferingPublication(id, status);

  Future<OfferingDto> updateOfferingPublication(
    String id,
    String status,
  ) =>
      apiService.updateOfferingPublication(id, status);

  Future<List<ScheduleDto>> getOfferingSchedules(
    String id, {
    String? from,
    String? to,
  }) =>
      apiService.getOfferingSchedules(id, from: from, to: to);

  Future<ScheduleDto> addOfferingSchedule(
    String id,
    CreateScheduleRequest request,
  ) =>
      apiService.addOfferingSchedule(id, request);

  Future<ScheduleDto> updateOfferingSchedule(
    String id,
    String scheduleId,
    UpdateScheduleRequest request,
  ) =>
      apiService.updateOfferingSchedule(id, scheduleId, request);

  Future<void> deleteOfferingSchedule(String id, String scheduleId) =>
      apiService.deleteOfferingSchedule(id, scheduleId);

  // ----------------- Availability -----------------

  Future<AvailabilityEvaluationResponse> evaluateAvailability(
    AvailabilityEvaluationRequest request,
  ) =>
      apiService.evaluateAvailability(request);

  // ----------------- Favourites -----------------

  Future<List<FavouriteDto>> getUserFavourites() =>
      apiService.getUserFavourites();

  Future<FavouriteDto> addFavourite(String targetType, String targetId) =>
      apiService.addFavourite(targetType, targetId);

  Future<void> removeFavourite(String targetType, String targetId) =>
      apiService.removeFavourite(targetType, targetId);

  // ----------------- Map & Nearby Proximity -----------------

  Future<MapConfigDto> getMapConfig() =>
      apiService.getMapConfig();

  Future<MapSearchResponseDto> searchMapPlaces(String query) =>
      apiService.searchMapPlaces(query);

  Future<NearbyResponse> getNearbyExperiences({
    String? query,
    double? latitude,
    double? longitude,
    double radiusMeters = 50000,
    int limit = 10,
  }) =>
      apiService.getNearbyExperiences(
        query: query,
        latitude: latitude,
        longitude: longitude,
        radiusMeters: radiusMeters,
        limit: limit,
      );

  // ----------------- Diagnostics & Agent Seam -----------------

  Future<DependenciesStatusResponseDto> getDependenciesStatus() =>
      apiService.getDependenciesStatus();

  Future<AgentContextResponseDto> getAgentContext() =>
      apiService.getAgentContext();
}
