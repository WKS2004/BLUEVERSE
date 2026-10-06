import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../data/models/experience_models.dart';
import '../../data/repositories/experience_repository.dart';
import '../../data/services/experience_api_service.dart';
import '../../data/services/experience_location_service.dart';

class ExperienceViewModel extends ChangeNotifier {
  ExperienceViewModel({
    required this.repository,
    ExperienceLocationProvider? locationProvider,
  }) : _locationProvider =
           locationProvider ?? const GeolocatorExperienceLocationProvider();

  final ExperienceRepository repository;
  final ExperienceLocationProvider _locationProvider;

  // Catalogue state
  List<DestinationDto> destinations = const [];
  List<ActivityDto> activities = const [];
  List<OfferingDto> offerings = const [];
  List<DestinationDto> managementDestinations = const [];
  List<ActivityDto> managementActivities = const [];
  List<OfferingDto> managementOfferings = const [];
  List<FavouriteDto> favourites = const [];

  // Search and filters
  String searchQuery = '';
  String selectedRegion = '';
  String selectedCategory = '';
  String selectedActivityId = '';

  // Place search and nearby discovery
  List<MapSearchResultItemDto> placeSearchResults = const [];
  bool isSearchingPlaces = false;
  String? placeSearchErrorMessage;
  MapConfigDto? mapConfig;
  bool isLoadingMapConfig = false;
  String? mapConfigErrorMessage;
  NearbyResponse? nearbyExperiences;
  bool isLoadingNearby = false;
  bool isGettingCurrentLocation = false;
  String? nearbyErrorMessage;
  String? nearbyCenterLabel;
  ExperienceCoordinates? mapFocusLocation;
  bool mapFocusIsCurrentLocation = false;
  String? selectedMapLocationName;
  String? selectedMapDestinationId;

  // Loading, messages and optional-user state
  bool isLoading = false;
  String? errorMessage;
  String? catalogueNotice;
  String? destinationsErrorMessage;
  String? activitiesErrorMessage;
  String? offeringsErrorMessage;
  String? successMessage;
  bool isLoadingFavourites = false;
  String? favouritesErrorMessage;
  String? favouriteActionErrorMessage;
  final Set<String> _favouriteActionsInProgress = {};
  bool isPerformingCatalogueAction = false;
  bool isLoadingManagementCatalog = false;
  String? managementCatalogErrorMessage;
  bool _favouritesRequested = false;

  // Destination detail state
  DestinationDto? selectedDestination;
  List<OfferingDto> destinationOfferings = const [];
  MarineConditionsContextDto? destinationMarine;
  String? destinationMarineError;
  OperationalAdvisoriesResponseDto? destinationAdvisories;
  String? destinationAdvisoriesError;
  BiodiversityContextResponseDto? destinationBiodiversity;
  String? destinationBiodiversityError;
  String? destinationOfferingsErrorMessage;
  bool isLoadingDestination = false;

  // Offering detail state
  OfferingDto? selectedOffering;
  List<ScheduleDto> offeringSchedules = const [];
  String? offeringSchedulesError;
  AvailabilityEvaluationResponse? availabilityResult;
  String? availabilityErrorMessage;
  bool isLoadingOffering = false;
  bool isEvaluatingAvailability = false;

  // Diagnostics and pre-G07 access seam
  DependenciesStatusResponseDto? dependenciesStatus;
  String? dependenciesStatusError;
  AgentContextResponseDto? agentContext;
  String? agentContextError;
  bool isLoadingDiagnostics = false;

  int _discoveryRequestId = 0;
  int _placeSearchRequestId = 0;
  int _mapConfigRequestId = 0;
  int _nearbyRequestId = 0;
  int _destinationRequestId = 0;
  int _offeringRequestId = 0;
  int _availabilityRequestId = 0;
  int _userScopeRevision = 0;
  int _favouritesLoadRequestId = 0;
  int _favouriteMutationRevision = 0;
  int _managementCatalogRequestId = 0;

