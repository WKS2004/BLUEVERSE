import 'package:flutter/foundation.dart';

import '../../data/models/experience_models.dart';
import '../../data/repositories/experience_repository.dart';

class ExperienceViewModel extends ChangeNotifier {
  ExperienceViewModel({required this.repository});

  final ExperienceRepository repository;

  // Catalog state
  List<DestinationDto> destinations = const [];
  List<ActivityDto> activities = const [];
  List<OfferingDto> offerings = const [];
  List<FavouriteDto> favourites = const [];
  MapConfigDto? mapConfig;

  // Search & Filters
  String searchQuery = '';
  String selectedRegion = '';
  String selectedCategory = '';

  // Interactive Map / Place search state
  List<MapSearchResultItemDto> placeSearchResults = const [];
  bool isSearchingPlaces = false;
  NearbyResponse? nearbyExperiences;

  // Loading & Error states
  bool isLoading = false;
  String? errorMessage;
  String? successMessage;

  // Destination detail state
  DestinationDto? selectedDestination;
  List<OfferingDto> destinationOfferings = const [];
  MarineConditionsContextDto? destinationMarine;
  OperationalAdvisoriesResponseDto? destinationAdvisories;
  BiodiversityContextResponseDto? destinationBiodiversity;
  bool isLoadingDestination = false;

  // Offering detail state
  OfferingDto? selectedOffering;
  List<ScheduleDto> offeringSchedules = const [];
  AvailabilityEvaluationResponse? availabilityResult;
  bool isLoadingOffering = false;
  bool isEvaluatingAvailability = false;

  // Diagnostics & Seam
  DependenciesStatusResponseDto? dependenciesStatus;
  AgentContextResponseDto? agentContext;

  bool isFavourite(String targetType, String targetId) {
    final normType = targetType.toUpperCase();
    final normId = targetId.toLowerCase();
    return favourites.any(
      (f) =>
          f.targetType.toUpperCase() == normType &&
          f.targetId.toLowerCase() == normId,
    );
  }

