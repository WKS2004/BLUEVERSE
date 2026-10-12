import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/coastal_operations_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/blueverse_theme.dart';
import 'package:mobile/ui/coastal_operations_logs_screen.dart';
import 'package:mobile/ui/coastal_operations_presentation.dart';
import 'package:mobile/ui/coastal_operations_screen.dart';

import 'support/mobile_test_support.dart';

const _id = '11111111-2222-4333-8444-555555555555';
const _assessment = {
  'assessmentId': _id,
  'workflowId': _id,
  'title': 'Review beach access',
  'objective':
      'Check the marked path and dune protection before the coastal walk.',
  'workflowStatus': 'SUBMITTED',
  'targetType': 'ACTIVITY',
  'targetId': _id,
  'version': 2,
  'periodStartsAt': '2026-10-01T08:00Z',
  'periodEndsAt': '2026-10-01T10:00Z',
  'createdAt': '2026-10-01T08:00Z',
  'updatedAt': '2026-10-01T09:00Z',
  'aiDependencyStatus': 'NOT_CONNECTED',
  'aiDispatchOutcome': 'NOT_REQUESTED',
  'componentDependencies': [
    {
      'service': 'marine-safety',
      'status': 'NOT_CONNECTED',
      'retryable': false,
      'checkedAt': '2026-10-01T09:00Z',
    },
  ],
};
const _alert = {
  'alertId': _id,
  'title': 'Use the marked shoreline path',
  'description':
      'Give the dune habitat space and follow the marked access path.',
  'lifecycle': 'PROPOSED',
  'targetType': 'ACTIVITY',
  'targetId': _id,
  'version': 2,
  'severity': 'MODERATE',
  'visibility': 'PUBLIC',
  'validFrom': '2026-10-01T08:00Z',
  'validUntil': '2026-10-01T10:00Z',
  'createdAt': '2026-10-01T08:00Z',
  'updatedAt': '2026-10-01T09:00Z',
};
const _options = {
  'timeZones': [
    {
      'id': 'Etc/UTC',
      'country': 'Worldwide',
      'location': 'UTC',
      'rulesAvailable': true,
    },
  ],
  'targets': {'status': 'NOT_CONNECTED', 'items': []},
  'plans': {'status': 'NOT_CONNECTED', 'items': []},
  'assessments': [],
};
http.Response _json(Object body, [int status = 200]) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

Future<List<http.Request>> _open(
  WidgetTester tester, {
  bool alerts = false,
  bool logs = false,
  String? view,
  Size size = const Size(390, 844),
  double scale = 1,
  bool reduced = false,
  List<String>? permissions,
  Future<http.Response> Function(http.Request)? respond,
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  final fontDirectory = Platform.environment['COASTAL_PREVIEW_FONT_DIR'];
  if (fontDirectory != null) {
    await tester.runAsync(() async {
      final font = ByteData.sublistView(
        await File('$fontDirectory/roboto-regular.ttf').readAsBytes(),
      );
      for (final family in ['Manrope', 'Sora', 'Roboto']) {
        await (FontLoader(family)..addFont(Future.value(font))).load();
      }
      final icons = ByteData.sublistView(
        await File('$fontDirectory/materialicons-regular.otf').readAsBytes(),
      );
      await (FontLoader('MaterialIcons')..addFont(Future.value(icons))).load();
    });
  }
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final calls = <http.Request>[];
  final client = MockClient((request) async {
    calls.add(request);
    if (respond != null) return respond(request);
    if (request.url.path.endsWith('/form-options')) return _json(_options);
    if (request.url.path.endsWith('/$_id')) {
      return _json({
        'assessment': _assessment,
        'decisions': [],
        'evidence': [],
      });
    }
    return _json({
      'items': request.url.path.endsWith('/alerts') ? [_alert] : [_assessment],
      'nextCursor': null,
    });
  });
  final authClient = MockClient((_) async => _json({}, 401));
  final store = MemoryAuthCredentialStore()
    ..values.addAll({
      'blueverse.active_account_id': 'test-operator',
      'blueverse.access_token.test-operator': 'synthetic-access',
    });
  final api = CoastalOperationsApiService(
    authApiService: AuthApiService(client: authClient, storage: store),
    client: client,
  );
  final user = mobileTestUser(
    permissions:
        permissions ??
        [
          'operations.assessment.read',
          'operations.assessment.create',
          'operations.alert.read',
          'operations.alert.create',
          'operations.audit.read',
        ],
  );
  final repo = FakeAuthRepository(user: user);
  final model = AuthViewModel(repository: repo)..user = user;
  addTearDown(model.dispose);
  addTearDown(repo.close);
  addTearDown(client.close);
  addTearDown(authClient.close);
  Widget page(bool alerts, bool logs, {String? view}) => logs
      ? CoastalOperationsLogsScreen(
          viewModel: model,
          apiService: api,
          initialAlerts: alerts,
        )
      : CoastalOperationsScreen(
          viewModel: model,
          apiService: api,
          section: alerts
              ? CoastalOperationsSection.alerts
              : CoastalOperationsSection.assessments,
          initialView: view,
          initialRecordId: view == 'detail' ? _id : null,
        );
  await tester.pumpWidget(
    MaterialApp(
      theme: BlueverseTheme.light,
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context).copyWith(
          textScaler: TextScaler.linear(scale),
          disableAnimations: reduced,
        ),
        child: RepaintBoundary(key: const ValueKey('preview'), child: child!),
      ),
      home: page(alerts, logs, view: view),
      onGenerateRoute: (settings) {
        final uri = Uri.parse(settings.name!);
        return PageRouteBuilder<void>(
          settings: settings,
          transitionDuration: Duration.zero,
          pageBuilder: (_, _, _) => page(
            uri.path.endsWith('/alerts') ||
                uri.queryParameters['kind'] == 'alerts',
            uri.path == '/operations/logs',
            view: uri.queryParameters['view'],
          ),
        );
      },
    ),
  );
  await tester.pumpAndSettle();
  return calls;
}

