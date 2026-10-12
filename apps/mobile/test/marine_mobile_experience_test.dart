import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/repositories/marine_repository.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/marine_api_client.dart';
import 'package:mobile/data/services/marine_service.dart';
import 'package:mobile/main.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/account_screens.dart';
import 'package:mobile/ui/blueverse_theme.dart';
import 'package:mobile/ui/feedback/loading_screen_controller.dart';
import 'package:mobile/ui/marine/history_screen.dart';
import 'package:mobile/ui/marine/marine_presentation.dart';
import 'package:mobile/ui/marine/marine_workspace_screen.dart';
import 'package:mobile/ui/marine/safety_profiles_screen.dart';
import 'package:mobile/ui/marine/suitability_screen.dart';

import 'support/mobile_test_support.dart';

const activityId = '12345678-1234-1234-1234-123456789abc';
http.Response json(Object? body, [int status = 200]) =>
    http.Response(jsonEncode(body), status);
MarineService service(Future<http.Response> Function(http.Request) respond) =>
    MarineService(
      repository: MarineRepository(
        client: MarineApiClient(
          client: MockClient(respond),
          gateway: const ApiGatewayConfig(),
        ),
      ),
    );
Map<String, Object?> snapshot() => {
  'id': 'reading-1',
  'latitude': 6.025,
  'longitude': 80.216,
  'forecastTime': '2026-10-09T10:00:00Z',
  'retrievedAt': '2026-10-09T09:55:00Z',
  'windSpeed': 15.25,
  'waveHeight': .8,
  'swellHeight': null,
  'rain': 0,
  'source': 'Synthetic coastal provider',
  'freshnessStatus': 'FRESH',
  'missingFields': ['swellHeight'],
};
Map<String, Object?> assessment() => {
  'id': 'assessment-1',
  'activityId': activityId,
  'activityName': 'Coastal kayaking',
  'conditionSnapshotId': 'reading-1',
  'safetyProfileId': 'profile-1',
  'profileVersion': 2,
  'latitude': 6.025,
  'longitude': 80.216,
  'requestedTime': '2026-10-09T10:00:00Z',
  'forecastTime': '2026-10-09T10:00:00Z',
  'evaluatedAt': '2026-10-09T09:55:00Z',
  'result': 'UNKNOWN',
  'conditions': {'windSpeed': 15.25, 'waveHeight': .8, 'swellHeight': null},
  'source': 'Synthetic coastal provider',
  'retrievedAt': '2026-10-09T09:55:00Z',
  'freshnessStatus': 'FRESH',
  'missingFields': ['swellHeight'],
  'violations': [],
  'cautionFactors': [],
  'criteria': [],
  'evidenceCompleteness': 'LEGACY_INCOMPLETE',
  'createdAt': '2026-10-09T09:55:00Z',
};
Finder field(String label) => find.byWidgetPredicate(
  (widget) => widget is TextField && widget.decoration?.labelText == label,
);
Future<void> tapVisible(WidgetTester tester, String text) async {
  await tester.ensureVisible(find.text(text).first);
  await tester.tap(find.text(text).first);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'MOB-MARINE-APP-012 signed-out real-app launch keeps coastal onboarding and never fetches Marine data (ui-integration: foundation-home)',
    (tester) async {
      final repository = FakeAuthRepository();
      addTearDown(repository.close);
      var calls = 0;
      await tester.pumpWidget(
        MyApp(
          authRepository: repository,
          marineService: service((_) async {
            calls++;
            return json([]);
          }),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Closer to the coast.'), findsOneWidget);
      expect(find.text('Skip'), findsOneWidget);
      expect(find.byType(MarineWorkspaceScreen), findsNothing);
      expect(calls, 0);
      expect(tester.takeException(), isNull);
    },
  );

  for (final grants in [
    ['marine.profile.read'],
    ['marine.profile.manage'],
    <String>[],
  ]) {
    testWidgets(
      'MOB-MARINE-APP-010 dashboard and account menu expose Marine only with a read grant: $grants (ui-integration: marine-conditions)',
      (tester) async {
        final repository = FakeAuthRepository(
          user: mobileTestUser(roles: ['Admin'], permissions: grants),
        );
        final model = AuthViewModel(repository: repository)
          ..user = repository.user;
        addTearDown(() {
          model.dispose();
          repository.close();
        });
        var opens = 0;
        await tester.pumpWidget(
          MaterialApp(
            home: AuthDashboardScreen(viewModel: model),
            routes: {
              '/marine/conditions': (_) {
                opens++;
                return const Scaffold(
                  body: Text('Synthetic Marine destination'),
                );
              },
            },
          ),
        );
        await tester.pumpAndSettle();
        final allowed = grants.contains('marine.profile.read');
        expect(
          find.text('Marine & safety'),
          allowed ? findsOneWidget : findsNothing,
        );
        await tester.tap(find.byTooltip('Switch BLUEVERSE account'));
        await tester.pumpAndSettle();
        expect(
          find.text('Marine & safety'),
          allowed ? findsNWidgets(2) : findsNothing,
        );
        if (allowed) {
          await tester.tap(
            find.descendant(
              of: find.byType(PopupMenuItem<String>),
              matching: find.text('Marine & safety'),
            ),
          );
          await tester.pumpAndSettle();
          expect(opens, 1);
          expect(find.text('Synthetic Marine destination'), findsOneWidget);
        } else {
          expect(opens, 0);
        }
      },
    );
  }

  testWidgets(
    'MOB-MARINE-APP-011 failed refresh retains saved readings with a stale-data label; recovery clears that label (ui-integration: marine-condition-history)',
    (tester) async {
      var fail = false;
      final api = service((request) async {
        if (request.url.path.endsWith('/history')) {
          return fail
              ? json({'detail': 'Readings unavailable.'}, 503)
              : json([snapshot()]);
        }
        if (request.url.path.endsWith('/assessments')) {
          return json([assessment()]);
        }
        return json([]);
      });
      await tester.pumpWidget(
        MaterialApp(home: MarineHistoryScreen(service: api)),
      );
      await tester.pumpAndSettle();
      fail = true;
      await tester
          .widget<RefreshIndicator>(find.byType(RefreshIndicator))
          .onRefresh();
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Showing last-loaded data; it may not match the current filters.',
        ),
        findsOneWidget,
      );
      expect(find.text('Wind: 15.25 km/h'), findsOneWidget);
      expect(find.text('SUITABILITY ASSESSMENTS'), findsOneWidget);
      fail = false;
      await tapVisible(tester, 'Retry Saved readings');
      expect(
        find.text(
          'Showing last-loaded data; it may not match the current filters.',
        ),
        findsNothing,
      );
      expect(find.text('Readings unavailable.'), findsNothing);
    },
  );

  test('MOB-MARINE-APP-001 public requests reuse the selected secure account and rotated token; missing credentials fail before transport', () async {
    final store = MemoryAuthCredentialStore();
    store.values.addAll({
      'blueverse.active_account_id': 'one',
      'blueverse.access_token.one': 'synthetic-one',
    });
    final auth = AuthApiService(storage: store);
    final headers = <String?>[];
    final api = MarineApiClient(
      client: MockClient((request) async {
        headers.add(request.headers['authorization']);
        expect(request.url.path, '/api/marine/activities');
        expect(request.headers['accept'], 'application/json');
        return json([]);
      }),
      gateway: const ApiGatewayConfig(),
      authenticatedHeaders: auth.authenticatedHeaders,
    );
    expect(await api.getActivities(), isEmpty);
    store.values['blueverse.access_token.one'] = 'synthetic-rotated';
    expect(await api.getActivities(), isEmpty);
    store.values.addAll({
      'blueverse.active_account_id': 'two',
      'blueverse.access_token.two': 'synthetic-two',
    });
    expect(await api.getActivities(), isEmpty);
    store.values.remove('blueverse.access_token.two');
    await expectLater(
      api.getActivities(),
      throwsA(
        isA<MarineApiException>().having((e) => e.statusCode, 'status', 401),
      ),
    );
    expect(headers, [
      'Bearer synthetic-one',
      'Bearer synthetic-rotated',
      'Bearer synthetic-two',
    ]);
  });

  test('MOB-MARINE-APP-002 writes attach the active credential once and finish loading on denial without replay', () async {
    var starts = 0;
    var finishes = 0;
    var calls = 0;
    final api = MarineApiClient(
      client: MockClient((request) async {
        calls++;
        expect(request.method, 'DELETE');
        expect(request.headers['authorization'], 'Bearer synthetic-current');
        expect(request.url.path, '/api/marine/safety-profiles/profile-1');
        return json({'detail': 'Current manage grant is required.'}, 403);
      }),
      gateway: const ApiGatewayConfig(),
      authenticatedHeaders: () async => {
        'Authorization': 'Bearer synthetic-current',
      },
      beginRequest: (_) {
        starts++;
        return () {
          finishes++;
        };
      },
    );
    await expectLater(
      api.deactivateSafetyProfile('profile-1'),
      throwsA(
        isA<MarineApiException>()
            .having((e) => e.statusCode, 'status', 403)
            .having(
              (e) => e.message,
              'detail',
              'Current manage grant is required.',
            ),
      ),
    );
    expect((calls, starts, finishes), (1, 1, 1));
  });

  test('MOB-MARINE-APP-003 malformed saved rows fail instead of disappearing as an empty success', () async {
    for (final payload in [
      null,
      {},
      [42],
      ['invalid row'],
    ]) {
      final api = MarineApiClient(
        client: MockClient((_) async => json(payload)),
        gateway: const ApiGatewayConfig(),
      );
      for (final read in [
        api.getHistory,
        api.getAssessments,
        api.getSafetyProfiles,
      ]) {
        await expectLater(
          read(),
          throwsA(
            isA<MarineApiException>()
                .having((e) => e.statusCode, 'status', 502)
                .having(
                  (e) => e.message,
                  'safe detail',
                  'Saved marine data could not be read. Please retry.',
                ),
          ),
        );
      }
    }
  });

  test('MOB-MARINE-APP-004 UTC inputs preserve offsets and reject impossible dates, missing date parts and extreme invalid values', () {
    expect(marineUtcTime('  '), isNull);
    expect(marineUtcTime('2026-10-09T10:00'), DateTime.utc(2026, 10, 9, 10));
    expect(
      marineUtcTime('2026-10-09T10:00+05:30'),
      DateTime.utc(2026, 10, 9, 4, 30),
    );
    expect(marineUtcTime('2024-02-29T00:00Z'), DateTime.utc(2024, 2, 29));
    for (final invalid in [
      '2026-02-29T10:00',
      '2026-10-09T24:00',
      '0000-01-01T10:00',
      '2026-13-01T10:00',
      '2026-10-09',
      'tomorrow',
      '2026-10-09T10:00+14:30',
    ]) {
      expect(
        () => marineUtcTime(invalid),
        throwsFormatException,
        reason: invalid,
      );
    }
  });

  testWidgets(
    'MOB-MARINE-APP-005 narrow native workspace has one app bar, pinned shared labels, heroes and reduced-motion tab changes (ui-integration: marine-conditions)',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(320, 812));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      var hold = false;
      final pending = <Completer<http.Response>>[];
      final api = service((_) {
        if (!hold) return Future.value(json([]));
        final read = Completer<http.Response>();
        pending.add(read);
        return read.future;
      });
      await tester.pumpWidget(
        MaterialApp(
          theme: BlueverseTheme.light,
          home: MediaQuery(
            data: const MediaQueryData(
              size: Size(320, 812),
              textScaler: TextScaler.linear(1.6),
              disableAnimations: true,
            ),
            child: MarineWorkspaceScreen(
              service: api,
              initialIndex: 0,
              canManage: false,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byType(AppBar), findsOneWidget);
      expect(find.text('Read the coast before you go.'), findsOneWidget);
      expect(
        tester.widget<AnimatedSwitcher>(find.byType(AnimatedSwitcher)).duration,
        Duration.zero,
      );
      final before = tester.getTopLeft(find.text('Coastal conditions'));
      await tester.drag(
        find.byType(SingleChildScrollView).last,
        const Offset(0, -450),
      );
      await tester.pumpAndSettle();
      expect(tester.getTopLeft(find.text('Coastal conditions')), before);
      hold = true;
      await tester.ensureVisible(find.text('Coastal evidence'));
      await tester.tap(find.text('Coastal evidence'));
      await tester.pump();
      expect(find.byType(MarineHistoryScreen), findsOneWidget);
      expect(find.text('Loading saved coastal evidence…'), findsOneWidget);
      expect(blueverseLoadingScreenController.isVisible, isFalse);
      expect(find.byType(MarineHero), findsOneWidget);
      for (final read in pending) {
        read.complete(json([]));
      }
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'MOB-MARINE-APP-006 the real shell responds to removed read grants without mounting or fetching protected data (ui-integration: marine-conditions)',
    (tester) async {
      final repository = FakeAuthRepository(
        user: mobileTestUser(permissions: ['marine.profile.read']),
      );
      addTearDown(repository.close);
      var calls = 0;
      await tester.pumpWidget(
        MyApp(
          authRepository: repository,
          marineService: service((_) async {
            calls++;
            return json([]);
          }),
        ),
      );
      await tester.pumpAndSettle();
      await tapVisible(tester, 'Marine & safety');
      expect(find.byType(MarineWorkspaceScreen), findsOneWidget);
      final model =
          tester
                  .widget<ListenableBuilder>(
                    find
                        .byWidgetPredicate(
                          (w) =>
                              w is ListenableBuilder &&
                              w.listenable is AuthViewModel,
                        )
                        .first,
                  )
                  .listenable
              as AuthViewModel;
      final previous = calls;
      model.user = mobileTestUser(permissions: ['marine.profile.manage']);
      model.notifyListeners();
      await tester.pumpAndSettle();
      expect(find.byType(MarineWorkspaceScreen), findsNothing);
      expect(
        find.textContaining('This coastal service needs permission'),
        findsOneWidget,
      );
      expect(calls, previous);
    },
  );

  for (final failed in ['history', 'assessments']) {
    testWidgets(
      'MOB-MARINE-APP-007 $failed failure preserves the other saved dataset and retry recovers (ui-integration: marine-condition-history)',
      (tester) async {
        var fail = true;
        final calls = <String>[];
        final api = service((request) async {
          calls.add(request.url.path);
          if (fail && request.url.path.endsWith('/$failed')) {
            return json({'detail': 'Synthetic saved data unavailable.'}, 503);
          }
          if (request.url.path.endsWith('/history')) return json([snapshot()]);
          if (request.url.path.endsWith('/assessments')) {
            return json([assessment()]);
          }
          return json([]);
        });
        await tester.pumpWidget(
          MaterialApp(
            theme: BlueverseTheme.light,
            home: MarineHistoryScreen(service: api),
          ),
        );
        await tester.pumpAndSettle();
        expect(
          calls,
          containsAll(['/api/marine/history', '/api/marine/assessments']),
        );
        expect(find.text('Synthetic saved data unavailable.'), findsOneWidget);
        expect(
          find.text(
            failed == 'history'
                ? 'SUITABILITY ASSESSMENTS'
                : 'STORED SNAPSHOTS',
          ),
          findsOneWidget,
        );
        expect(
          find.text(
            failed == 'history'
                ? 'No condition snapshots match these filters. Run a condition query to store the first one.'
                : 'No suitability assessments match these filters.',
          ),
          findsNothing,
        );
        fail = false;
        await tapVisible(
          tester,
          failed == 'history'
              ? 'Retry Saved readings'
              : 'Retry Saved assessments',
        );
        expect(find.text('STORED SNAPSHOTS'), findsOneWidget);
        expect(find.text('SUITABILITY ASSESSMENTS'), findsOneWidget);
        expect(find.text('Synthetic saved data unavailable.'), findsNothing);
        expect(find.text('Wind: 15.25 km/h'), findsOneWidget);
        expect(find.text('Swell: Unavailable'), findsOneWidget);
      },
    );
  }

  testWidgets(
    'MOB-MARINE-APP-008 UNKNOWN survives absent condition evidence; pulling refresh performs a read without another assessment write (ui-integration: marine-conditions)',
    (tester) async {
      var posts = 0;
      var gets = 0;
      final api = service((request) async {
        if (request.url.path.endsWith('/activities')) {
          return json([
            {
              'id': activityId,
              'name': 'Coastal kayaking',
              'activityType': 'kayaking',
              'isActive': true,
            },
          ]);
        }
        if (request.method == 'POST') {
          posts++;
          return json({
            'status': 'UNKNOWN',
            'activityId': activityId,
            'activityName': 'Coastal kayaking',
            'location': {'latitude': 6.025, 'longitude': 80.216},
            'requestedTime': '2026-10-09T10:00:00Z',
            'forecastTime': '2026-10-09T10:00:00Z',
            'evaluatedAt': '2026-10-09T09:55:00Z',
            'conditions': null,
            'source': null,
            'retrievedAt': null,
            'freshness': null,
            'missingFields': ['conditions'],
            'violations': [],
            'cautionFactors': [],
            'assessmentId': 'assessment-1',
            'snapshotId': 'reading-1',
          });
        }
        gets++;
        return json(snapshot());
      });
      await tester.pumpWidget(
        MaterialApp(home: SuitabilityScreen(service: api)),
      );
      await tester.pumpAndSettle();
      await tapVisible(tester, 'Check conditions & suitability');
      expect(find.text('UNKNOWN'), findsOneWidget);
      expect(find.text('More evidence is needed'), findsOneWidget);
      expect(posts, 1);
      await tester
          .widget<RefreshIndicator>(find.byType(RefreshIndicator))
          .onRefresh();
      await tester.pumpAndSettle();
      expect((posts, gets), (1, 1));
      expect(find.text('Wind: 15.25 km/h'), findsOneWidget);
    },
  );

  testWidgets(
    'MOB-MARINE-APP-009 failed profile read is recoverable and never labelled an empty catalogue (ui-integration: marine-safety-profile-management)',
    (tester) async {
      final api = service(
        (request) async => request.url.path.endsWith('/safety-profiles')
            ? json({'detail': 'Safety limits unavailable.'}, 503)
            : json([]),
      );
      await tester.pumpWidget(
        MaterialApp(home: SafetyProfilesScreen(service: api, canManage: false)),
      );
      await tester.pumpAndSettle();
      expect(find.text('Safety limits unavailable.'), findsOneWidget);
      expect(
        find.textContaining('No safety profiles are configured.'),
        findsNothing,
      );
      expect(find.text('Retry profiles'), findsOneWidget);
      expect(find.text('Create draft'), findsNothing);
    },
  );
}