  bool isFavourite(String targetType, String targetId) {
    final normType = targetType.toUpperCase();
    final normId = targetId.toLowerCase();
    return favourites.any(
      (f) =>
          f.targetType.toUpperCase() == normType &&
          f.targetId.toLowerCase() == normId,
    );
  }

  DestinationDto? get selectedMapDestination {
    final selectedId = selectedMapDestinationId;
    if (selectedId == null) return null;
    for (final destination in destinations) {
      if (destination.id == selectedId) return destination;
    }
    return null;
  }

  bool isFavouriteActionInProgress(String targetType, String targetId) =>
      _favouriteActionsInProgress.contains(_favouriteKey(targetType, targetId));

  List<ActivityDto> get visibleActivities {
    final query = searchQuery.trim().toLowerCase();
    return activities
        .where((activity) {
          final categoryMatches =
              selectedCategory.isEmpty ||
              (activity.category ?? '').toLowerCase() ==
                  selectedCategory.toLowerCase();
          if (!categoryMatches) return false;
          if (query.isEmpty) return true;
          return <String?>[
            activity.name,
            activity.code,
            activity.category,
            activity.description,
          ].any((value) => (value ?? '').toLowerCase().contains(query));
        })
        .toList(growable: false);
  }

  List<OfferingDto> get visibleOfferings {
    final query = searchQuery.trim().toLowerCase();
    final selectedActivityIds = selectedCategory.isEmpty
        ? null
        : activities
              .where(
                (activity) =>
                    (activity.category ?? '').toLowerCase() ==
                    selectedCategory.toLowerCase(),
              )
              .map((activity) => activity.id)
              .toSet();

    return offerings
        .where((offering) {
          if (selectedActivityId.isNotEmpty &&
              offering.activityId != selectedActivityId) {
            return false;
          }
          if (selectedActivityIds != null &&
              !selectedActivityIds.contains(offering.activityId)) {
            return false;
          }
          if (query.isEmpty) return true;
          return <String?>[
            offering.title,
            offering.description,
            offering.activityName,
            offering.destinationName,
          ].any((value) => (value ?? '').toLowerCase().contains(query));
        })
        .toList(growable: false);
  }

  List<String> get availableCategories {
    final values =
        activities
            .map((activity) => activity.category?.trim() ?? '')
            .where((category) => category.isNotEmpty)
            .toSet()
            .toList(growable: false)
          ..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
    return values;
  }

  Future<void> loadDiscoveryData({bool loadFavs = false}) async {
    final requestId = ++_discoveryRequestId;
    final userScopeRevision = _userScopeRevision;
    final favouriteMutationRevision = _favouriteMutationRevision;
    isLoading = true;
    errorMessage = null;
    catalogueNotice = null;
    destinationsErrorMessage = null;
    activitiesErrorMessage = null;
    offeringsErrorMessage = null;
    if (loadFavs) {
      _favouritesRequested = true;
      favouritesErrorMessage = null;
    }
    notifyListeners();

    final results = await Future.wait<Object?>([
      _safeCall(
        () => repository.getDestinations(
          query: searchQuery.trim().isNotEmpty ? searchQuery.trim() : null,
          region: selectedRegion.isNotEmpty ? selectedRegion : null,
          pageSize: 50,
        ),
      ),
      _safeCall(() => repository.getActivities(pageSize: 50)),
      _safeCall(() => repository.getOfferings(pageSize: 50)),
      if (loadFavs) _safeCall(repository.getUserFavourites),
    ]);

    if (requestId != _discoveryRequestId) return;

    final destinationResult =
        results[0] as _LoadResult<PagedResult<DestinationDto>>;
    final activityResult = results[1] as _LoadResult<PagedResult<ActivityDto>>;
    final offeringResult = results[2] as _LoadResult<PagedResult<OfferingDto>>;
    final coreErrors = <String>[];

    if (destinationResult.value != null) {
      destinations = destinationResult.value!.items;
    } else {
      destinationsErrorMessage = destinationResult.error;
      coreErrors.add('destinations');
    }
    if (activityResult.value != null) {
      activities = activityResult.value!.items;
    } else {
      activitiesErrorMessage = activityResult.error;
      coreErrors.add('activities');
    }
    if (offeringResult.value != null) {
      offerings = offeringResult.value!.items;
    } else {
      offeringsErrorMessage = offeringResult.error;
      coreErrors.add('offerings');
    }

    if (loadFavs && userScopeRevision == _userScopeRevision) {
      final favouritesResult = results[3] as _LoadResult<List<FavouriteDto>>;
      if (favouriteMutationRevision != _favouriteMutationRevision) {
        // Do not let an older catalogue request overwrite a completed save.
      } else if (favouritesResult.value != null) {
        favourites = favouritesResult.value!;
      } else {
        favouritesErrorMessage = favouritesResult.error;
      }
    }

    if (coreErrors.length == 3) {
      errorMessage = 'Coastal experiences could not be loaded. Check your connection and try again.';
    } else if (coreErrors.isNotEmpty) {
      catalogueNotice =
          'Some ${coreErrors.join(' and ')} could not be loaded. The available results are still shown.';
    }

    isLoading = false;
    notifyListeners();
  }