Future<void> _preview(WidgetTester tester, String name) async {
  final directory = Platform.environment['COASTAL_PREVIEW_DIR'];
  if (directory == null) return;
  final hero = tester.widget<CoastalOperationsHero>(
    find.byType(CoastalOperationsHero),
  );
  await tester.runAsync(
    () => precacheImage(
      AssetImage(
        hero.assessments
            ? 'assets/coastal/onboarding/assessment-hero.png'
            : 'assets/coastal/onboarding/alerts-hero.png',
      ),
      tester.element(find.byType(CoastalOperationsHero)),
    ),
  );
  await tester.pumpAndSettle();
  final boundary = tester.renderObject<RenderRepaintBoundary>(
    find.byKey(const ValueKey('preview')),
  );
  await tester.runAsync(() async {
    final image = await boundary.toImage(pixelRatio: 1);
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    await File('$directory/$name.png')
        .writeAsBytes(bytes!.buffer.asUint8List());
    image.dispose();
  });
}

void main() {
  testWidgets(
    'MOB-OPS-REBUILD-007 unexpected save failure retains entries and supports retry (ui-integration: coastal-operations-assessment)',
    (tester) async {
      final posts = <http.Request>[];
      final saved = {
        ..._assessment,
        'workflowStatus': 'DRAFT',
        'title': 'Review dune access',
      };
      await _open(
        tester,
        view: 'create',
        respond: (request) async {
          if (request.method == 'POST') {
            posts.add(request);
            if (posts.length == 1) {
              throw StateError('Synthetic transport failure');
            }
            return _json(saved);
          }
          if (request.url.path.endsWith('/form-options')) {
            return _json(_options);
          }
          if (request.url.path.endsWith('/$_id')) {
            return _json({
              'assessment': saved,
              'decisions': [],
              'evidence': [],
            });
          }
          return _json({'items': [], 'nextCursor': null});
        },
      );
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.first, 'Review dune access');
      tester.widget<TextFormField>(fields.at(1)).controller!.text =
          '2026-10-01T08:00';
      tester.widget<TextFormField>(fields.at(2)).controller!.text =
          '2026-10-01T10:00';
      await tester.enterText(
        fields.last,
        'Check marked paths before the coastal walk.',
      );
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Save draft'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Save draft'));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'The draft could not be saved. Your entries are still here; please retry.',
        ),
        findsOneWidget,
      );
      expect(
        tester.widget<TextFormField>(fields.first).controller!.text,
        'Review dune access',
      );
      expect(
        tester.widget<TextFormField>(fields.last).controller!.text,
        'Check marked paths before the coastal walk.',
      );
      expect(posts, hasLength(1));
      expect(jsonDecode(posts.single.body), {
        'title': 'Review dune access',
        'objective': 'Check marked paths before the coastal walk.',
        'targetType': 'DESTINATION',
        'sourceWorkflowId': null,
        'timeZoneId': 'Etc/UTC',
        'periodStartsAt': '2026-10-01T08:00',
        'periodEndsAt': '2026-10-01T10:00',
      });
      await tester.ensureVisible(find.text('Save draft'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Save draft'));
      await tester.pumpAndSettle();
      expect(posts, hasLength(2));
      expect(posts.last.body, posts.first.body);
      expect(
        find.text(
          'Assessment draft saved. Submit it when you are ready to check coastal context.',
        ),
        findsOneWidget,
      );
      expect(find.text('Save draft'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
  for (final alerts in [false, true]) {
    testWidgets(
      'MOB-OPS-REBUILD-006 $alerts narrow detail keeps context readable and authored fields read-only (ui-integration: coastal-operations-${alerts ? 'alerts' : 'assessment'})',
      (tester) async {
        final calls = await _open(
          tester,
          alerts: alerts,
          view: 'detail',
          size: const Size(320, 740),
          scale: 2,
        );
        expect(
          find.text('Back to ${alerts ? 'Alerts' : 'Assessments'}'),
          findsOneWidget,
        );
        expect(find.byType(TextFormField), findsNothing);
        expect(
          find.text(
            alerts ? 'Use the marked shoreline path' : 'Review beach access',
          ),
          findsWidgets,
        );
        for (var i = 0; i < 14; i++) {
          await tester.drag(find.byType(ListView).last, const Offset(0, -250));
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
        }
        expect(find.text('View activity'), findsOneWidget);
        if (!alerts) {
          expect(find.text('Marine safety'), findsOneWidget);
          expect(find.textContaining('Checked '), findsOneWidget);
          expect(
            find.textContaining('automated proposals are not available yet'),
            findsOneWidget,
          );
          expect(find.text('Edit draft'), findsNothing);
        }
        expect(calls.every((request) => request.method == 'GET'), isTrue);
      },
    );
  }
  for (final logs in [false, true]) {
    for (final alerts in [false, true]) {
      for (final size in [
        const Size(390, 844),
        const Size(320, 740),
        const Size(768, 1024),
      ]) {
        testWidgets(
          'MOB-OPS-REBUILD-001 $logs/$alerts ${size.width} readable responsive workspace (ui-integration: coastal-operations-${logs
              ? 'logs'
              : alerts
              ? 'alerts'
              : 'assessment'})',
          (tester) async {
            final calls = await _open(
              tester,
              logs: logs,
              alerts: alerts,
              size: size,
              scale: size.width == 320 ? 2 : 1,
            );
            expect(find.byType(CoastalOperationsHero), findsOneWidget);
            expect(
              calls.single.url.path,
              logs
                  ? '/api/operations/logs/${alerts ? 'alerts' : 'assessments'}'
                  : '/api/operations/${alerts ? 'alerts' : 'assessments'}',
            );
            final nav = find.byType(CoastalOperationsNavigation);
            final pinned = tester.getTopLeft(nav);
            if (size.width == 390) {
              await _preview(
                tester,
                'mobile-${logs ? 'logs-' : ''}${alerts ? 'alerts' : 'assessments'}',
              );
            }
            for (var i = 0; i < 12; i++) {
              await tester.drag(
                find.byType(ListView).first,
                const Offset(0, -250),
              );
              await tester.pumpAndSettle();
              expect(tester.takeException(), isNull);
              expect(tester.getTopLeft(nav), pinned);
            }
            expect(find.text('Next'), findsOneWidget);
            expect(
              find.textContaining(
                alerts
                    ? 'Use the marked shoreline path'
                    : 'Review beach access',
              ),
              findsWidgets,
            );
            expect(calls.every((request) => request.method == 'GET'), isTrue);
          },
        );
      }
    }
  }
  for (final alerts in [false, true]) {
    testWidgets(
      'MOB-OPS-REBUILD-002 $alerts accessible full-page form validates without mutation (ui-integration: coastal-operations-${alerts ? 'alerts' : 'assessment'})',
      (tester) async {
        final calls = await _open(
          tester,
          alerts: alerts,
          view: 'create',
          size: const Size(320, 740),
          scale: 2,
        );
        expect(find.text('Draft overview'), findsOneWidget);
        expect(find.text('Coastal links'), findsOneWidget);
        expect(find.text('Dates & time zone'), findsOneWidget);
        expect(find.byTooltip('Search'), findsNothing);
        await tester.ensureVisible(find.text('Save draft'));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        await tester.tap(find.text('Save draft'));
        await tester.pumpAndSettle();
        expect(find.text('Add a clear title.'), findsOneWidget);
        expect(find.text('Choose a date and time.'), findsNWidgets(2));
        expect(calls.every((request) => request.method == 'GET'), isTrue);
        expect(tester.takeException(), isNull);
      },
    );
  }
  for (final logs in [false, true]) {
    testWidgets(
      'MOB-OPS-REBUILD-003 $logs both directions animate content below fixed tabs (ui-integration: coastal-operations-logs)',
      (tester) async {
        final calls = await _open(tester, logs: logs);
        final pinned = tester.getTopLeft(
          find.byType(CoastalOperationsNavigation),
        );
        for (final forward in [true, false]) {
          await tester.tap(
            find.text(
              logs
                  ? (forward ? 'Alert logs' : 'Assessment logs')
                  : (forward ? 'Alerts' : 'Assessments'),
            ),
          );
          await tester.pump();
          await tester.pump(const Duration(milliseconds: 80));
          final slide = tester.widget<SlideTransition>(
            find
                .descendant(
                  of: find.byType(CoastalOperationsEntrance),
                  matching: find.byType(SlideTransition),
                )
                .first,
          );
          final fade = tester.widget<FadeTransition>(
            find
                .descendant(
                  of: find.byType(CoastalOperationsEntrance),
                  matching: find.byType(FadeTransition),
                )
                .first,
          );
          expect(
            slide.position.value.dx,
            forward ? greaterThan(0) : lessThan(0),
          );
          expect(fade.opacity.value, inExclusiveRange(0, 1));
          expect(
            tester.getTopLeft(find.byType(CoastalOperationsNavigation)),
            pinned,
          );
          await tester.pumpAndSettle();
          expect(slide.position.value, Offset.zero);
          expect(
            calls.last.url.path.endsWith(forward ? '/alerts' : '/assessments'),
            isTrue,
          );
          final route = ModalRoute.of(
            tester.element(find.byType(CoastalOperationsEntrance)),
          )!;
          expect(
            route.settings.name,
            logs
                ? '/operations/logs?kind=${forward ? 'alerts' : 'assessments'}'
                : '/operations/${forward ? 'alerts' : 'assessments'}',
          );
        }
        expect(tester.takeException(), isNull);
      },
    );
  }
  testWidgets(
    'MOB-OPS-REBUILD-004 reduced motion disables entry animation and refresh does not replay (ui-integration: coastal-operations-assessment)',
    (tester) async {
      await _open(tester, reduced: true);
      final fade = tester.widget<FadeTransition>(
        find
            .descendant(
              of: find.byType(CoastalOperationsEntrance),
              matching: find.byType(FadeTransition),
            )
            .first,
      );
      expect(fade.opacity.value, 1);
      await tester.ensureVisible(find.byTooltip('Refresh coastal operations'));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('Refresh coastal operations'));
      await tester.pump();
      expect(fade.opacity.value, 1);
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'MOB-OPS-REBUILD-005 Logs denied access makes no API requests (ui-integration: coastal-operations-logs)',
    (tester) async {
      final calls = await _open(
        tester,
        logs: true,
        permissions: ['operations.alert.read'],
      );
      expect(calls, isEmpty);
      expect(
        find.text(
          'Your permissions do not allow access to Coastal Operations logs.',
        ),
        findsOneWidget,
      );
      expect(find.byType(CoastalOperationsHero), findsNothing);
    },
  );
}
