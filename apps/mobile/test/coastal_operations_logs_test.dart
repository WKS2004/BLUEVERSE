import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/coastal_operations_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/coastal_operations_logs_screen.dart';
import 'package:mobile/ui/coastal_operations_screen.dart';
import 'package:mobile/ui/coastal_operations_search.dart';
import 'package:mobile/ui/coastal_operations_activity.dart';

import 'support/mobile_test_support.dart';

const id = '11111111-2222-4333-8444-555555555555';
const imageId = '11111111-2222-4333-8444-666666666666';
const review = {
  'assessmentId': id,
  'workflowId': id,
  'title': 'Coastal review',
  'objective': 'Inspect beach access',
  'workflowStatus': 'DRAFT',
  'targetType': 'ACTIVITY',
  'targetId': id,
  'version': 2,
  'periodStartsAt': '2026-10-01T08:00Z',
  'periodEndsAt': '2026-10-01T10:00Z',
  'createdAt': '2026-10-01T08:00Z',
  'updatedAt': '2026-10-01T09:00Z',
  'aiDependencyStatus': 'NOT_CONNECTED',
  'aiDispatchOutcome': 'NOT_REQUESTED',
  'componentDependencies': [],
};
const image = {
  'evidenceId': imageId,
  'mediaType': 'image/png',
  'byteLength': 100,
  'contentSha256': 'test-digest',
  'inspectionStatus': 'AVAILABLE',
  'assessmentVersion': 2,
  'uploadedAt': '2026-10-01T08:00Z',
  'expiresAt': '2027-10-01T08:00Z',
};
http.Response json(Object body, [int status = 200]) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