  Future<void> loadMapConfig({bool force = false}) async {
    if (isLoadingMapConfig || (!force && mapConfig != null)) return;

    final requestId = ++_mapConfigRequestId;
    isLoadingMapConfig = true;
    mapConfigErrorMessage = null;
    notifyListeners();

    try {
      final result = await repository.getMapConfig();
      if (requestId != _mapConfigRequestId) return;
      mapConfig = result;
    } catch (_) {
      if (requestId != _mapConfigRequestId) return;
      mapConfigErrorMessage = 'The coastal map could not be loaded. Check your connection and try again.';
    } finally {
      if (requestId == _mapConfigRequestId) {
        isLoadingMapConfig = false;
        notifyListeners();
      }
    }
  }

  Future<void> loadManagementCatalog() async {
    final requestId = ++_managementCatalogRequestId;
    isLoadingManagementCatalog = true;
    managementCatalogErrorMessage = null;
    notifyListeners();

    const statuses = ['DRAFT', 'PUBLISHED', 'ARCHIVED'];
    final results = await Future.wait<Object?>([
      Future.wait(
        statuses.map(
          (status) => _loadAllPages<DestinationDto>(
            (page) => repository.getDestinations(
              status: status,
              page: page,
              pageSize: 100,
            ),
          ),
        ),
      ),
      Future.wait(
        statuses.map(
          (status) => _loadAllPages<ActivityDto>(
            (page) => repository.getActivities(
              status: status,
              page: page,
              pageSize: 100,
            ),
          ),
        ),
      ),
      Future.wait(
        statuses.map(
          (status) => _loadAllPages<OfferingDto>(
            (page) => repository.getOfferings(
              status: status,
              page: page,
              pageSize: 100,
            ),
          ),
        ),
      ),
    ]);

    if (requestId != _managementCatalogRequestId) return;

    final destinationStates =
        results[0] as List<_LoadResult<List<DestinationDto>>>;
    final activityStates = results[1] as List<_LoadResult<List<ActivityDto>>>;
    final offeringStates = results[2] as List<_LoadResult<List<OfferingDto>>>;
    final stateWasMissing = [
      ...destinationStates.map((result) => result.value == null),
      ...activityStates.map((result) => result.value == null),
      ...offeringStates.map((result) => result.value == null),
    ];
    final stateHadError = [
      ...destinationStates.map((result) => result.error != null),
      ...activityStates.map((result) => result.error != null),
      ...offeringStates.map((result) => result.error != null),
    ];

    final destinationLoads = destinationStates
        .where((result) => result.value != null)
        .toList(growable: false);
    final activityLoads = activityStates
        .where((result) => result.value != null)
        .toList(growable: false);
    final offeringLoads = offeringStates
        .where((result) => result.value != null)
        .toList(growable: false);

    if (destinationLoads.isNotEmpty) {
      managementDestinations = destinationLoads
          .expand((result) => result.value!)
          .toList(growable: false);
    }
    if (activityLoads.isNotEmpty) {
      managementActivities = activityLoads
          .expand((result) => result.value!)
          .toList(growable: false);
    }
    if (offeringLoads.isNotEmpty) {
      managementOfferings = offeringLoads
          .expand((result) => result.value!)
          .toList(growable: false);
    }

    final allStatesFailed = stateWasMissing.every((missing) => missing);
    final anyStateFailed = stateHadError.any((failed) => failed);
    managementCatalogErrorMessage = allStatesFailed
        ? 'The catalogue could not be loaded. Check your connection and try again.'
        : anyStateFailed
        ? 'Some publication states could not be loaded. Refresh to see the complete catalogue.'
        : null;
    isLoadingManagementCatalog = false;
    notifyListeners();
  }

