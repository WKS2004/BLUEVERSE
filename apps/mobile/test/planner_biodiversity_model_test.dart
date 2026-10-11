import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/data/models/planner_models.dart';

void main() {
  const messages = {
    'available':
        'Wildlife context is available. A prediction does not guarantee a sighting.',
    'not_requested': 'Wildlife context was not requested.',
    'unavailable':
        'Wildlife predictions are temporarily unavailable. Please try again later.',
    'invalid': 'Wildlife predictions could not be verified for this location.',
    'stale': 'The wildlife prediction is out of date. Request a fresh result.',
  };

  for (final entry in messages.entries) {
    test('Planner biodiversity parses ${entry.key} status', () {
      final result = PlannerBiodiversityPrediction.fromJson({
        'contractVersion': 1,
        'destinationId': 'destination-id',
        'activityId': 'activity-id',
        'status': entry.key,
        'message': entry.key == 'available' ? entry.value : 'untrusted provider message',
        'limitations': entry.key == 'available' ? 'Fixture limitation' : 'untrusted provider limitation',
        'predictedSpecies': [
          {
            'speciesId': 'synthetic',
            'scientificName': 'Synthetic species',
            'commonName': 'Fixture',
            'habitatSuitability': 0.8,
            'confidenceLevel': 'synthetic-test-only',
          },
        ],
        'modelMetadata': {
          'modelVersion': 'fixture-v1',
          'inferenceTimestamp': '2026-10-11T00:00:00Z',
        },
        'provenance': {
          'provider': 'test-provider',
          'modelVersion': 'fixture-v1',
          'inferenceTimestamp': '2026-10-11T00:00:00Z',
        },
      });

      expect(result.status, entry.key);
      expect(result.message, entry.value);
      if (entry.key == 'available') {
        expect(result.predictedSpecies, hasLength(1));
      } else {
        expect(result.predictedSpecies, isEmpty);
        expect(result.modelMetadata, isNull);
        expect(result.provenance, isNull);
      }
    });
  }

  test('unknown status fails closed to unavailable and drops prediction data', () {
    final result = PlannerBiodiversityPrediction.fromJson({
      'contractVersion': 1,
      'destinationId': 'destination-id',
      'activityId': null,
      'status': 'future-status',
      'limitations': 'untrusted future payload',
      'predictedSpecies': [
        {
          'speciesId': 'synthetic',
          'scientificName': 'Synthetic species',
          'commonName': 'Fixture',
          'habitatSuitability': 0.8,
          'confidenceLevel': 'synthetic-test-only',
        },
      ],
      'modelMetadata': {'modelVersion': 'future', 'inferenceTimestamp': null},
    });

    expect(result.status, 'unavailable');
    expect(result.message, messages['unavailable']);
    expect(result.predictedSpecies, isEmpty);
    expect(result.modelMetadata, isNull);
    expect(result.limitations, messages['unavailable']);
  });

  test('unsupported contract version fails closed to unavailable', () {
    final result = PlannerBiodiversityPrediction.fromJson({
      'contractVersion': 2,
      'destinationId': 'destination-id',
      'activityId': null,
      'status': 'available',
      'message': messages['available'],
      'limitations': 'untrusted future payload',
      'predictedSpecies': [
        {
          'speciesId': 'synthetic',
          'scientificName': 'Synthetic species',
          'commonName': 'Fixture',
          'habitatSuitability': 0.8,
          'confidenceLevel': 'synthetic-test-only',
        },
      ],
      'modelMetadata': {'modelVersion': 'future', 'inferenceTimestamp': null},
    });

    expect(result.status, 'unavailable');
    expect(result.message, messages['unavailable']);
    expect(result.limitations, messages['unavailable']);
    expect(result.predictedSpecies, isEmpty);
    expect(result.modelMetadata, isNull);
  });

  test('available status without provenance fails closed to unavailable', () {
    final result = PlannerBiodiversityPrediction.fromJson({
      'contractVersion': 1,
      'destinationId': 'destination-id',
      'activityId': null,
      'status': 'available',
      'predictedSpecies': [
        {
          'speciesId': 'synthetic',
          'scientificName': 'Synthetic species',
          'commonName': 'Fixture',
          'habitatSuitability': 0.8,
          'confidenceLevel': 'synthetic-test-only',
        },
      ],
      'modelMetadata': {
        'modelVersion': 'fixture-v1',
        'inferenceTimestamp': '2026-10-11T00:00:00Z',
      },
    });

    expect(result.status, 'unavailable');
    expect(result.message, messages['unavailable']);
    expect(result.predictedSpecies, isEmpty);
    expect(result.modelMetadata, isNull);
  });
}
