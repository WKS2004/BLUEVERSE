import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/models/experience_models.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/auth_credential_store.dart';
import 'package:mobile/data/services/experience_api_service.dart';

class MemoryCredentialStore implements AuthCredentialStore {
  final values = <String, String>{};

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async {
    values[key] = value;
  }

  @override
  Future<void> delete(String key) async {
    values.remove(key);
  }
}

void main() {
  group('ExperienceApiService Public API Contract Tests', () {
    test('getDestinations parses paged destinations correctly', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/experiences/destinations');
        expect(request.url.queryParameters['region'], 'Southern Province');

        final payload = {
          'total': 1,
          'page': 1,
          'pageSize': 10,
          'items': [
            {
              'id': 'dest-mirissa',
              'name': 'Mirissa Coastal Haven',
              'slug': 'mirissa-coastal-haven',
              'description': 'Whale watching and coastal sanctuary',
              'region': 'Southern Province',
              'latitude': 5.9482,
              'longitude': 80.4716,
              'status': 'PUBLISHED',
              'createdAt': '2026-09-01T00:00:00Z',
              'updatedAt': '2026-09-02T00:00:00Z',
            }
          ]
        };
        return http.Response(jsonEncode(payload), 200);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final result = await service.getDestinations(region: 'Southern Province');
      expect(result.total, 1);
      expect(result.items.first.name, 'Mirissa Coastal Haven');
      expect(result.items.first.latitude, 5.9482);
    });

    test('getDestinationById parses destination details', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/experiences/destinations/dest-mirissa');
        final payload = {
          'id': 'dest-mirissa',
          'name': 'Mirissa Coastal Haven',
          'slug': 'mirissa-coastal-haven',
          'description': 'Whale watching sanctuary',
          'region': 'Southern Province',
          'latitude': 5.9482,
          'longitude': 80.4716,
          'status': 'PUBLISHED',
          'createdAt': '2026-09-01T00:00:00Z',
          'updatedAt': '2026-09-02T00:00:00Z',
        };
        return http.Response(jsonEncode(payload), 200);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final dest = await service.getDestinationById('dest-mirissa');
      expect(dest.id, 'dest-mirissa');
      expect(dest.name, 'Mirissa Coastal Haven');
    });

    test('evaluateDestinationPublication returns evaluation payload', () async {
      final client = MockClient((request) async {
        expect(
          request.url.path,
          '/api/experiences/destinations/dest-mirissa/publication-evaluations',
        );
        expect(request.method, 'POST');
        final payload = {
          'targetId': 'dest-mirissa',
          'targetType': 'DESTINATION',
          'currentStatus': 'DRAFT',
          'requestedStatus': 'PUBLISHED',
          'canTransition': true,
          'reasons': ['Safety guidelines met', 'Coordinates verified'],
        };
        return http.Response(jsonEncode(payload), 200);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final eval = await service.evaluateDestinationPublication('dest-mirissa', 'PUBLISHED');
      expect(eval.canTransition, isTrue);
      expect(eval.reasons.length, 2);
    });

    test('getDestinationMarineConditions returns contextual marine data', () async {
      final client = MockClient((request) async {
        expect(
          request.url.path,
          '/api/experiences/destinations/dest-mirissa/marine-conditions',
        );
        final payload = {
          'destinationId': 'dest-mirissa',
          'destinationName': 'Mirissa Coastal Haven',
          'latitude': 5.9482,
          'longitude': 80.4716,
          'responded': true,
          'attemptsCount': 1,
          'latencyMs': 45,
          'remoteStatus': 'CONNECTED',
          'safetyLevel': 'Low Risk',
          'waterCondition': 'Calm',
          'waveHeightMeters': 0.8,
          'windSpeedKnots': 12.0,
          'fallbackUsed': false,
          'disclaimer': 'Official Marine Weather Service data.',
        };
        return http.Response(jsonEncode(payload), 200);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final marine = await service.getDestinationMarineConditions('dest-mirissa');
      expect(marine.safetyLevel, 'Low Risk');
      expect(marine.waveHeightMeters, 0.8);
    });

    test('evaluateAvailability verifies booking slots deterministically', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/experiences/availability/evaluations');
        expect(request.method, 'POST');
        final payload = {
          'offeringId': 'off-whale-watching',
          'startsAt': '2026-10-10T09:00:00Z',
          'endsAt': '2026-10-10T12:00:00Z',
          'status': 'AVAILABLE',
          'reasonCodes': ['CAPACITY_AVAILABLE', 'WEATHER_SUITABLE'],
          'offering': {
            'offeringId': 'off-whale-watching',
            'offeringTitle': 'Whale Watching Cruise',
            'destinationId': 'dest-mirissa',
            'destinationName': 'Mirissa',
            'destinationStatus': 'PUBLISHED',
            'activityId': 'act-marine',
            'activityName': 'Marine Wildlife',
            'activityStatus': 'PUBLISHED',
            'offeringStatus': 'PUBLISHED',
          },
          'evaluatedAt': '2026-10-03T02:00:00Z',
        };
        return http.Response(jsonEncode(payload), 200);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final req = AvailabilityEvaluationRequest(
        offeringId: 'off-whale-watching',
        startsAt: DateTime.utc(2026, 10, 10, 9),
        endsAt: DateTime.utc(2026, 10, 10, 12),
      );
      final res = await service.evaluateAvailability(req);
      expect(res.status, 'AVAILABLE');
      expect(res.reasonCodes, contains('CAPACITY_AVAILABLE'));
    });

    test('favourites CRUD (get, add, remove) interacts with endpoints correctly', () async {
      var added = false;
      var removed = false;

      final client = MockClient((request) async {
        if (request.method == 'GET' && request.url.path == '/api/experiences/favourites') {
          return http.Response(
            jsonEncode([
              {
                'id': 'fav-1',
                'userId': 'user-1',
                'targetType': 'DESTINATION',
                'targetId': 'dest-mirissa',
                'targetTitle': 'Mirissa Coastal Haven',
                'targetStatus': 'PUBLISHED',
                'createdAt': '2026-09-10T00:00:00Z',
              }
            ]),
            200,
          );
        }
        if (request.method == 'PUT' &&
            request.url.path == '/api/experiences/favourites/DESTINATION/dest-mirissa') {
          added = true;
          return http.Response(
            jsonEncode({
              'id': 'fav-new',
              'userId': 'user-1',
              'targetType': 'DESTINATION',
              'targetId': 'dest-mirissa',
              'targetTitle': 'Mirissa Coastal Haven',
              'createdAt': '2026-10-01T00:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'DELETE' &&
            request.url.path == '/api/experiences/favourites/DESTINATION/dest-mirissa') {
          removed = true;
          return http.Response('', 204);
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final favs = await service.getUserFavourites();
      expect(favs.length, 1);
      expect(favs.first.targetId, 'dest-mirissa');

      await service.addFavourite('DESTINATION', 'dest-mirissa');
      expect(added, isTrue);

      await service.removeFavourite('DESTINATION', 'dest-mirissa');
      expect(removed, isTrue);
    });

    test('searchMapPlaces queries /api/experiences/map/search', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/experiences/map/search');
        expect(request.url.queryParameters['q'], 'Mirissa');
        final payload = {
          'query': 'Mirissa',
          'results': [
            {
              'displayName': 'Mirissa Bay, Southern Province, Sri Lanka',
              'latitude': 5.9482,
              'longitude': 80.4716,
              'type': 'bay',
              'region': 'Southern Province',
              'country': 'Sri Lanka',
            }
          ],
          'source': 'OpenStreetMap Nominatim',
          'fallback': false,
          'retrievedAt': '2026-10-03T00:00:00Z',
        };
        return http.Response(jsonEncode(payload), 200);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final res = await service.searchMapPlaces('Mirissa');
      expect(res.results.length, 1);
      expect(res.results.first.displayName, contains('Mirissa Bay'));
    });

    test('getNearbyExperiences queries /api/experiences/nearby', () async {
      final client = MockClient((request) async {
        expect(request.url.path, '/api/experiences/nearby');
        expect(request.url.queryParameters['latitude'], '5.9482');
        expect(request.url.queryParameters['longitude'], '80.4716');
        final payload = {
          'count': 1,
          'results': [
            {
              'destinationId': 'dest-mirissa',
              'name': 'Mirissa Coastal Haven',
              'slug': 'mirissa-coastal-haven',
              'latitude': 5.9482,
              'longitude': 80.4716,
              'distanceMeters': 120.0,
              'activeOfferingsCount': 4,
            }
          ]
        };
        return http.Response(jsonEncode(payload), 200);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final res = await service.getNearbyExperiences(
        latitude: 5.9482,
        longitude: 80.4716,
      );
      expect(res.count, 1);
      expect(res.results.first.name, 'Mirissa Coastal Haven');
    });

    test('getDependenciesStatus and getAgentContext return diagnostic payloads', () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/experiences/dependencies/status') {
          return http.Response(
            jsonEncode({
              'serviceName': 'Experience & Biodiversity Service',
              'overallStatus': 'HEALTHY',
              'dependencies': [
                {
                  'serviceName': 'PostgreSQL Experience DB',
                  'status': 'HEALTHY',
                  'responded': true,
                  'latencyMs': 2,
                }
              ]
            }),
            200,
          );
        }
        if (request.url.path == '/api/experiences/agent/context') {
          return http.Response(
            jsonEncode({
              'agentName': 'Coastal Experience Planner Seam',
              'status': 'PRE_G07_SEAM_READY',
              'detail': 'Contextual seam prepared without autonomous tool execution',
              'plannedTools': ['search_experiences', 'evaluate_conditions'],
              'checkedAt': '2026-10-03T02:00:00Z',
            }),
            200,
          );
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final dep = await service.getDependenciesStatus();
      expect(dep.overallStatus, 'HEALTHY');
      expect(dep.dependencies.length, 1);

      final agent = await service.getAgentContext();
      expect(agent.status, 'PRE_G07_SEAM_READY');
      expect(agent.plannedTools.length, 2);
    });

    test('destination mutations (create, update, delete, updatePublication) succeed', () async {
      var created = false;
      var updated = false;
      var deleted = false;
      var publicationUpdated = false;

      final client = MockClient((request) async {
        if (request.method == 'POST' && request.url.path == '/api/experiences/destinations') {
          created = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          expect(body['name'], 'Trincomalee Coral Cove');
          return http.Response(
            jsonEncode({
              'id': 'dest-trinco',
              'name': body['name'],
              'slug': 'trincomalee-coral-cove',
              'description': body['description'],
              'region': body['region'],
              'latitude': body['latitude'],
              'longitude': body['longitude'],
              'status': 'DRAFT',
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T00:00:00Z',
            }),
            201,
          );
        }
        if (request.method == 'PUT' && request.url.path == '/api/experiences/destinations/dest-trinco') {
          updated = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'dest-trinco',
              'name': body['name'],
              'slug': 'trincomalee-coral-cove-updated',
              'description': body['description'],
              'region': body['region'],
              'latitude': body['latitude'],
              'longitude': body['longitude'],
              'status': 'DRAFT',
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T01:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'DELETE' && request.url.path == '/api/experiences/destinations/dest-trinco') {
          deleted = true;
          return http.Response('', 204);
        }
        if (request.method == 'PATCH' && request.url.path == '/api/experiences/destinations/dest-trinco/publication') {
          publicationUpdated = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'dest-trinco',
              'name': 'Trincomalee Coral Cove',
              'slug': 'trincomalee-coral-cove',
              'latitude': 8.5874,
              'longitude': 81.2152,
              'status': body['status'],
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T02:00:00Z',
            }),
            200,
          );
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final newDest = await service.createDestination(
        const CreateDestinationRequest(
          name: 'Trincomalee Coral Cove',
          latitude: 8.5874,
          longitude: 81.2152,
          region: 'Eastern Province',
          description: 'Pristine coral ecosystems',
        ),
      );
      expect(created, isTrue);
      expect(newDest.id, 'dest-trinco');

      final updatedDest = await service.updateDestination(
        'dest-trinco',
        const UpdateDestinationRequest(
          name: 'Trincomalee Coral Cove Updated',
          latitude: 8.5874,
          longitude: 81.2152,
          region: 'Eastern Province',
          description: 'Updated sanctuary details',
        ),
      );
      expect(updated, isTrue);
      expect(updatedDest.name, 'Trincomalee Coral Cove Updated');

      final pubDest = await service.updateDestinationPublication('dest-trinco', 'PUBLISHED');
      expect(publicationUpdated, isTrue);
      expect(pubDest.status, 'PUBLISHED');

      await service.deleteDestination('dest-trinco');
      expect(deleted, isTrue);
    });

    test('getDestinationOperationalAdvisories and getDestinationBiodiversity parse telemetry correctly', () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/experiences/destinations/dest-mirissa/operational-advisories') {
          return http.Response(
            jsonEncode({
              'destinationId': 'dest-mirissa',
              'destinationName': 'Mirissa Coastal Haven',
              'responded': true,
              'attemptsCount': 1,
              'latencyMs': 20,
              'remoteStatus': 'CONNECTED',
              'advisories': [
                {
                  'advisoryId': 'adv-1',
                  'title': 'Optimal Diving Conditions',
                  'severity': 'INFO',
                  'description': 'Clear visibility up to 15m.',
                  'issuedAt': '2026-10-04T00:00:00Z',
                }
              ],
              'fallbackUsed': false,
              'message': 'OK',
            }),
            200,
          );
        }
        if (request.url.path == '/api/experiences/destinations/dest-mirissa/biodiversity') {
          return http.Response(
            jsonEncode({
              'destinationId': 'dest-mirissa',
              'destinationName': 'Mirissa Coastal Haven',
              'latitude': 5.9482,
              'longitude': 80.4716,
              'status': 'CONNECTED',
              'predictions': [
                {
                  'speciesName': 'Blue Whale',
                  'scientificName': 'Balaenoptera musculus',
                  'conservationStatus': 'Endangered',
                  'occurrenceProbability': 0.88,
                }
              ],
              'disclaimer': 'Marine science prediction seam',
              'responded': true,
              'attemptsCount': 1,
              'latencyMs': 22,
            }),
            200,
          );
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final advisories = await service.getDestinationOperationalAdvisories('dest-mirissa');
      expect(advisories.destinationId, 'dest-mirissa');
      expect(advisories.advisories.first.title, 'Optimal Diving Conditions');

      final bio = await service.getDestinationBiodiversity('dest-mirissa');
      expect(bio.predictions.first.scientificName, 'Balaenoptera musculus');
      expect(bio.predictions.first.occurrenceProbability, 0.88);
    });

    test('activities full lifecycle (get, getById, create, update, publication, delete) succeeds', () async {
      var created = false;
      var updated = false;
      var publicationEvaluated = false;
      var publicationUpdated = false;
      var deleted = false;

      final client = MockClient((request) async {
        if (request.method == 'GET' && request.url.path == '/api/experiences/activities') {
          expect(request.url.queryParameters['category'], 'Snorkeling');
          return http.Response(
            jsonEncode({
              'total': 1,
              'page': 1,
              'pageSize': 10,
              'items': [
                {
                  'id': 'act-coral-reef',
                  'code': 'act-coral-snork',
                  'name': 'Coral Reef Snorkeling',
                  'category': 'Snorkeling',
                  'status': 'PUBLISHED',
                  'createdAt': '2026-09-01T00:00:00Z',
                  'updatedAt': '2026-09-02T00:00:00Z',
                }
              ],
            }),
            200,
          );
        }
        if (request.method == 'GET' && request.url.path == '/api/experiences/activities/act-coral-reef') {
          return http.Response(
            jsonEncode({
              'id': 'act-coral-reef',
              'code': 'act-coral-snork',
              'name': 'Coral Reef Snorkeling',
              'category': 'Snorkeling',
              'status': 'PUBLISHED',
              'createdAt': '2026-09-01T00:00:00Z',
              'updatedAt': '2026-09-02T00:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'POST' && request.url.path == '/api/experiences/activities') {
          created = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'act-new',
              'code': body['code'],
              'name': body['name'],
              'category': body['category'],
              'status': 'DRAFT',
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T00:00:00Z',
            }),
            201,
          );
        }
        if (request.method == 'PUT' && request.url.path == '/api/experiences/activities/act-new') {
          updated = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'act-new',
              'code': 'act-new-code',
              'name': body['name'],
              'category': body['category'],
              'status': 'DRAFT',
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T01:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'POST' && request.url.path == '/api/experiences/activities/act-new/publication-evaluations') {
          publicationEvaluated = true;
          return http.Response(
            jsonEncode({
              'targetId': 'act-new',
              'targetType': 'ACTIVITY',
              'currentStatus': 'DRAFT',
              'requestedStatus': 'PUBLISHED',
              'canTransition': true,
              'reasons': ['Prerequisites met'],
            }),
            200,
          );
        }
        if (request.method == 'PATCH' && request.url.path == '/api/experiences/activities/act-new/publication') {
          publicationUpdated = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'act-new',
              'code': 'act-new-code',
              'name': 'Updated Reef Snorkeling',
              'category': 'Snorkeling',
              'status': body['status'],
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T02:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'DELETE' && request.url.path == '/api/experiences/activities/act-new') {
          deleted = true;
          return http.Response('', 204);
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final list = await service.getActivities(category: 'Snorkeling');
      expect(list.items.first.code, 'act-coral-snork');

      final single = await service.getActivityById('act-coral-reef');
      expect(single.id, 'act-coral-reef');

      final createdAct = await service.createActivity(
        const CreateActivityRequest(
          code: 'act-new-code',
          name: 'New Reef Snorkeling',
          category: 'Snorkeling',
        ),
      );
      expect(created, isTrue);
      expect(createdAct.id, 'act-new');

      final updatedAct = await service.updateActivity(
        'act-new',
        const UpdateActivityRequest(
          name: 'Updated Reef Snorkeling',
          category: 'Snorkeling',
        ),
      );
      expect(updated, isTrue);
      expect(updatedAct.name, 'Updated Reef Snorkeling');

      final eval = await service.evaluateActivityPublication('act-new', 'PUBLISHED');
      expect(publicationEvaluated, isTrue);
      expect(eval.canTransition, isTrue);

      final pubAct = await service.updateActivityPublication('act-new', 'PUBLISHED');
      expect(publicationUpdated, isTrue);
      expect(pubAct.status, 'PUBLISHED');

      await service.deleteActivity('act-new');
      expect(deleted, isTrue);
    });

    test('offerings and schedules full lifecycle succeeds', () async {
      var createdOff = false;
      var updatedOff = false;
      var evalOff = false;
      var pubOff = false;
      var deletedOff = false;
      var addedSched = false;
      var updatedSched = false;
      var deletedSched = false;

      final client = MockClient((request) async {
        if (request.method == 'GET' && request.url.path == '/api/experiences/offerings') {
          expect(request.url.queryParameters['activityId'], 'act-whale');
          return http.Response(
            jsonEncode({
              'total': 1,
              'page': 1,
              'pageSize': 10,
              'items': [
                {
                  'id': 'off-whale-safari',
                  'destinationId': 'dest-mirissa',
                  'destinationName': 'Mirissa',
                  'activityId': 'act-whale',
                  'activityName': 'Whale Watching',
                  'activityCode': 'act-whale-watching',
                  'title': 'Whale Watching Safari',
                  'price': 15000.0,
                  'currency': 'LKR',
                  'durationMinutes': 180,
                  'maxCapacity': 12,
                  'status': 'PUBLISHED',
                  'createdAt': '2026-09-01T00:00:00Z',
                  'updatedAt': '2026-09-02T00:00:00Z',
                }
              ],
            }),
            200,
          );
        }
        if (request.method == 'GET' && request.url.path == '/api/experiences/offerings/off-whale-safari') {
          return http.Response(
            jsonEncode({
              'id': 'off-whale-safari',
              'destinationId': 'dest-mirissa',
              'destinationName': 'Mirissa',
              'activityId': 'act-whale',
              'activityName': 'Whale Watching',
              'activityCode': 'act-whale-watching',
              'title': 'Whale Watching Safari',
              'status': 'PUBLISHED',
              'createdAt': '2026-09-01T00:00:00Z',
              'updatedAt': '2026-09-02T00:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'POST' && request.url.path == '/api/experiences/offerings') {
          createdOff = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'off-new',
              'destinationId': body['destinationId'],
              'destinationName': 'Mirissa',
              'activityId': body['activityId'],
              'activityName': 'Whale Watching',
              'activityCode': 'act-whale-watching',
              'title': body['title'],
              'price': body['price'],
              'currency': body['currency'],
              'status': 'DRAFT',
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T00:00:00Z',
            }),
            201,
          );
        }
        if (request.method == 'PUT' && request.url.path == '/api/experiences/offerings/off-new') {
          updatedOff = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'off-new',
              'destinationId': 'dest-mirissa',
              'destinationName': 'Mirissa',
              'activityId': 'act-whale',
              'activityName': 'Whale Watching',
              'activityCode': 'act-whale-watching',
              'title': body['title'],
              'price': body['price'],
              'currency': body['currency'],
              'status': 'DRAFT',
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T01:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'POST' && request.url.path == '/api/experiences/offerings/off-new/publication-evaluations') {
          evalOff = true;
          return http.Response(
            jsonEncode({
              'targetId': 'off-new',
              'targetType': 'OFFERING',
              'currentStatus': 'DRAFT',
              'requestedStatus': 'PUBLISHED',
              'canTransition': true,
              'reasons': ['Pricing and capacity validated'],
            }),
            200,
          );
        }
        if (request.method == 'PATCH' && request.url.path == '/api/experiences/offerings/off-new/publication') {
          pubOff = true;
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'off-new',
              'destinationId': 'dest-mirissa',
              'destinationName': 'Mirissa',
              'activityId': 'act-whale',
              'activityName': 'Whale Watching',
              'activityCode': 'act-whale-watching',
              'title': 'Sunset Cruise',
              'status': body['status'],
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T02:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'DELETE' && request.url.path == '/api/experiences/offerings/off-new') {
          deletedOff = true;
          return http.Response('', 204);
        }
        // Schedules
        if (request.method == 'GET' && request.url.path == '/api/experiences/offerings/off-new/schedules') {
          return http.Response(
            jsonEncode([
              {
                'id': 'sched-1',
                'offeringId': 'off-new',
                'startsAt': '2026-10-10T06:00:00Z',
                'endsAt': '2026-10-10T09:00:00Z',
                'timeZoneId': 'Asia/Colombo',
                'isActive': true,
                'createdAt': '2026-10-04T00:00:00Z',
                'updatedAt': '2026-10-04T00:00:00Z',
              }
            ]),
            200,
          );
        }
        if (request.method == 'POST' && request.url.path == '/api/experiences/offerings/off-new/schedules') {
          addedSched = true;
          return http.Response(
            jsonEncode({
              'id': 'sched-2',
              'offeringId': 'off-new',
              'startsAt': '2026-10-11T06:00:00Z',
              'endsAt': '2026-10-11T09:00:00Z',
              'timeZoneId': 'Asia/Colombo',
              'isActive': true,
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T00:00:00Z',
            }),
            201,
          );
        }
        if (request.method == 'PUT' && request.url.path == '/api/experiences/offerings/off-new/schedules/sched-2') {
          updatedSched = true;
          return http.Response(
            jsonEncode({
              'id': 'sched-2',
              'offeringId': 'off-new',
              'startsAt': '2026-10-11T07:00:00Z',
              'endsAt': '2026-10-11T10:00:00Z',
              'timeZoneId': 'Asia/Colombo',
              'isActive': true,
              'createdAt': '2026-10-04T00:00:00Z',
              'updatedAt': '2026-10-04T01:00:00Z',
            }),
            200,
          );
        }
        if (request.method == 'DELETE' && request.url.path == '/api/experiences/offerings/off-new/schedules/sched-2') {
          deletedSched = true;
          return http.Response('', 204);
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: MemoryCredentialStore(),
      );

      final offerings = await service.getOfferings(activityId: 'act-whale');
      expect(offerings.items.first.title, 'Whale Watching Safari');

      final singleOff = await service.getOfferingById('off-whale-safari');
      expect(singleOff.id, 'off-whale-safari');

      final created = await service.createOffering(
        const CreateOfferingRequest(
          destinationId: 'dest-mirissa',
          activityId: 'act-whale',
          title: 'Sunset Cruise',
          price: 18000,
          currency: 'LKR',
        ),
      );
      expect(createdOff, isTrue);
      expect(created.id, 'off-new');

      final updated = await service.updateOffering(
        'off-new',
        const UpdateOfferingRequest(
          title: 'Sunset Cruise Premium',
          price: 20000,
          currency: 'LKR',
        ),
      );
      expect(updatedOff, isTrue);
      expect(updated.title, 'Sunset Cruise Premium');

      final eval = await service.evaluateOfferingPublication('off-new', 'PUBLISHED');
      expect(evalOff, isTrue);
      expect(eval.canTransition, isTrue);

      final pub = await service.updateOfferingPublication('off-new', 'PUBLISHED');
      expect(pubOff, isTrue);
      expect(pub.status, 'PUBLISHED');

      // Schedules test
      final scheds = await service.getOfferingSchedules('off-new');
      expect(scheds.length, 1);
      expect(scheds.first.id, 'sched-1');

      final newSched = await service.addOfferingSchedule(
        'off-new',
        CreateScheduleRequest(
          startsAt: DateTime.utc(2026, 10, 11, 6),
          endsAt: DateTime.utc(2026, 10, 11, 9),
        ),
      );
      expect(addedSched, isTrue);
      expect(newSched.id, 'sched-2');

      final updatedSchedRes = await service.updateOfferingSchedule(
        'off-new',
        'sched-2',
        UpdateScheduleRequest(
          startsAt: DateTime.utc(2026, 10, 11, 7),
          endsAt: DateTime.utc(2026, 10, 11, 10),
        ),
      );
      expect(updatedSched, isTrue);
      expect(updatedSchedRes.id, 'sched-2');

      await service.deleteOfferingSchedule('off-new', 'sched-2');
      expect(deletedSched, isTrue);

      await service.deleteOffering('off-new');
      expect(deletedOff, isTrue);
    });

    test('error handling throws ExperienceApiException on 400, 401, 403, 404, 500', () async {
      for (final statusCode in [400, 401, 403, 404, 500]) {
        final client = MockClient((request) async {
          return http.Response(
            jsonEncode({'message': 'Operation failed with code $statusCode'}),
            statusCode,
          );
        });

        final service = ExperienceApiService(
          client: client,
          storage: MemoryCredentialStore(),
        );

        expect(
          () => service.getDestinationById('dest-nonexistent'),
          throwsA(
            isA<ExperienceApiException>()
                .having((e) => e.statusCode, 'statusCode', statusCode)
                .having((e) => e.message, 'message', contains('$statusCode')),
          ),
        );
      }
    });
  });
}