  void updateFilters({
    String? query,
    String? region,
    String? category,
    String? activityId,
  }) {
    if (query != null) searchQuery = query;
    if (region != null) selectedRegion = region;
    if (category != null) {
      selectedCategory = category;
      selectedActivityId = '';
    }
    if (activityId != null) selectedActivityId = activityId;
    notifyListeners();
  }

  Future<void> searchPlaces(String query) async {
    final normalizedQuery = query.trim();
    final requestId = ++_placeSearchRequestId;
    ++_nearbyRequestId;
    isGettingCurrentLocation = false;
    nearbyExperiences = null;
    nearbyErrorMessage = null;
    nearbyCenterLabel = null;
    isLoadingNearby = false;
    if (normalizedQuery.isEmpty) {
      placeSearchResults = const [];
      placeSearchErrorMessage = null;
      isSearchingPlaces = false;
      notifyListeners();
      return;
    }
    if (normalizedQuery.length < 2) {
      placeSearchResults = const [];
      placeSearchErrorMessage =
          'Enter at least 2 characters to search for a coastal place.';
      isSearchingPlaces = false;
      notifyListeners();
      return;
    }

    placeSearchResults = const [];
    placeSearchErrorMessage = null;
    isSearchingPlaces = true;
    notifyListeners();

    final result = await _safeCall(
      () => repository.searchMapPlaces(normalizedQuery),
    );
    if (requestId != _placeSearchRequestId) return;
    if (result.value != null) {
      placeSearchResults = result.value!.results;
      if (placeSearchResults.isEmpty) {
        placeSearchErrorMessage = 'No coastal places matched that search.';
      } else {
        for (final place in placeSearchResults) {
          if (!_hasUsableCoordinates(place.latitude, place.longitude)) continue;
          _setMapFocus(
            place.latitude,
            place.longitude,
            isCurrentLocation: false,
          );
          selectedMapLocationName = place.displayName;
          selectedMapDestinationId = null;
          unawaited(
            fetchNearby(
              latitude: place.latitude,
              longitude: place.longitude,
              centerLabel: place.displayName,
            ),
          );
          break;
        }
      }
    } else {
      placeSearchErrorMessage = 'Place search is unavailable. Browse the destination list or try again.';
    }
    isSearchingPlaces = false;
    notifyListeners();
  }

  Future<void> fetchNearby({
    String? query,
    double? latitude,
    double? longitude,
    double radiusMeters = 50000,
    String? centerLabel,
  }) async {
    final requestId = ++_nearbyRequestId;
    isGettingCurrentLocation = false;
    if (latitude != null && longitude != null) {
      _setMapFocus(latitude, longitude, isCurrentLocation: false);
      if (centerLabel != null) selectedMapLocationName = centerLabel;
    }
    nearbyErrorMessage = null;
    isLoadingNearby = true;
    notifyListeners();

    final result = await _safeCall(
      () => repository.getNearbyExperiences(
        query: query,
        latitude: latitude,
        longitude: longitude,
        radiusMeters: radiusMeters,
      ),
    );
    if (requestId != _nearbyRequestId) return;
    if (result.value != null) {
      nearbyExperiences = result.value;
      nearbyCenterLabel = centerLabel;
      if (nearbyExperiences!.results.isEmpty) {
        nearbyErrorMessage = 'No destinations were found near this place.';
      }
    } else {
      nearbyErrorMessage = 'Nearby destinations are unavailable. You can still browse or search by place.';
    }
    isLoadingNearby = false;
    notifyListeners();
  }

