import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/models/auth_models.dart';
import 'package:mobile/data/repositories/marine_repository.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/marine_api_client.dart';
import 'package:mobile/data/services/marine_service.dart';
import 'package:mobile/ui/marine/history_screen.dart';
import 'package:mobile/ui/marine/marine_auth_navigation.dart';
import 'package:mobile/ui/marine/marine_route_table.dart';
import 'package:mobile/ui/marine/safety_profiles_screen.dart';
import 'package:mobile/ui/marine/suitability_screen.dart';
import 'package:mobile/main.dart';

import 'support/mobile_test_support.dart';

class TwoGateways extends ApiGatewayConfig {
  @override
  List<Uri> get baseUris => [
    Uri.parse('http://first'),
    Uri.parse('http://second'),
  ];
}

MarineService service(http.Client client) => MarineService(
  repository: MarineRepository(
    client: MarineApiClient(client: client, gateway: TwoGateways()),
  ),
);

Finder coordinate(String label) => find.byWidgetPredicate(
  (widget) => widget is TextField && widget.decoration?.labelText == label,
);

Future<void> enterLocation(
  WidgetTester tester,
  String latitude,
  String longitude,
) async {
  await tester.enterText(coordinate('Latitude (−90 to 90)'), latitude);
  await tester.enterText(coordinate('Longitude (−180 to 180)'), longitude);
}

