import '../../data/models/marine_models.dart';
import '../../data/repositories/marine_repository.dart';

/// Service boundary for UI-level marine workflows.
///
/// Keeps widgets free of HTTP and repository details; it only coordinates
/// the repository calls and propagates typed exceptions.
class MarineService {
  MarineService({required MarineRepository repository})
    : _repository = repository;

  final MarineRepository _repository;

  Future<ConditionSnapshotDto> currentConditions({
    required double latitude,
    required double longitude,
    DateTime? timeUtc,
  }) => _repository.currentConditions(
    latitude: latitude,
    longitude: longitude,
    timeUtc: timeUtc,
  );

  Future<List<ConditionSnapshotDto>> history({
    double? latitude,
    double? longitude,
    DateTime? fromUtc,
    DateTime? toUtc,
  }) => _repository.history(
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
  }) => _repository.assessments(
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
  }) => _repository.evaluateSuitability(
    activityId: activityId,
    latitude: latitude,
    longitude: longitude,
    timeUtc: timeUtc,
  );

  Future<List<SafetyProfileDto>> currentProfiles() =>
      _repository.currentProfiles();

  Future<SafetyProfileDto?> profile(String id) => _repository.profile(id);

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
  }) => _repository.createProfile(
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
  }) => _repository.updateProfile(
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
      _repository.reviewProfile(id);

  Future<void> deactivateProfile(String id) =>
      _repository.deactivateProfile(id);

  List<Map<String, dynamic>> get referenceActivities =>
      MarineRepository.referenceActivities;
}