  Future<void> fetchNearbyFromCurrentLocation({
    double radiusMeters = 50000,
  }) async {
    final requestId = ++_nearbyRequestId;
    ++_placeSearchRequestId;
    isSearchingPlaces = false;
    nearbyErrorMessage = null;
    isGettingCurrentLocation = true;
    isLoadingNearby = true;
    notifyListeners();

    try {
      final location = await _locationProvider.getApproximateCurrentLocation();
      if (requestId != _nearbyRequestId) return;
      if (!_hasUsableCoordinates(location.latitude, location.longitude)) {
        throw const ExperienceLocationException(
          'Your approximate location could not be read. Search for a coastal place instead.',
        );
      }
      _setMapFocus(
        location.latitude,
        location.longitude,
        isCurrentLocation: true,
      );
      selectedMapLocationName = 'Your approximate location';
      selectedMapDestinationId = null;
      isGettingCurrentLocation = false;
      notifyListeners();
      final result = await _safeCall(
        () => repository.getNearbyExperiences(
          latitude: location.latitude,
          longitude: location.longitude,
          radiusMeters: radiusMeters,
        ),
      );
      if (requestId != _nearbyRequestId) return;
      if (result.value != null) {
        nearbyExperiences = result.value;
        nearbyCenterLabel = 'your approximate location';
        if (nearbyExperiences!.results.isEmpty) {
          nearbyErrorMessage = 'No destinations were found within 50 km. Try searching another coastal place.';
        }
      } else {
        nearbyErrorMessage = 'Nearby destinations are unavailable. You can still browse or search by place.';
      }
    } on ExperienceLocationException catch (error) {
      if (requestId == _nearbyRequestId) {
        nearbyErrorMessage = error.message;
      }
    } on Object {
      if (requestId == _nearbyRequestId) {
        nearbyErrorMessage = 'Your approximate location could not be used. Search for a coastal place instead.';
      }
    } finally {
      if (requestId == _nearbyRequestId) {
        isLoadingNearby = false;
        isGettingCurrentLocation = false;
        notifyListeners();
      }
    }
  }

  void selectMapPlace(MapSearchResultItemDto place) {
    if (!_hasUsableCoordinates(place.latitude, place.longitude)) return;
    ++_placeSearchRequestId;
    isSearchingPlaces = false;
    selectedMapLocationName = place.displayName;
    selectedMapDestinationId = null;
    isGettingCurrentLocation = false;
    _setMapFocus(place.latitude, place.longitude, isCurrentLocation: false);
    notifyListeners();
  }

  void selectMapDestination(String destinationId) {
    DestinationDto? destination;
    for (final candidate in destinations) {
      if (candidate.id == destinationId) {
        destination = candidate;
        break;
      }
    }
    if (destination == null ||
        !_hasUsableCoordinates(destination.latitude, destination.longitude)) {
      return;
    }

    ++_placeSearchRequestId;
    isSearchingPlaces = false;
    selectedMapLocationName = destination.name;
    selectedMapDestinationId = destination.id;
    isGettingCurrentLocation = false;
    _setMapFocus(
      destination.latitude,
      destination.longitude,
      isCurrentLocation: false,
    );
    unawaited(
      fetchNearby(
        latitude: destination.latitude,
        longitude: destination.longitude,
        centerLabel: destination.name,
      ),
    );
  }