void main() {
  Future<void> open(
    WidgetTester tester,
    MockClient client,
    List<String> grants, {
    bool logs = true,
  }) async {
    final authClient = MockClient((_) async => json({}, 401));
    final storage = MemoryAuthCredentialStore()
      ..values.addAll({
        'blueverse.active_account_id': 'test-operator',
        'blueverse.access_token.test-operator': 'synthetic-access',
      });
    final api = CoastalOperationsApiService(
      authApiService: AuthApiService(client: authClient, storage: storage),
      client: client,
    );
    final user = mobileTestUser(permissions: grants);
    final repository = FakeAuthRepository(user: user);
    final model = AuthViewModel(repository: repository)..user = user;
    addTearDown(model.dispose);
    addTearDown(repository.close);
    addTearDown(client.close);
    addTearDown(authClient.close);
    await tester.pumpWidget(
      mobileTestApp(
        home: logs
            ? CoastalOperationsLogsScreen(viewModel: model, apiService: api)
            : CoastalOperationsScreen(
                viewModel: model,
                apiService: api,
                section: CoastalOperationsSection.assessments,
              ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> tap(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await tester.pumpAndSettle();
    await tester.tap(finder);
    await tester.pumpAndSettle();
  }

  testWidgets(
    'MOB-OPS-LOGS-001 logs expose retained records, cursor paging, page sizes and scoped activity (ui-integration: coastal-operations-logs)',
    (tester) async {
      final calls = <http.Request>[];
      final client = MockClient((request) async {
        calls.add(request);
        if (request.url.path.endsWith('/audit')) {
          return json({
            'items': [
              {
                'auditId': id,
                'action': 'CANCELLED',
                'actorId': id,
                'correlationId': 'test-cancel',
                'createdAt': '2026-10-01T09:00Z',
              },
            ],
            'nextCursor': null,
          });
        }
        if (request.url.path.endsWith('/alerts')) {
          return json({
            'items': [
              {
                'alertId': id,
                'title': 'Withdrawn notice',
                'description': 'Use marked path',
                'targetType': 'ACTIVITY',
                'targetId': id,
                'severity': 'LOW',
                'visibility': 'OPERATIONS',
                'lifecycle': 'WITHDRAWN',
                'validFrom': '2026-10-01T08:00Z',
                'validUntil': '2026-10-01T10:00Z',
                'createdAt': '2026-10-01T08:00Z',
                'updatedAt': '2026-10-01T09:00Z',
                'version': 2,
              },
            ],
            'nextCursor': null,
          });
        }
        return json({
          'items': [
            {
              ...review,
              'title': request.url.queryParameters.containsKey('cursor')
                  ? 'Older review'
                  : 'Cancelled review',
              'workflowStatus': 'CANCELLED',
            },
          ],
          'nextCursor': request.url.queryParameters.containsKey('cursor')
              ? null
              : 'older-page',
        });
      });
      await open(tester, client, [
        'operations.audit.read',
        'operations.assessment.read',
        'operations.alert.manage',
      ]);
      expect(find.text('Cancelled review'), findsOneWidget);
      expect(calls.single.url.path, '/api/operations/logs/assessments');
      expect(calls.single.url.queryParameters['pageSize'], '25');
      await tap(tester, find.text('View activity'));
      expect(
        find.descendant(
          of: find.byType(CoastalOperationsActivity),
          matching: find.text('Cancelled the draft'),
        ),
        findsOneWidget,
      );
      expect(calls.last.url.path, '/api/operations/assessments/$id/audit');
      await tap(tester, find.byTooltip('Close activity'));
      await tap(tester, find.text('Next'));
      expect(find.text('Older review'), findsOneWidget);
      expect(calls.last.url.queryParameters['cursor'], 'older-page');
      await tap(tester, find.text('Previous'));
      expect(find.text('Cancelled review'), findsOneWidget);
      expect(calls.last.url.queryParameters.containsKey('cursor'), isFalse);
      final select = find.byType(DropdownButton<int>);
      expect(
        tester
            .widget<DropdownButton<int>>(select)
            .items!
            .map((item) => item.value),
        [5, 10, 25, 50, 100],
      );
      for (final size in [5, 10, 25, 50, 100]) {
        await tap(tester, select);
        await tap(tester, find.text('$size').last);
        expect(calls.last.url.queryParameters['pageSize'], '$size');
        expect(calls.last.url.queryParameters.containsKey('cursor'), isFalse);
      }
      await tester.scrollUntilVisible(
        find.text('Alert logs'),
        -300,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tap(tester, find.text('Alert logs'));
      expect(find.text('Withdrawn notice'), findsOneWidget);
      expect(calls.last.url.path, '/api/operations/logs/alerts');
      expect(find.text('Search the Alerts'), findsOneWidget);
      expect(calls.last.headers['authorization'], 'Bearer synthetic-access');
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'MOB-OPS-LOGS-002 audit alone cannot fetch retained records (ui-integration: coastal-operations-logs)',
    (tester) async {
      var requests = 0;
      await open(
        tester,
        MockClient((_) async {
          requests++;
          return json({'items': [], 'nextCursor': null});
        }),
        ['operations.audit.read'],
      );
      expect(requests, 0);
      expect(
        find.text(
          'Your permissions do not allow access to Coastal Operations logs.',
        ),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'MOB-OPS-SEARCH-001 500ms debounce restarts, sends once, reset cancels and loading stays in bar (ui-integration: coastal-operations-assessment)',
    (tester) async {
      final calls = <Map<String, String>>[];
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: CoastalOperationsSearch(
              assessments: true,
              canManage: true,
              loading: true,
              onApply: calls.add,
            ),
          ),
        ),
      );
      await tester.enterText(find.byType(TextFormField).first, 'coast');
      await tester.pump(const Duration(milliseconds: 400));
      expect(calls, isEmpty);
      await tester.enterText(find.byType(TextFormField).first, 'coastal');
      await tester.pump(const Duration(milliseconds: 499));
      expect(calls, isEmpty);
      await tester.pump(const Duration(milliseconds: 1));
      expect(calls, [
        {'search': 'coastal'},
      ]);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.bySemanticsLabel('Searching records'), findsOneWidget);
      await tester.tap(find.byTooltip('Search'));
      expect(calls, hasLength(1));
      await tester.enterText(find.byType(TextFormField).first, 'pending');
      await tester.tap(find.byTooltip('Reset filters'));
      await tester.pump(const Duration(seconds: 1));
      expect(calls, [
        {'search': 'coastal'},
        {},
      ]);
      await tester.pumpWidget(const SizedBox());
    },
  );

  for (final published in [false, true]) {
    testWidgets(
      'MOB-OPS-EVIDENCE-${published ? '002' : '001'} ${published ? "published evidence is read only" : "draft removal confirms current version and refreshes detail"} (ui-integration: coastal-operations-assessment)',
      (tester) async {
        var attached = true;
        var version = 2;
        http.Request? deletion;
        final record = {
          ...review,
          'workflowStatus': published ? 'SUBMITTED' : 'DRAFT',
        };
        await open(
          tester,
          MockClient((request) async {
            if (request.method == 'DELETE') {
              deletion = request;
              attached = false;
              version++;
              return json({'assessmentVersion': version});
            }
            if (request.url.path.endsWith('/$id')) {
              return json({
                'assessment': {...record, 'version': version},
                'decisions': [],
                'evidence': attached ? [image] : [],
              });
            }
            return json({
              'items': [record],
              'nextCursor': null,
            });
          }),
          [
            'operations.assessment.read',
            'operations.assessment.update',
            'operations.assessment.delete',
            'operations.evidence.upload',
            'operations.evidence.read',
          ],
          logs: false,
        );
        await tap(tester, find.text('Coastal review'));
        expect(find.text('Back to Assessments'), findsOneWidget);
        expect(find.text('Find your coastal reviews'), findsNothing);
        await tester.ensureVisible(find.byTooltip('View evidence image'));
        await tester.pumpAndSettle();
        if (published) {
          expect(find.byTooltip('Remove evidence image'), findsNothing);
          expect(find.text('Add PNG'), findsNothing);
          expect(find.text('Edit draft'), findsNothing);
          expect(find.text('Cancel draft'), findsNothing);
        } else {
          await tap(tester, find.byTooltip('Remove evidence image'));
          expect(find.text('Remove this draft image?'), findsOneWidget);
          final confirm = find.widgetWithText(FilledButton, 'Confirm removal');
          expect(
            tester
                .widget<FilledButton>(confirm)
                .style!
                .backgroundColor!
                .resolve({}),
            Theme.of(tester.element(confirm)).colorScheme.error,
          );
          await tap(tester, find.text('Keep image'));
          expect(deletion, isNull);
          await tap(tester, find.byTooltip('Remove evidence image'));
          await tap(tester, find.text('Confirm removal'));
          expect(find.byTooltip('Remove evidence image'), findsNothing);
          expect(
            find.text('No evidence images have been added.'),
            findsOneWidget,
          );
          expect(
            deletion!.url.path,
            '/api/operations/assessments/$id/evidence/$imageId',
          );
          expect(jsonDecode(deletion!.body), {'expectedVersion': 2});
          expect(
            deletion!.headers['content-type'],
            contains('application/json'),
          );
        }
        expect(find.byType(AppBar), findsOneWidget);
        expect(find.text('BLUEVERSE · Care for the coast'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'MOB-OPS-LOGS-003 failed collection read retains cards and retries (ui-integration: coastal-operations-logs)',
    (tester) async {
      var fail = false;
      final pending = Completer<http.Response>();
      await open(
        tester,
        MockClient((request) async {
          if (request.url.queryParameters['search'] == 'pending') {
            return pending.future;
          }
          return fail
              ? json({}, 503)
              : json({
                  'items': [review],
                  'nextCursor': null,
                });
        }),
        ['operations.audit.read', 'operations.assessment.read'],
      );
      await tester.enterText(find.byType(TextFormField).first, 'pending');
      await tester.pump(const Duration(seconds: 1));
      await tester.pump();
      expect(find.text('Coastal review'), findsOneWidget);
      expect(find.bySemanticsLabel('Searching records'), findsOneWidget);
      await tester.tap(find.byTooltip('Reset filters'));
      await tester.pumpAndSettle();
      pending.complete(
        json({
          'items': [
            {...review, 'title': 'Stale result'},
          ],
          'nextCursor': null,
        }),
      );
      await tester.pumpAndSettle();
      expect(find.text('Stale result'), findsNothing);
      fail = true;
      await tap(tester, find.text('Drafts'));
      expect(find.text('Coastal review'), findsOneWidget);
      expect(find.text('Retry logs'), findsOneWidget);
      fail = false;
      await tap(tester, find.text('Retry logs'));
      expect(find.text('Retry logs'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
}
