import '../../data/models/marine_models.dart';
import '../../data/services/marine_api_client.dart';

/// Repository layer for the marine-safety domain.
///
/// It hides HTTP plumbing behind named operations and exposes only the data
/// shapes the UI needs. Authorization is inherited from the caller; the
/// repository never makes an unauthenticated request.
class MarineRepository {
  MarineRepository({required this.client});

  final MarineApiClient client;

  Future<ConditionSnapshotDto> currentConditions({
    required double latitude,
    required double longitude,
    DateTime? timeUtc,
  }) => client.getConditions(
        latitude: latitude,
        longitude: longitude,
        timeUtc: timeUtc,
      );

  Future<List<ConditionSnapshotDto>> history({
    double? latitude,
    double? longitude,
    DateTime? fromUtc,
    DateTime? toUtc,
  }) => client.getHistory(
        latitude: latitude,
        longitude: longitude,
        fromUtc: fromUtc,
        toUtc: toUtc,
      );

  Future<SuitabilityResultDto> evaluateSuitability({
    required String activityId,
    required double latitude,
    required double longitude,
    DateTime? timeUtc,
  }) => client.evaluateSuitability(
        activityId: activityId,
        latitude: latitude,
        longitude: longitude,
        timeUtc: timeUtc,
      );

  /// Returns active profiles only. Read-only historical/archeological data is
  /// owned by the backend and visible in the full profile list.
  Future<List<SafetyProfileDto>> currentProfiles() => client.getSafetyProfiles();

  Future<SafetyProfileDto?> profile(String id) => client.getSafetyProfile(id);

  Future<SafetyProfileDto> createProfile({
    required String activityId,
    required double maxWindSpeed,
    required double maxWaveHeight,
    required double maxSwellHeight,
    double? cautionWindSpeed,
    double? cautionWaveHeight,
    double? cautionSwellHeight,
  }) => client.createSafetyProfile(
        activityId: activityId,
        maxWindSpeed: maxWindSpeed,
        maxWaveHeight: maxWaveHeight,
        maxSwellHeight: maxSwellHeight,
        cautionWindSpeed: cautionWindSpeed,
        cautionWaveHeight: cautionWaveHeight,
        cautionSwellHeight: cautionSwellHeight,
      );

  Future<SafetyProfileDto> updateProfile({
    required String id,
    required double maxWindSpeed,
    required double maxWaveHeight,
    required double maxSwellHeight,
    double? cautionWindSpeed,
    double? cautionWaveHeight,
    double? cautionSwellHeight,
    required bool isActive,
  }) => client.updateSafetyProfile(
        id: id,
        maxWindSpeed: maxWindSpeed,
        maxWaveHeight: maxWaveHeight,
        maxSwellHeight: maxSwellHeight,
        cautionWindSpeed: cautionWindSpeed,
        cautionWaveHeight: cautionWaveHeight,
        cautionSwellHeight: cautionSwellHeight,
        isActive: isActive,
      );

  Future<void> deactivateProfile(String id) => client.deactivateSafetyProfile(id);

  /// Reference activity identifiers owned by the backend service.
  static const List<Map<String, dynamic>> _referenceActivities = [
    {'id': '33333333-3333-3333-3333-333333333301', 'name': 'Surfing', 'type': 'Surfing'},
    {'id': '33333333-3333-3333-3333-333333333302', 'name': 'Snorkeling', 'type': 'Snorkeling'},
    {'id': '33333333-3333-3333-3333-333333333303', 'name': 'Scuba Diving', 'type': 'Diving'},
    {'id': '33333333-3333-3333-3333-333333333304', 'name': 'Whale and Dolphin Watching', 'type': 'BoatTour'},
    {'id': '33333333-3333-3333-3333-333333333305', 'name': 'Coastal Boat Tour', 'type': 'BoatTour'},
  ];

  /// Immutable service-owned activity reference rows. Only these IDs are used
  /// by the client to target existing marine operations.
  static List<Map<String, dynamic>> get referenceActivities =>
      List.unmodifiable(_referenceActivities);
}