  void clearMapSelection() {
    ++_nearbyRequestId;
    ++_placeSearchRequestId;
    isSearchingPlaces = false;
    isGettingCurrentLocation = false;
    isLoadingNearby = false;
    mapFocusLocation = null;
    mapFocusIsCurrentLocation = false;
    selectedMapLocationName = null;
    selectedMapDestinationId = null;
    nearbyExperiences = null;
    nearbyErrorMessage = null;
    nearbyCenterLabel = null;
    notifyListeners();
  }

  void _setMapFocus(
    double latitude,
    double longitude, {
    required bool isCurrentLocation,
  }) {
    if (!_hasUsableCoordinates(latitude, longitude)) return;
    mapFocusLocation = ExperienceCoordinates(
      latitude: latitude,
      longitude: longitude,
    );
    mapFocusIsCurrentLocation = isCurrentLocation;
  }

  bool _hasUsableCoordinates(double latitude, double longitude) =>
      latitude.isFinite &&
      longitude.isFinite &&
      latitude >= -85.0511 &&
      latitude <= 85.0511 &&
      longitude >= -180 &&
      longitude <= 180 &&
      !(latitude == 0 && longitude == 0);

  Future<void> toggleFavourite(String targetType, String targetId) async {
    final key = _favouriteKey(targetType, targetId);
    if (!_favouriteActionsInProgress.add(key)) return;
    final userScopeRevision = _userScopeRevision;
    _favouriteMutationRevision++;
    favouriteActionErrorMessage = null;
    successMessage = null;
    final already = isFavourite(targetType, targetId);
    notifyListeners();
    try {
      if (already) {
        await repository.removeFavourite(targetType, targetId);
        if (userScopeRevision != _userScopeRevision) return;
        favourites = favourites
            .where(
              (f) =>
                  !(f.targetType.toUpperCase() == targetType.toUpperCase() &&
                      f.targetId.toLowerCase() == targetId.toLowerCase()),
            )
            .toList(growable: false);
        successMessage = 'Removed from your coastal wishlist.';
      } else {
        final newFavourite = await repository.addFavourite(
          targetType,
          targetId,
        );
        if (userScopeRevision != _userScopeRevision) return;
        if (!isFavourite(targetType, targetId)) {
          favourites = [...favourites, newFavourite];
        }
        successMessage = 'Saved to your coastal wishlist.';
      }
      _favouriteMutationRevision++;
    } on Object catch (error) {
      if (userScopeRevision == _userScopeRevision) {
        favouriteActionErrorMessage = _friendlyError(error);
      }
    } finally {
      if (userScopeRevision == _userScopeRevision) {
        _favouriteActionsInProgress.remove(key);
        notifyListeners();
      }
    }
  }

  Future<void> loadFavourites() async {
    final requestId = ++_favouritesLoadRequestId;
    final userScopeRevision = _userScopeRevision;
    final favouriteMutationRevision = _favouriteMutationRevision;
    _favouritesRequested = true;
    isLoadingFavourites = true;
    favouritesErrorMessage = null;
    notifyListeners();

    final result = await _safeCall(repository.getUserFavourites);
    if (requestId != _favouritesLoadRequestId ||
        userScopeRevision != _userScopeRevision) {
      return;
    }
    if (favouriteMutationRevision != _favouriteMutationRevision) {
      isLoadingFavourites = false;
      notifyListeners();
      return;
    }
    if (result.value != null) {
      favourites = result.value!;
    } else {
      favouritesErrorMessage = result.error;
    }
    isLoadingFavourites = false;
    notifyListeners();
  }

  void clearUserScopedState() {
    _userScopeRevision++;
    _favouritesLoadRequestId++;
    _favouriteMutationRevision++;
    _favouriteActionsInProgress.clear();
    isLoadingFavourites = false;
    favourites = const [];
    favouritesErrorMessage = null;
    favouriteActionErrorMessage = null;
    successMessage = null;
    _favouritesRequested = false;
    notifyListeners();
  }