  Future<void> loadDiscoveryData({bool loadFavs = true}) async {
    isLoading = true;
    errorMessage = null;
    notifyListeners();

    try {
      final destsFuture = repository.getDestinations(
        query: searchQuery.isNotEmpty ? searchQuery : null,
        region: selectedRegion.isNotEmpty ? selectedRegion : null,
        pageSize: 50,
      );
      final actsFuture = repository.getActivities(
        category: selectedCategory.isNotEmpty ? selectedCategory : null,
        pageSize: 50,
      );
      final offFuture = repository.getOfferings(pageSize: 50);
      final mapFuture = repository.getMapConfig();
      final favFuture = loadFavs ? repository.getUserFavourites() : Future.value(<FavouriteDto>[]);

      final results = await Future.wait([
        destsFuture,
        actsFuture,
        offFuture,
        mapFuture,
        favFuture,
      ]);

      destinations = (results[0] as PagedResult<DestinationDto>).items;
      activities = (results[1] as PagedResult<ActivityDto>).items;
      offerings = (results[2] as PagedResult<OfferingDto>).items;
      mapConfig = results[3] as MapConfigDto;
      if (loadFavs) {
        favourites = results[4] as List<FavouriteDto>;
      }
    } on Object catch (e) {
      errorMessage = e.toString();
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  void updateFilters({
    String? query,
    String? region,
    String? category,
  }) {
    if (query != null) searchQuery = query;
    if (region != null) selectedRegion = region;
    if (category != null) selectedCategory = category;
    notifyListeners();
  }

  Future<void> searchPlaces(String query) async {
    if (query.trim().isEmpty) {
      placeSearchResults = const [];
      notifyListeners();
      return;
    }

    isSearchingPlaces = true;
    notifyListeners();

    try {
      final res = await repository.searchMapPlaces(query);
      placeSearchResults = res.results;
    } on Object catch (e) {
      errorMessage = e.toString();
    } finally {
      isSearchingPlaces = false;
      notifyListeners();
    }
  }

  Future<void> fetchNearby({
    String? query,
    double? latitude,
    double? longitude,
    double radiusMeters = 50000,
  }) async {
    isLoading = true;
    notifyListeners();

    try {
      nearbyExperiences = await repository.getNearbyExperiences(
        query: query,
        latitude: latitude,
        longitude: longitude,
        radiusMeters: radiusMeters,
      );
    } on Object catch (e) {
      errorMessage = e.toString();
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<void> toggleFavourite(String targetType, String targetId) async {
    final already = isFavourite(targetType, targetId);
    try {
      if (already) {
        await repository.removeFavourite(targetType, targetId);
        favourites = favourites
            .where(
              (f) => !(f.targetType.toUpperCase() == targetType.toUpperCase() &&
                  f.targetId.toLowerCase() == targetId.toLowerCase()),
            )
            .toList(growable: false);
        successMessage = 'Removed from your coastal wishlist.';
      } else {
        final newFav = await repository.addFavourite(targetType, targetId);
        favourites = [...favourites, newFav];
        successMessage = 'Saved to your coastal wishlist.';
      }
    } on Object catch (e) {
      errorMessage = e.toString();
    }
    notifyListeners();
  }

  Future<void> loadFavourites() async {
    isLoading = true;
    errorMessage = null;
    notifyListeners();

    try {
      favourites = await repository.getUserFavourites();
    } on Object catch (e) {
      errorMessage = e.toString();
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<void> loadDestinationDetail(String destinationId) async {
    isLoadingDestination = true;
    errorMessage = null;
    notifyListeners();

    try {
      final destFuture = repository.getDestinationById(destinationId);
      final offFuture = repository.getOfferings(destinationId: destinationId, pageSize: 20);

      final dest = await destFuture;
      final offRes = await offFuture;

      selectedDestination = dest;
      destinationOfferings = offRes.items;

      // Resilient enrichment queries
      try {
        destinationMarine = await repository.getDestinationMarineConditions(destinationId);
      } catch (_) {
        destinationMarine = null;
      }

      try {
        destinationAdvisories = await repository.getDestinationOperationalAdvisories(destinationId);
      } catch (_) {
        destinationAdvisories = null;
      }

      try {
        destinationBiodiversity = await repository.getDestinationBiodiversity(destinationId);
      } catch (_) {
        destinationBiodiversity = null;
      }
    } on Object catch (e) {
      errorMessage = e.toString();
    } finally {
      isLoadingDestination = false;
      notifyListeners();
    }
  }

  Future<void> loadOfferingDetail(String offeringId) async {
    isLoadingOffering = true;
    errorMessage = null;
    availabilityResult = null;
    notifyListeners();

    try {
      final off = await repository.getOfferingById(offeringId);
      final scheds = await repository.getOfferingSchedules(offeringId);
      selectedOffering = off;
      offeringSchedules = scheds;
    } on Object catch (e) {
      errorMessage = e.toString();
    } finally {
      isLoadingOffering = false;
      notifyListeners();
    }
  }

  Future<void> evaluateOfferingAvailability({
    required String offeringId,
    required DateTime startUtc,
    required DateTime endUtc,
    int requestedPartySize = 2,
  }) async {
    isEvaluatingAvailability = true;
    notifyListeners();

    try {
      final req = AvailabilityEvaluationRequest(
        offeringId: offeringId,
        startsAt: startUtc,
        endsAt: endUtc,
      );
      availabilityResult = await repository.evaluateAvailability(req);
    } on Object catch (e) {
      errorMessage = e.toString();
    } finally {
      isEvaluatingAvailability = false;
      notifyListeners();
    }
  }

  // Catalogue Management & Diagnostics
  Future<void> loadDiagnostics() async {
    isLoading = true;
    notifyListeners();

    try {
      dependenciesStatus = await repository.getDependenciesStatus();
      agentContext = await repository.getAgentContext();
    } on Object catch (e) {
      errorMessage = e.toString();
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  void clearMessages() {
    errorMessage = null;
    successMessage = null;
    notifyListeners();
  }
}
