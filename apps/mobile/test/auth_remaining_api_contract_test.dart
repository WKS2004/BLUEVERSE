import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/marine_api_client.dart';
import 'package:mobile/data/services/marine_service.dart';
import 'package:mobile/data/repositories/marine_repository.dart';
void main() {
  group('MOB-MARINE-003', () {
    test('marine service delegates to the repository and preserves the result',
        () async {
      final repository = MarineRepository(client: _dummyMarineClient());
      final service = MarineService(repository: repository);

      final snapshot = await service.currentConditions(
        latitude: 6.025,
        longitude: 80.216,
      );
      expect(snapshot.latitude, 6.025);
      expect(snapshot.longitude, 80.216);
      expect(service.referenceActivities, isA<List<Map<String, dynamic>>>()
          .having((list) => list.length, 'length', 5));
    });

    test('suitability evaluation targets the reference activity for the supplied location',
        () async {
      final repository = MarineRepository(client: _dummyMarineClient());
      final service = MarineService(repository: repository);

      final result = await service.evaluateSuitability(
        activityId: '33333333-3333-3333-3333-333333333301',
        latitude: 6.025,
        longitude: 80.216,
      );
      expect(result.isSuitable, isTrue);
      expect(result.activityName, 'Surfing');
    });

    test('history request applies optional time-window filters', () async {
      final repository = MarineRepository(client: _dummyMarineClient());
      final service = MarineService(repository: repository);

      final snapshots = await service.history(
        latitude: 6.025,
        longitude: 80.216,
        fromUtc: DateTime.utc(2026, 9, 26),
        toUtc: DateTime.utc(2026, 9, 26, 12),
      );
      expect(snapshots, isEmpty);
    });
  });

  group('MOB-AUTH-014 auth registration contract', () {
    test('API client maps the gateway paths and serializes JSON bodies', () async {
      late http.Request request;
      final client = MockClient((incoming) async {
        request = incoming;
        // ignore: avoid_print
        print('AUTH-REQ url: ' + incoming.url.toString());
        if (incoming.url.path == '/api/marine/current') {
          return http.Response(
            jsonEncode({
              'id': '00000000-0000-0000-0000-000000000001',
              'latitude': 6.025,
              'longitude': 80.216,
              'forecastTime': '2026-09-26T10:00:00Z',
              'retrievedAt': '2026-09-26T10:05:00Z',
              'windSpeed': 15.2,
              'waveHeight': 0.8,
              'swellHeight': 0.7,
              'rain': 0.0,
              'weatherCode': 200,
              'source': 'Open-Meteo',
              'freshnessStatus': 'FRESH',
              'missingFields': [],
            }),
            200,
          );
        }
        if (incoming.url.path == '/api/marine/evaluate') {
          return http.Response(
            jsonEncode({
              'status': 'SUITABLE',
              'activityId': '33333333-3333-3333-3333-333333333301',
              'activityName': 'Surfing',
              'location': {'latitude': 6.025, 'longitude': 80.216},
              'requestedTime': '2026-09-26T10:00:00Z',
              'evaluatedAt': '2026-09-26T10:06:00Z',
              'source': 'Open-Meteo',
              'retrievedAt': '2026-09-26T10:06:30Z',
              'freshness': 'FRESH',
              'missingFields': [],
              'violations': [],
              'cautionFactors': [],
              'assessmentId': '11111111-1111-1111-1111-111111111111',
              'snapshotId': '22222222-2222-2222-2222-222222222222',
            }),
            200,
          );
        }
        return http.Response('{}', 404);
      });
      addTearDown(client.close);

      final api = MarineApiClient(client: client, gateway: const ApiGatewayConfig());

      final snapshot = await api.getConditions(
        latitude: 6.025,
        longitude: 80.216,
      );
      expect(snapshot.latitude, 6.025);
      expect(snapshot.longitude, 80.216);
      expect(request.url.path, '/api/marine/current');
      expect(request.method, 'GET');
      expect(request.url.queryParameters, {'latitude': '6.02500', 'longitude': '80.21600'});

      final result = await api.evaluateSuitability(
        activityId: '33333333-3333-3333-3333-333333333301',
        latitude: 6.025,
        longitude: 80.216,
      );
      expect(result.isSuitable, isTrue);
      expect(request.url.path, '/api/marine/evaluate');
      expect(request.method, 'POST');

      // MOB-AUTH-014: evaluateSuitability serializes its parameters into the
      // POST body (activityId, latitude, longitude), not the URL query string.
      expect(jsonDecode(request.body), {
        'activityId': '33333333-3333-3333-3333-333333333301',
        'latitude': '6.02500',
        'longitude': '80.21600',
      });
    });
  });
}

MarineApiClient _dummyMarineClient() {
  final client = MockClient((incoming) async {
    if (incoming.url.path == '/api/marine/current') {
      return http.Response(
        jsonEncode({
          'id': '00000000-0000-0000-0000-000000000001',
          'latitude': 6.025,
          'longitude': 80.216,
          'forecastTime': '2026-09-26T10:00:00Z',
          'retrievedAt': '2026-09-26T10:05:00Z',
          'windSpeed': 15.2,
          'waveHeight': 0.8,
          'swellHeight': 0.7,
          'rain': 0.0,
          'weatherCode': 200,
          'source': 'Open-Meteo',
          'freshnessStatus': 'FRESH',
          'missingFields': [],
        }),
        200,
      );
    }
    if (incoming.url.path == '/api/marine/evaluate') {
      return http.Response(
        jsonEncode({
          'status': 'SUITABLE',
          'activityId': '33333333-3333-3333-3333-333333333301',
          'activityName': 'Surfing',
          'location': {'latitude': 6.025, 'longitude': 80.216},
          'requestedTime': '2026-09-26T10:00:00Z',
          'evaluatedAt': '2026-09-26T10:06:00Z',
          'source': 'Open-Meteo',
          'retrievedAt': '2026-09-26T10:06:30Z',
          'freshness': 'FRESH',
          'missingFields': [],
          'violations': [],
          'cautionFactors': [],
          'assessmentId': '11111111-1111-1111-1111-111111111111',
          'snapshotId': '22222222-2222-2222-2222-222222222222',
        }),
        200,
      );
    }
    if (incoming.url.path == '/api/marine/history') {
      return http.Response(
        jsonEncode([]),
        200,
      );
    }
    return http.Response('{}', 404);
  });
  return MarineApiClient(client: client, gateway: const ApiGatewayConfig());
}