  Future<void> loadDestinationDetail(String destinationId) async {
    final requestId = ++_destinationRequestId;
    isLoadingDestination = true;
    errorMessage = null;
    catalogueNotice = null;
    selectedDestination = null;
    destinationOfferings = const [];
    destinationMarine = null;
    destinationMarineError = null;
    destinationAdvisories = null;
    destinationAdvisoriesError = null;
    destinationBiodiversity = null;
    destinationBiodiversityError = null;
    destinationOfferingsErrorMessage = null;
    notifyListeners();

    final results = await Future.wait<Object?>([
      _safeCall(() => repository.getDestinationById(destinationId)),
      _safeCall(
        () =>
            repository.getOfferings(destinationId: destinationId, pageSize: 50),
      ),
      _safeCall(() => repository.getDestinationMarineConditions(destinationId)),
      _safeCall(
        () => repository.getDestinationOperationalAdvisories(destinationId),
      ),
      _safeCall(() => repository.getDestinationBiodiversity(destinationId)),
    ]);
    if (requestId != _destinationRequestId) return;

    final destination = results[0] as _LoadResult<DestinationDto>;
    final destinationOfferingResult =
        results[1] as _LoadResult<PagedResult<OfferingDto>>;
    final marine = results[2] as _LoadResult<MarineConditionsContextDto>;
    final advisories =
        results[3] as _LoadResult<OperationalAdvisoriesResponseDto>;
    final biodiversity =
        results[4] as _LoadResult<BiodiversityContextResponseDto>;

    if (destination.value != null) {
      selectedDestination = destination.value;
    } else {
      errorMessage = destination.error;
    }
    destinationOfferings = destinationOfferingResult.value?.items ?? const [];
    destinationOfferingsErrorMessage = destinationOfferingResult.error;
    destinationMarine = marine.value;
    destinationMarineError = marine.error;
    destinationAdvisories = advisories.value;
    destinationAdvisoriesError = advisories.error;
    destinationBiodiversity = biodiversity.value;
    destinationBiodiversityError = biodiversity.error;

    isLoadingDestination = false;
    notifyListeners();
  }

  Future<void> loadOfferingDetail(String offeringId) async {
    final requestId = ++_offeringRequestId;
    isLoadingOffering = true;
    errorMessage = null;
    selectedOffering = null;
    offeringSchedules = const [];
    offeringSchedulesError = null;
    availabilityResult = null;
    availabilityErrorMessage = null;
    notifyListeners();

    final results = await Future.wait<Object?>([
      _safeCall(() => repository.getOfferingById(offeringId)),
      _safeCall(() => repository.getOfferingSchedules(offeringId)),
    ]);
    if (requestId != _offeringRequestId) return;

    final offering = results[0] as _LoadResult<OfferingDto>;
    final schedules = results[1] as _LoadResult<List<ScheduleDto>>;
    selectedOffering = offering.value;
    errorMessage = offering.error;
    offeringSchedules = schedules.value ?? const [];
    offeringSchedulesError = schedules.error;
    isLoadingOffering = false;
    notifyListeners();
  }

  Future<void> evaluateOfferingAvailability({
    required String offeringId,
    required DateTime startUtc,
    required DateTime endUtc,
  }) async {
    final requestId = ++_availabilityRequestId;
    final start = startUtc.toUtc();
    final end = endUtc.toUtc();
    if (!end.isAfter(start)) {
      availabilityErrorMessage = 'Choose an end time after the start time.';
      availabilityResult = null;
      isEvaluatingAvailability = false;
      notifyListeners();
      return;
    }

    isEvaluatingAvailability = true;
    availabilityErrorMessage = null;
    availabilityResult = null;
    notifyListeners();

    final result = await _safeCall(
      () => repository.evaluateAvailability(
        AvailabilityEvaluationRequest(
          offeringId: offeringId,
          startsAt: start,
          endsAt: end,
        ),
      ),
    );
    if (requestId != _availabilityRequestId) return;
    availabilityResult = result.value;
    availabilityErrorMessage = result.error;
    isEvaluatingAvailability = false;
    notifyListeners();
  }

