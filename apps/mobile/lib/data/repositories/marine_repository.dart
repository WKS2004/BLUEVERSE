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

  Future<List<MarineAssessmentHistoryDto>> assessments({
    String? activityId,
    String? result,
    DateTime? fromUtc,
    DateTime? toUtc,
  }) => client.getAssessments(
    activityId: activityId,
    result: result,
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

  /// Returns all stored profile versions, including drafts and inactive history.
  Future<List<SafetyProfileDto>> currentProfiles() =>
      client.getSafetyProfiles();

  Future<SafetyProfileDto?> profile(String id) => client.getSafetyProfile(id);

  Future<SafetyProfileDto> createProfile({
    required String activityId,
    required double maxWindSpeed,
    required double maxWaveHeight,
    required double maxSwellHeight,
    required String windCriteriaSource,
    required String windCriteriaRationale,
    required String waveCriteriaSource,
    required String waveCriteriaRationale,
    required String swellCriteriaSource,
    required String swellCriteriaRationale,
    double? cautionWindSpeed,
    double? cautionWaveHeight,
    double? cautionSwellHeight,
  }) => client.createSafetyProfile(
    activityId: activityId,
    maxWindSpeed: maxWindSpeed,
    maxWaveHeight: maxWaveHeight,
    maxSwellHeight: maxSwellHeight,
    windCriteriaSource: windCriteriaSource,
    windCriteriaRationale: windCriteriaRationale,
    waveCriteriaSource: waveCriteriaSource,
    waveCriteriaRationale: waveCriteriaRationale,
    swellCriteriaSource: swellCriteriaSource,
    swellCriteriaRationale: swellCriteriaRationale,
    cautionWindSpeed: cautionWindSpeed,
    cautionWaveHeight: cautionWaveHeight,
    cautionSwellHeight: cautionSwellHeight,
  );

  Future<SafetyProfileDto> updateProfile({
    required String id,
    required double maxWindSpeed,
    required double maxWaveHeight,
    required double maxSwellHeight,
    required String windCriteriaSource,
    required String windCriteriaRationale,
    required String waveCriteriaSource,
    required String waveCriteriaRationale,
    required String swellCriteriaSource,
    required String swellCriteriaRationale,
    double? cautionWindSpeed,
    double? cautionWaveHeight,
    double? cautionSwellHeight,
  }) => client.updateSafetyProfile(
    id: id,
    maxWindSpeed: maxWindSpeed,
    maxWaveHeight: maxWaveHeight,
    maxSwellHeight: maxSwellHeight,
    windCriteriaSource: windCriteriaSource,
    windCriteriaRationale: windCriteriaRationale,
    waveCriteriaSource: waveCriteriaSource,
    waveCriteriaRationale: waveCriteriaRationale,
    swellCriteriaSource: swellCriteriaSource,
    swellCriteriaRationale: swellCriteriaRationale,
    cautionWindSpeed: cautionWindSpeed,
    cautionWaveHeight: cautionWaveHeight,
    cautionSwellHeight: cautionSwellHeight,
  );

  Future<SafetyProfileDto> reviewProfile(String id) =>
      client.reviewSafetyProfile(id);

  Future<void> deactivateProfile(String id) =>
      client.deactivateSafetyProfile(id);

  Future<List<Map<String, dynamic>>> activities() => client.getActivities();
}