void main() {
  testWidgets(
    'MOB-MARINE-REPAIR-009 the real application shell opens safety profiles for a reader',
    (tester) async {
      final repository = FakeAuthRepository(
        user: mobileTestUser(permissions: ['marine.profile.read']),
      );
      addTearDown(repository.close);
      final calls = <String>[];
      await tester.pumpWidget(
        MyApp(
          authRepository: repository,
          marineService: service(
            MockClient((request) async {
              calls.add('${request.method} ${request.url.path}');
              return http.Response('[]', 200);
            }),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Marine & safety'));
      await tester.tap(find.text('Marine & safety'));
      await tester.pumpAndSettle();
      expect(find.byType(AppBar), findsOneWidget);
      await tester.tap(find.text('Activity safety limits'));
      await tester.pumpAndSettle();
      expect(find.byType(SafetyProfilesScreen), findsOneWidget);
      expect(calls, contains('GET /api/marine/safety-profiles'));
      expect(
        find.textContaining('This coastal service needs permission'),
        findsNothing,
      );
      expect(find.text('Create draft'), findsNothing);
      expect(
        tester
            .widget<SafetyProfilesScreen>(find.byType(SafetyProfilesScreen))
            .canManage,
        isFalse,
      );
    },
  );
  testWidgets(
    'MOB-MARINE-REPAIR-001 signed decimal suitability coordinates reach the API unchanged',
    (tester) async {
      late Uri received;
      final api = service(
        MockClient((request) async {
          if (request.url.path.endsWith('/activities')) {
            return http.Response('[]', 200);
          }
          received = request.url;
          return http.Response(
            jsonEncode({
              'id': 'snapshot',
              'latitude': -6.025,
              'longitude': -80.216,
              'forecastTime': '2026-10-08T08:00:00Z',
              'retrievedAt': '2026-10-08T08:01:00Z',
              'windSpeed': 15,
              'waveHeight': 0.8,
              'swellHeight': 0.7,
              'source': 'Open-Meteo',
              'freshnessStatus': 'FRESH',
              'missingFields': [],
            }),
            200,
          );
        }),
      );
      await tester.pumpWidget(
        MaterialApp(home: SuitabilityScreen(service: api)),
      );
      await tester.pumpAndSettle();
      await enterLocation(tester, '-6.025', '-80.216');
      expect(
        tester
            .widget<TextField>(coordinate('Latitude (−90 to 90)'))
            .controller!
            .text,
        '-6.025',
      );
      await tester.ensureVisible(find.text('View conditions only'));
      await tester.tap(find.text('View conditions only'));
      await tester.pumpAndSettle();
      expect(received.path, '/api/marine/current');
      expect(received.queryParameters['latitude'], '-6.02500');
      expect(received.queryParameters['longitude'], '-80.21600');
      expect(find.text('Open-Meteo'), findsOneWidget);
    },
  );

  testWidgets(
    'MOB-MARINE-REPAIR-002 history preserves coordinates and rejects nonblank invalid filters',
    (tester) async {
      final queries = <Uri>[];
      final api = service(
        MockClient((request) async {
          if (request.url.path.endsWith('/history')) queries.add(request.url);
          return http.Response('[]', 200);
        }),
      );
      await tester.pumpWidget(
        MaterialApp(home: MarineHistoryScreen(service: api)),
      );
      await tester.pumpAndSettle();
      await enterLocation(tester, '-6.025', '-80.216');
      await tester.ensureVisible(find.text('Apply filters'));
      await tester.tap(find.text('Apply filters'));
      await tester.pumpAndSettle();
      expect(queries.last.queryParameters['latitude'], '-6.02500');
      expect(queries.last.queryParameters['longitude'], '-80.21600');
      final count = queries.length;
      for (final invalid in ['NaN', 'Infinity', 'abc', '-90.01', '90.01']) {
        await tester.enterText(coordinate('Latitude (−90 to 90)'), invalid);
        await tester.ensureVisible(find.text('Apply filters'));
        await tester.tap(find.text('Apply filters'));
        await tester.pumpAndSettle();
        expect(
          queries.length,
          count,
          reason:
              'Invalid filter must not silently become an unfiltered query: $invalid',
        );
        expect(find.textContaining('Latitude must'), findsOneWidget);
      }
      await enterLocation(tester, '-90', '180');
      await tester.ensureVisible(find.text('Apply filters'));
      await tester.tap(find.text('Apply filters'));
      await tester.pumpAndSettle();
      expect(queries.last.queryParameters['latitude'], '-90.00000');
      expect(queries.last.queryParameters['longitude'], '180.00000');
    },
  );

  test('MOB-MARINE-REPAIR-003 profile navigation requires read permission', () {
    AuthUser user(List<String> permissions) => AuthUser(
      id: 'reader',
      email: 'reader@example.test',
      fullName: 'Reader',
      isActive: true,
      createdAt: DateTime.utc(2026),
      roles: const [],
      permissions: permissions,
    );
    expect(marineRouteHrefsFor(user(['marine.profile.read'])), [
      MarineRoutes.conditions,
      MarineRoutes.history,
      MarineRoutes.safetyProfiles,
    ]);
    expect(marineRouteHrefsFor(user(['marine.profile.manage'])), isEmpty);
    expect(marineRouteHrefsFor(user([])), isEmpty);
    expect(marineRouteHrefsFor(null), isEmpty);
  });

  testWidgets(
    'MOB-MARINE-REPAIR-004 readers can load profiles without mutation controls',
    (tester) async {
      final calls = <String>[];
      await tester.pumpWidget(
        MaterialApp(
          home: SafetyProfilesScreen(
            canManage: false,
            service: service(
              MockClient((request) async {
                calls.add('${request.method} ${request.url.path}');
                return http.Response('[]', 200);
              }),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(calls, contains('GET /api/marine/safety-profiles'));
      expect(calls.every((call) => call.startsWith('GET ')), isTrue);
      expect(find.text('Create draft'), findsNothing);
      expect(find.text('Review and activate'), findsNothing);
      expect(find.text('Deactivate profile'), findsNothing);
      expect(find.text('Create a new version'), findsNothing);
    },
  );

  testWidgets(
    'MOB-MARINE-REPAIR-008 a four-second operation completes within the default budget',
    (tester) async {
      final completion = Completer<http.Response>();
      var sends = 0;
      final api = MarineApiClient(
        client: MockClient((_) {
          sends++;
          return completion.future;
        }),
        gateway: TwoGateways(),
      );
      final pending = api.deactivateSafetyProfile('profile');
      await tester.pump(const Duration(seconds: 4));
      expect(sends, 1);
      completion.complete(http.Response('', 204));
      await tester.pump();
      await pending;
      expect(sends, 1);
    },
  );

  // Completion after the deadline is deliberate: a timeout does not cancel an accepted write.
  for (final operation in [
    'evaluate',
    'create',
    'update',
    'review',
    'delete',
  ]) {
    test(
      'MOB-MARINE-REPAIR-005 $operation timeout makes one attempt and reports unknown outcome',
      () async {
        final completion = Completer<http.Response>();
        final requests = <http.Request>[];
        final api = MarineApiClient(
          client: MockClient((request) {
            requests.add(request);
            return completion.future;
          }),
          gateway: TwoGateways(),
          requestTimeout: const Duration(milliseconds: 15),
        );
        await expectLater(
          mutation(api, operation),
          throwsA(
            isA<MarineApiException>()
                .having((error) => error.statusCode, 'status', 0)
                .having(
                  (error) => error.message,
                  'detail',
                  contains('outcome could not be confirmed'),
                ),
          ),
        );
        expect(requests, hasLength(1));
        expect(requests.single.url.host, 'first');
        expect(
          requests.single.method,
          operation == 'delete'
              ? 'DELETE'
              : operation == 'update'
              ? 'PUT'
              : 'POST',
        );
        completion.complete(http.Response('{}', 200));
        await Future<void>.delayed(Duration.zero);
        expect(requests, hasLength(1));
      },
    );
  }

  for (final failure in [
    const SocketException('connection closed'),
    http.ClientException('connection closed'),
  ]) {
    test('MOB-MARINE-REPAIR-006 $failure does not replay a write', () async {
      var calls = 0;
      final api = MarineApiClient(
        client: MockClient((_) async {
          calls++;
          throw failure;
        }),
        gateway: TwoGateways(),
      );
      await expectLater(
        api.deactivateSafetyProfile('profile'),
        throwsA(
          isA<MarineApiException>().having(
            (error) => error.message,
            'detail',
            contains('outcome could not be confirmed'),
          ),
        ),
      );
      expect(calls, 1);
    });
  }

  test('MOB-MARINE-REPAIR-007 read failures may fall back, HTTP rejections stop, budget covers provider latency', () async {
    final hosts = <String>[];
    final api = MarineApiClient(
      client: MockClient((request) async {
        hosts.add(request.url.host);
        if (hosts.length == 1) throw const SocketException('unreachable');
        return http.Response('[]', 200);
      }),
      gateway: TwoGateways(),
    );
    expect(api.requestTimeout, const Duration(seconds: 45));
    expect(await api.getSafetyProfiles(), isEmpty);
    expect(hosts, ['first', 'second']);
    var rejectedCalls = 0;
    final rejected = MarineApiClient(
      client: MockClient((_) async {
        rejectedCalls++;
        return http.Response('{"detail":"Unavailable"}', 503);
      }),
      gateway: TwoGateways(),
    );
    await expectLater(
      rejected.deactivateSafetyProfile('profile'),
      throwsA(
        isA<MarineApiException>().having((e) => e.statusCode, 'status', 503),
      ),
    );
    expect(rejectedCalls, 1);
    expect(
      () => MarineApiClient(
        client: MockClient((_) async => http.Response('[]', 200)),
        gateway: TwoGateways(),
        requestTimeout: Duration.zero,
      ),
      throwsArgumentError,
    );
  });
}

Future<Object?> mutation(MarineApiClient api, String operation) {
  if (operation == 'evaluate') {
    return api.evaluateSuitability(
      activityId: 'activity',
      latitude: 6.025,
      longitude: 80.216,
    );
  }
  if (operation == 'review') return api.reviewSafetyProfile('profile');
  if (operation == 'delete') return api.deactivateSafetyProfile('profile');
  if (operation == 'update') {
    return api.updateSafetyProfile(
      id: 'profile',
      maxWindSpeed: 20,
      maxWaveHeight: 1,
      maxSwellHeight: 1,
      windCriteriaSource: 'source',
      windCriteriaRationale: 'rationale',
      waveCriteriaSource: 'source',
      waveCriteriaRationale: 'rationale',
      swellCriteriaSource: 'source',
      swellCriteriaRationale: 'rationale',
    );
  }
  return api.createSafetyProfile(
    activityId: 'activity',
    maxWindSpeed: 20,
    maxWaveHeight: 1,
    maxSwellHeight: 1,
    windCriteriaSource: 'source',
    windCriteriaRationale: 'rationale',
    waveCriteriaSource: 'source',
    waveCriteriaRationale: 'rationale',
    swellCriteriaSource: 'source',
    swellCriteriaRationale: 'rationale',
  );
}