  Future<T?> performCatalogueAction<T>(
    Future<T> Function() operation, {
    required String successText,
    bool refreshCatalog = false,
    bool refreshManagementCatalog = true,
  }) async {
    isPerformingCatalogueAction = true;
    errorMessage = null;
    successMessage = null;
    notifyListeners();
    try {
      final result = await operation();
      successMessage = successText;
      if (refreshCatalog) {
        await loadDiscoveryData(loadFavs: _favouritesRequested);
      }
      if (refreshManagementCatalog) {
        await loadManagementCatalog();
      }
      return result;
    } on Object catch (error) {
      errorMessage = _friendlyError(error);
      return null;
    } finally {
      isPerformingCatalogueAction = false;
      notifyListeners();
    }
  }

  Future<void> loadDiagnostics() async {
    isLoadingDiagnostics = true;
    dependenciesStatusError = null;
    agentContextError = null;
    notifyListeners();

    final results = await Future.wait<Object?>([
      _safeCall(repository.getDependenciesStatus),
      _safeCall(repository.getAgentContext),
    ]);
    final dependencies =
        results[0] as _LoadResult<DependenciesStatusResponseDto>;
    final agent = results[1] as _LoadResult<AgentContextResponseDto>;
    dependenciesStatus = dependencies.value;
    dependenciesStatusError = dependencies.error;
    agentContext = agent.value;
    agentContextError = agent.error;
    isLoadingDiagnostics = false;
    notifyListeners();
  }

  void clearMessages() {
    errorMessage = null;
    catalogueNotice = null;
    successMessage = null;
    favouriteActionErrorMessage = null;
    notifyListeners();
  }

  Future<_LoadResult<T>> _safeCall<T>(Future<T> Function() operation) async {
    try {
      return _LoadResult<T>(value: await operation());
    } on Object catch (error) {
      return _LoadResult<T>(error: _friendlyError(error));
    }
  }

  Future<_LoadResult<List<T>>> _loadAllPages<T>(
    Future<PagedResult<T>> Function(int page) loadPage,
  ) async {
    const maxPages = 100;
    final items = <T>[];
    var total = 0;

    for (var page = 1; page <= maxPages; page++) {
      final result = await _safeCall(() => loadPage(page));
      final pageResult = result.value;
      if (pageResult == null) {
        return _LoadResult<List<T>>(
          value: items.isEmpty ? null : items,
          error: result.error,
        );
      }

      if (page == 1) total = pageResult.total;
      if (pageResult.items.isEmpty) {
        return _LoadResult<List<T>>(
          value: items,
          error: items.length < total
              ? 'The catalogue returned an incomplete page.'
              : null,
        );
      }
      items.addAll(pageResult.items);
      if (items.length >= total) return _LoadResult<List<T>>(value: items);
    }

    return _LoadResult<List<T>>(
      value: items,
      error: 'The catalogue exceeds the mobile page limit.',
    );
  }

  String _friendlyError(Object error) {
    if (error is ExperienceApiException) {
      if (error.statusCode == 0) {
        return 'The coastal service could not be reached. Check your connection and try again.';
      }
      if (error.statusCode == 401) {
        return 'Your session has expired. Sign in again to continue.';
      }
      if (error.statusCode == 403) {
        return 'Your account does not have permission to do that.';
      }
      if (error.statusCode == 404) {
        return 'This coastal experience is no longer available.';
      }
      if (error.statusCode >= 500) {
        return 'The coastal service is having trouble. Try again shortly.';
      }
      return error.message;
    }
    if (error is FormatException) {
      return 'The coastal service returned information the app could not read.';
    }
    return 'Something went wrong. Please try again.';
  }

  String _favouriteKey(String targetType, String targetId) =>
      '${targetType.toUpperCase()}:${targetId.toLowerCase()}';
}

class _LoadResult<T> {
  const _LoadResult({this.value, this.error});

  final T? value;
  final String? error;
}
