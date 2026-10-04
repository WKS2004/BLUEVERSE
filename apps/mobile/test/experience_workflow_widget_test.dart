import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/models/experience_models.dart';
import 'package:mobile/data/repositories/experience_repository.dart';
import 'package:mobile/data/services/auth_credential_store.dart';
import 'package:mobile/data/services/experience_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/experiences/catalogue_management_screen.dart';
import 'package:mobile/ui/experiences/destination_detail_screen.dart';
import 'package:mobile/ui/experiences/experience_view_model.dart';
import 'package:mobile/ui/experiences/experiences_discovery_screen.dart';
import 'package:mobile/ui/experiences/favourites_screen.dart';
import 'package:mobile/ui/experiences/offering_detail_screen.dart';

import 'support/mobile_test_support.dart';

class TestCredentialStore implements AuthCredentialStore {
  final values = <String, String>{};
  @override
  Future<String?> read(String key) async => values[key];
  @override
  Future<void> write(String key, String value) async => values[key] = value;
  @override
  Future<void> delete(String key) async => values.remove(key);
}

ExperienceViewModel createTestViewModel({
  List<DestinationDto>? destinations,
  List<OfferingDto>? offerings,
  List<FavouriteDto>? favourites,
  DestinationDto? destinationDetail,
  OfferingDto? offeringDetail,
}) {
  final client = MockClient((request) async {
    final path = request.url.path;

    if (path == '/api/experiences/destinations' && request.method == 'GET') {
      return http.Response(
        '''
        {
          "total": ${destinations?.length ?? 1},
          "page": 1,
          "pageSize": 50,
          "items": [
            {
              "id": "dest-mirissa",
              "name": "Mirissa Coastal Haven",
              "slug": "mirissa-coastal-haven",
              "description": "Marine wildlife and whale migration spot",
              "region": "Southern Province",
              "latitude": 5.9482,
              "longitude": 80.4716,
              "status": "PUBLISHED",
              "createdAt": "2026-09-01T00:00:00Z",
              "updatedAt": "2026-09-02T00:00:00Z"
            }
          ]
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/activities' && request.method == 'GET') {
      return http.Response(
        '''
        {
          "total": 1,
          "page": 1,
          "pageSize": 50,
          "items": [
            {
              "id": "act-whale",
              "code": "act-whale-watching",
              "name": "Whale Watching Expedition",
              "category": "Marine Life",
              "status": "PUBLISHED",
              "createdAt": "2026-09-01T00:00:00Z",
              "updatedAt": "2026-09-02T00:00:00Z"
            }
          ]
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/offerings' && request.method == 'GET') {
      return http.Response(
        '''
        {
          "total": 1,
          "page": 1,
          "pageSize": 50,
          "items": [
            {
              "id": "off-whale-safari",
              "destinationId": "dest-mirissa",
              "destinationName": "Mirissa Coastal Haven",
              "activityId": "act-whale",
              "activityName": "Whale Watching Expedition",
              "activityCode": "act-whale-watching",
              "title": "Sunrise Blue Whale Safari",
              "description": "Morning ethical boat expedition",
              "price": 12500,
              "currency": "LKR",
              "durationMinutes": 240,
              "maxCapacity": 8,
              "status": "PUBLISHED",
              "createdAt": "2026-09-01T00:00:00Z",
              "updatedAt": "2026-09-02T00:00:00Z"
            }
          ]
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/map/config') {
      return http.Response(
        '''
        {
          "provider": "OpenStreetMap",
          "tileServiceType": "vector",
          "vectorTileUrl": "https://tile.openstreetmap.org",
          "availableStyles": {"standard": "osm-standard"},
          "defaultStyle": "standard",
          "attribution": "© OpenStreetMap",
          "documentationUrl": "https://osm.org"
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/favourites' && request.method == 'GET') {
      return http.Response(
        '''
        [
          {
            "id": "fav-1",
            "userId": "user-current",
            "targetType": "DESTINATION",
            "targetId": "dest-mirissa",
            "targetTitle": "Mirissa Coastal Haven",
            "targetStatus": "PUBLISHED",
            "createdAt": "2026-09-10T00:00:00Z"
          }
        ]
        ''',
        200,
      );
    }

    if (path == '/api/experiences/destinations/dest-mirissa' && request.method == 'GET') {
      return http.Response(
        '''
        {
          "id": "dest-mirissa",
          "name": "Mirissa Coastal Haven",
          "slug": "mirissa-coastal-haven",
          "description": "Marine wildlife and whale migration spot",
          "region": "Southern Province",
          "latitude": 5.9482,
          "longitude": 80.4716,
          "status": "PUBLISHED",
          "createdAt": "2026-09-01T00:00:00Z",
          "updatedAt": "2026-09-02T00:00:00Z"
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/destinations/dest-mirissa/marine-conditions') {
      return http.Response(
        '''
        {
          "destinationId": "dest-mirissa",
          "destinationName": "Mirissa Coastal Haven",
          "latitude": 5.9482,
          "longitude": 80.4716,
          "responded": true,
          "attemptsCount": 1,
          "latencyMs": 30,
          "remoteStatus": "CONNECTED",
          "safetyLevel": "Low Risk",
          "waterCondition": "Calm Waters",
          "waveHeightMeters": 0.8,
          "windSpeedKnots": 10.0,
          "fallbackUsed": false,
          "disclaimer": "Coastal safety verified."
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/destinations/dest-mirissa/biodiversity') {
      return http.Response(
        '''
        {
          "destinationId": "dest-mirissa",
          "destinationName": "Mirissa Coastal Haven",
          "latitude": 5.9482,
          "longitude": 80.4716,
          "status": "CONNECTED",
          "predictions": [
            {
              "speciesName": "Blue Whale (Balaenoptera musculus)",
              "scientificName": "Balaenoptera musculus",
              "conservationStatus": "Endangered",
              "occurrenceProbability": 0.85
            }
          ],
          "disclaimer": "Marine biodiversity models.",
          "responded": true,
          "attemptsCount": 1,
          "latencyMs": 25
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/offerings/off-whale-safari' && request.method == 'GET') {
      return http.Response(
        '''
        {
          "id": "off-whale-safari",
          "destinationId": "dest-mirissa",
          "destinationName": "Mirissa Coastal Haven",
          "activityId": "act-whale",
          "activityName": "Whale Watching Expedition",
          "activityCode": "act-whale-watching",
          "title": "Sunrise Blue Whale Safari",
          "description": "Morning ethical boat expedition",
          "price": 12500,
          "currency": "LKR",
          "durationMinutes": 240,
          "maxCapacity": 8,
          "status": "PUBLISHED",
          "createdAt": "2026-09-01T00:00:00Z",
          "updatedAt": "2026-09-02T00:00:00Z"
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/offerings/off-whale-safari/schedules') {
      return http.Response(
        '''
        [
          {
            "id": "sched-1",
            "offeringId": "off-whale-safari",
            "startsAt": "2026-10-10T06:00:00Z",
            "endsAt": "2026-10-10T10:00:00Z",
            "timeZoneId": "Asia/Colombo",
            "isActive": true,
            "createdAt": "2026-09-01T00:00:00Z",
            "updatedAt": "2026-09-02T00:00:00Z"
          }
        ]
        ''',
        200,
      );
    }

    if (path == '/api/experiences/availability/evaluations' && request.method == 'POST') {
      return http.Response(
        '''
        {
          "offeringId": "off-whale-safari",
          "startsAt": "2026-10-10T06:00:00Z",
          "endsAt": "2026-10-10T10:00:00Z",
          "status": "AVAILABLE",
          "reasonCodes": ["SLOT_AVAILABLE", "WEATHER_OPTIMAL"],
          "offering": {
            "offeringId": "off-whale-safari",
            "offeringTitle": "Sunrise Blue Whale Safari",
            "destinationId": "dest-mirissa",
            "destinationName": "Mirissa Coastal Haven",
            "destinationStatus": "PUBLISHED",
            "activityId": "act-whale",
            "activityName": "Whale Watching",
            "activityStatus": "PUBLISHED",
            "offeringStatus": "PUBLISHED"
          },
          "evaluatedAt": "2026-10-03T02:00:00Z"
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/dependencies/status') {
      return http.Response(
        '''
        {
          "serviceName": "Experience Service",
          "overallStatus": "HEALTHY",
          "dependencies": [
            {
              "serviceName": "PostgreSQL",
              "status": "HEALTHY",
              "responded": true,
              "latencyMs": 2
            }
          ]
        }
        ''',
        200,
      );
    }

    if (path == '/api/experiences/agent/context') {
      return http.Response(
        '''
        {
          "agentName": "Experience Planner Seam",
          "status": "PRE_G07_SEAM_READY",
          "detail": "Read-only context",
          "plannedTools": ["discovery"],
          "checkedAt": "2026-10-03T02:00:00Z"
        }
        ''',
        200,
      );
    }

    return http.Response('Not Found', 404);
  });

  final apiService = ExperienceApiService(
    client: client,
    storage: TestCredentialStore(),
  );
  return ExperienceViewModel(repository: ExperienceRepository(apiService: apiService));
}

void main() {
  group('MOB-EXP Experience Discovery and Exploration Workflow Tests', () {
    testWidgets('MOB-EXP-001 discovery screen renders destinations and offerings cards', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: ExperiencesDiscoveryScreen(viewModel: vm),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Coastal Experiences'), findsOneWidget);
      expect(find.text('Mirissa Coastal Haven'), findsOneWidget);
      expect(find.text('Sunrise Blue Whale Safari'), findsOneWidget);
      expect(find.text('LKR 12500 / person'), findsOneWidget);
      expect(find.byKey(const Key('input-experience-search')), findsOneWidget);
    });

    testWidgets('MOB-EXP-002 coastal map tab displays map container and nearby triggers', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: ExperiencesDiscoveryScreen(viewModel: vm, initialTab: 1),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('OpenStreetMap Coastal View'), findsOneWidget);
      expect(find.text('Nearby Coastal Hotspots'), findsOneWidget);
      expect(find.byKey(const Key('input-map-place-search')), findsOneWidget);
    });

    testWidgets('MOB-EXP-003 destination detail screen renders conditions and biodiversity telemetry', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: DestinationDetailScreen(
            destinationId: 'dest-mirissa',
            viewModel: vm,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Mirissa Coastal Haven'), findsNWidgets(2));
      expect(find.text('Live Marine Conditions'), findsOneWidget);
      expect(find.text('0.8 m'), findsOneWidget);
      expect(find.text('Calm Waters'), findsOneWidget);
      expect(find.text('Biodiversity & Conservation'), findsOneWidget);
      expect(find.textContaining('Balaenoptera musculus'), findsOneWidget);
    });

    testWidgets('MOB-EXP-004 offering detail screen performs live availability evaluation', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: OfferingDetailScreen(
            offeringId: 'off-whale-safari',
            viewModel: vm,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Sunrise Blue Whale Safari'), findsNWidgets(2));
      expect(find.text('Upcoming Schedule Windows (1)'), findsOneWidget);

      final evalBtn = find.byKey(const Key('btn-evaluate-availability'));
      expect(evalBtn, findsOneWidget);

      await tester.ensureVisible(evalBtn);
      await tester.pumpAndSettle();

      await tester.tap(evalBtn);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('box-availability-result')), findsOneWidget);
      expect(find.text('Available for Booking'), findsOneWidget);
    });

    testWidgets('MOB-EXP-005 favourites screen renders saved wishlist items', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: FavouritesScreen(viewModel: vm),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Saved Wishlist'), findsOneWidget);
      expect(find.text('Mirissa Coastal Haven'), findsOneWidget);
    });

    testWidgets('MOB-EXP-006 catalogue management screen shows tabs for authorized managers', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      final user = mobileTestUser(
        permissions: const ['experiences.catalogue.manage'],
      );
      final authRepo = FakeAuthRepository(user: user);
      addTearDown(authRepo.close);
      final authVm = AuthViewModel(repository: authRepo)..user = user;
      addTearDown(authVm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: CatalogueManagementScreen(
            viewModel: vm,
            authViewModel: authVm,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Catalogue Management'), findsOneWidget);
      expect(find.text('Destinations'), findsOneWidget);
      expect(find.text('Activities'), findsOneWidget);
      expect(find.text('Offerings'), findsOneWidget);
      expect(find.text('Diagnostics & Seam'), findsOneWidget);
      expect(find.byKey(const Key('btn-create-destination-dialog')), findsOneWidget);
    });

    testWidgets('MOB-EXP-007 discovery search input and filter chips trigger reload', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: ExperiencesDiscoveryScreen(viewModel: vm),
        ),
      );
      await tester.pumpAndSettle();

      final searchInput = find.byKey(const Key('input-experience-search'));
      expect(searchInput, findsOneWidget);

      await tester.enterText(searchInput, 'Coral');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();

      expect(vm.searchQuery, 'Coral');

      // Tap on a Region chip via widget type and text
      final southernFilterChip = find.widgetWithText(FilterChip, 'Southern Province');
      expect(southernFilterChip, findsOneWidget);
      await tester.tap(southernFilterChip);
      await tester.pumpAndSettle();

      expect(vm.selectedRegion, 'Southern Province');

      // Tap on a Category chip via ChoiceChip
      final marineChoiceChip = find.widgetWithText(ChoiceChip, 'Marine Life');
      expect(marineChoiceChip, findsOneWidget);
      await tester.tap(marineChoiceChip);
      await tester.pumpAndSettle();

      expect(vm.selectedCategory, 'Marine Life');
    });

    testWidgets('MOB-EXP-008 coastal map place search and nearby triggers run smoothly', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: ExperiencesDiscoveryScreen(viewModel: vm, initialTab: 1),
        ),
      );
      await tester.pumpAndSettle();

      final placeSearch = find.byKey(const Key('input-map-place-search'));
      expect(placeSearch, findsOneWidget);

      await tester.enterText(placeSearch, 'Mirissa');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();

      // Trigger "Near Me"
      final nearMeBtn = find.text('Near Me');
      expect(nearMeBtn, findsOneWidget);
      await tester.tap(nearMeBtn);
      await tester.pumpAndSettle();
    });

    testWidgets('MOB-EXP-009 destination detail handles missing marine conditions gracefully', (tester) async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/experiences/destinations/dest-empty' && request.method == 'GET') {
          return http.Response(
            '''
            {
              "id": "dest-empty",
              "name": "Isolated Sandy Shore",
              "slug": "isolated-sandy-shore",
              "region": "Northern Province",
              "latitude": 9.6615,
              "longitude": 80.0255,
              "status": "PUBLISHED",
              "createdAt": "2026-09-01T00:00:00Z",
              "updatedAt": "2026-09-02T00:00:00Z"
            }
            ''',
            200,
          );
        }
        if (request.url.path == '/api/experiences/offerings') {
          return http.Response(
            '''
            {
              "total": 0,
              "page": 1,
              "pageSize": 20,
              "items": []
            }
            ''',
            200,
          );
        }
        if (request.url.path.contains('/marine-conditions')) {
          return http.Response('Internal Error', 500);
        }
        if (request.url.path.contains('/biodiversity')) {
          return http.Response(
            '''
            {
              "destinationId": "dest-empty",
              "destinationName": "Isolated Sandy Shore",
              "latitude": 9.6615,
              "longitude": 80.0255,
              "status": "NOT_CONNECTED",
              "predictions": [],
              "disclaimer": "No telemetry",
              "responded": false,
              "attemptsCount": 0,
              "latencyMs": 0
            }
            ''',
            200,
          );
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: TestCredentialStore(),
      );
      final vm = ExperienceViewModel(repository: ExperienceRepository(apiService: service));
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: DestinationDetailScreen(
            destinationId: 'dest-empty',
            viewModel: vm,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Isolated Sandy Shore'), findsNWidgets(2));
      expect(find.text('Live Marine Conditions'), findsOneWidget);
      expect(find.text('Live marine sensor conditions currently calibrating.'), findsOneWidget);
      expect(find.text('Species Observed: 0'), findsOneWidget);
    });

    testWidgets('MOB-EXP-010 offering detail handles unavailable or restricted slots', (tester) async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/experiences/offerings/off-whale-safari' && request.method == 'GET') {
          return http.Response(
            '''
            {
              "id": "off-whale-safari",
              "destinationId": "dest-mirissa",
              "destinationName": "Mirissa Coastal Haven",
              "activityId": "act-whale",
              "activityName": "Whale Watching",
              "activityCode": "act-whale-watching",
              "title": "Sunrise Blue Whale Safari",
              "price": 12500,
              "currency": "LKR",
              "status": "PUBLISHED",
              "createdAt": "2026-09-01T00:00:00Z",
              "updatedAt": "2026-09-02T00:00:00Z"
            }
            ''',
            200,
          );
        }
        if (request.url.path == '/api/experiences/offerings/off-whale-safari/schedules') {
          return http.Response('[]', 200);
        }
        if (request.url.path == '/api/experiences/availability/evaluations' && request.method == 'POST') {
          return http.Response(
            '''
            {
              "offeringId": "off-whale-safari",
              "startsAt": "2026-10-10T06:00:00Z",
              "endsAt": "2026-10-10T10:00:00Z",
              "status": "UNAVAILABLE",
              "reasonCodes": ["HIGH_SWELL_WARNING", "CAPACITY_FULL"],
              "offering": {
                "offeringId": "off-whale-safari",
                "offeringTitle": "Sunrise Blue Whale Safari",
                "destinationId": "dest-mirissa",
                "destinationName": "Mirissa Coastal Haven",
                "destinationStatus": "PUBLISHED",
                "activityId": "act-whale",
                "activityName": "Whale Watching",
                "activityStatus": "PUBLISHED",
                "offeringStatus": "PUBLISHED"
              },
              "evaluatedAt": "2026-10-03T02:00:00Z"
            }
            ''',
            200,
          );
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: TestCredentialStore(),
      );
      final vm = ExperienceViewModel(repository: ExperienceRepository(apiService: service));
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: OfferingDetailScreen(
            offeringId: 'off-whale-safari',
            viewModel: vm,
          ),
        ),
      );
      await tester.pumpAndSettle();

      final evalBtn = find.byKey(const Key('btn-evaluate-availability'));
      await tester.ensureVisible(evalBtn);
      await tester.tap(evalBtn);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('box-availability-result')), findsOneWidget);
      expect(find.text('Status: UNAVAILABLE'), findsOneWidget);
      expect(find.textContaining('HIGH_SWELL_WARNING'), findsOneWidget);
    });

    testWidgets('MOB-EXP-011 favourites screen supports filter chips and item removal', (tester) async {
      final client = MockClient((request) async {
        if (request.method == 'GET' && request.url.path == '/api/experiences/favourites') {
          return http.Response(
            '''
            [
              {
                "id": "fav-1",
                "userId": "user-current",
                "targetType": "DESTINATION",
                "targetId": "dest-mirissa",
                "targetTitle": "Mirissa Coastal Haven",
                "targetStatus": "PUBLISHED",
                "createdAt": "2026-09-10T00:00:00Z"
              },
              {
                "id": "fav-2",
                "userId": "user-current",
                "targetType": "OFFERING",
                "targetId": "off-whale-safari",
                "targetTitle": "Sunrise Blue Whale Safari",
                "targetStatus": "PUBLISHED",
                "createdAt": "2026-09-11T00:00:00Z"
              }
            ]
            ''',
            200,
          );
        }
        if (request.method == 'DELETE' &&
            request.url.path == '/api/experiences/favourites/DESTINATION/dest-mirissa') {
          return http.Response('', 204);
        }
        return http.Response('Not Found', 404);
      });

      final service = ExperienceApiService(
        client: client,
        storage: TestCredentialStore(),
      );
      final vm = ExperienceViewModel(repository: ExperienceRepository(apiService: service));
      addTearDown(vm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: FavouritesScreen(viewModel: vm),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Mirissa Coastal Haven'), findsOneWidget);
      expect(find.text('Sunrise Blue Whale Safari'), findsOneWidget);

      // Filter by Offerings only
      final offeringsFilter = find.text('Offerings');
      expect(offeringsFilter, findsOneWidget);
      await tester.tap(offeringsFilter);
      await tester.pumpAndSettle();

      expect(find.text('Sunrise Blue Whale Safari'), findsOneWidget);
      expect(find.text('Mirissa Coastal Haven'), findsNothing);

      // Back to All
      await tester.tap(find.text('All Saved (2)'));
      await tester.pumpAndSettle();

      // Remove destination
      final removeBtn = find.byKey(const Key('btn-remove-favourite-fav-1'));
      expect(removeBtn, findsOneWidget);
      await tester.tap(removeBtn);
      await tester.pumpAndSettle();

      // Verifies destination is removed from active favourites in view model
      expect(vm.isFavourite('DESTINATION', 'dest-mirissa'), isFalse);
    });

    testWidgets('MOB-EXP-012 catalogue management screen shows access denied without permission', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      // User lacking experiences.catalogue.manage
      final unprivilegedUser = mobileTestUser(permissions: const ['experiences.view']);
      final authRepo = FakeAuthRepository(user: unprivilegedUser);
      addTearDown(authRepo.close);
      final authVm = AuthViewModel(repository: authRepo)..user = unprivilegedUser;
      addTearDown(authVm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: CatalogueManagementScreen(
            viewModel: vm,
            authViewModel: authVm,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Catalogue Access Restricted'), findsOneWidget);
      expect(find.textContaining('Managing coastal catalog entries requires'), findsOneWidget);
      expect(find.byKey(const Key('btn-create-destination-dialog')), findsNothing);
    });

    testWidgets('MOB-EXP-013 catalogue management destination creation dialog renders form', (tester) async {
      final vm = createTestViewModel();
      addTearDown(vm.dispose);

      final user = mobileTestUser(
        permissions: const ['experiences.catalogue.manage'],
      );
      final authRepo = FakeAuthRepository(user: user);
      addTearDown(authRepo.close);
      final authVm = AuthViewModel(repository: authRepo)..user = user;
      addTearDown(authVm.dispose);

      await tester.pumpWidget(
        mobileTestApp(
          home: CatalogueManagementScreen(
            viewModel: vm,
            authViewModel: authVm,
          ),
        ),
      );
      await tester.pumpAndSettle();

      final createBtn = find.byKey(const Key('btn-create-destination-dialog'));
      expect(createBtn, findsOneWidget);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.text('Add Coastal Destination'), findsOneWidget);
      expect(find.text('Name *'), findsOneWidget);
      expect(find.text('Save'), findsOneWidget);

      // Cancel dialog
      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();
      expect(find.text('Add Coastal Destination'), findsNothing);
    });
  });
}


