import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/coastal_operations_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/coastal_operations_screen.dart';
import 'package:mobile/ui/coastal_operations_logs_screen.dart';
import 'package:mobile/ui/coastal_operations_activity.dart';

import 'support/mobile_test_support.dart';

const id = '11111111-2222-4333-8444-555555555555';
const review = {
  'assessmentId': id,
  'workflowId': id,
  'title': 'Review beach access',
  'objective': 'Inspect dunes',
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
const alert = {
  'alertId': id,
  'title': 'Coastal notice',
  'description': 'Stay on marked paths',
  'lifecycle': 'PROPOSED',
  'targetType': 'ACTIVITY',
  'targetId': id,
  'version': 2,
  'severity': 'LOW',
  'visibility': 'PUBLIC',
  'validFrom': '2026-10-01T08:00Z',
  'validUntil': '2026-10-01T10:00Z',
  'createdAt': '2026-10-01T08:00Z',
  'updatedAt': '2026-10-01T09:00Z',
};
const options = {
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
http.Response json(Object value, [int status = 200]) => http.Response(
  jsonEncode(value),
  status,
  headers: {'content-type': 'application/json'},
);

void main() {
  Future<void> open(
    WidgetTester tester,
    MockClient client, {
    bool assessments = true,
    String? view,
    bool logs = false,
    List<String>? grants,
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
    final user = mobileTestUser(
      permissions:
          grants ??
          [
            'operations.assessment.create',
            'operations.assessment.read',
            'operations.assessment.update',
            'operations.alert.create',
            'operations.alert.read',
            'operations.alert.update',
            'operations.audit.read',
          ],
    );
    final repository = FakeAuthRepository(user: user);
    final model = AuthViewModel(repository: repository)..user = user;
    addTearDown(model.dispose);
    addTearDown(repository.close);
    addTearDown(authClient.close);
    addTearDown(client.close);
    await tester.pumpWidget(
      MaterialApp(
        home: logs
            ? CoastalOperationsLogsScreen(viewModel: model, apiService: api)
            : CoastalOperationsScreen(
                viewModel: model,
                apiService: api,
                section: assessments
                    ? CoastalOperationsSection.assessments
                    : CoastalOperationsSection.alerts,
                initialView: view,
                initialRecordId: id,
              ),
        onGenerateRoute: (settings) {
          final uri = Uri.parse(settings.name!);
          if (uri.path == '/operations/logs') {
            return MaterialPageRoute<void>(
              settings: settings,
              builder: (_) => CoastalOperationsLogsScreen(
                viewModel: model,
                apiService: api,
              ),
            );
          }
          return MaterialPageRoute<void>(
            settings: settings,
            builder: (_) => CoastalOperationsScreen(
              viewModel: model,
              apiService: api,
              section: uri.path.endsWith('alerts')
                  ? CoastalOperationsSection.alerts
                  : CoastalOperationsSection.assessments,
              initialView: uri.queryParameters['view'],
              initialRecordId: uri.queryParameters['id'],
              fromLogs: uri.queryParameters['origin'] == 'logs',
            ),
          );
        },
      ),
    );
    await tester.pumpAndSettle();
  }

  http.Response fixture(http.Request request, {String status = 'DRAFT'}) {
    if (request.url.path.endsWith('/form-options')) return json(options);
    if (request.url.path.endsWith('/audit')) {
      return json({'items': [], 'nextCursor': null});
    }
    if (request.url.path.endsWith('/$id')) {
      return json({
        'assessment': {...review, 'workflowStatus': status},
        'decisions': [],
        'evidence': [],
      });
    }
    return json({
      'items': request.url.path.endsWith('/alerts') ? [alert] : [review],
      'nextCursor': null,
    });
  }

  for (final assessments in [true, false]) {
    testWidgets(
      'MOB-OPS-NAV-001 ${assessments ? 'assessment' : 'alert'} create/edit view intent restores the full workspace (ui-integration: coastal-operations-${assessments ? 'assessment' : 'alerts'})',
      (tester) async {
        for (final view in ['create', 'edit']) {
          await open(
            tester,
            MockClient((request) async => fixture(request)),
            assessments: assessments,
            view: view,
          );
          expect(
            find.text(view == 'create' ? 'Save draft' : 'Update draft'),
            findsOneWidget,
          );
          expect(find.byTooltip('Search'), findsNothing);
          expect(
            find.text('Back to ${assessments ? 'Assessments' : 'Alerts'}'),
            findsOneWidget,
          );
          if (view == 'edit') {
            expect(
              find.widgetWithText(
                TextFormField,
                assessments ? 'Review beach access' : 'Coastal notice',
              ),
              findsOneWidget,
            );
          }
          expect(find.text('BLUEVERSE · Care for the coast'), findsOneWidget);
          await tester.pumpWidget(const SizedBox());
        }
      },
    );
  }
  testWidgets(
    'MOB-OPS-NAV-002 published and denied edit targets recover without mutation (ui-integration: coastal-operations-assessment)',
    (tester) async {
      final methods = <String>[];
      for (final denied in [true, false]) {
        await open(
          tester,
          MockClient((request) async {
            methods.add(request.method);
            return fixture(request, status: 'SUBMITTED');
          }),
          view: 'edit',
          grants: denied ? ['operations.assessment.read'] : null,
        );
        expect(find.text('Update draft'), findsNothing);
        expect(
          find.textContaining(
            denied
                ? 'outside your current access'
                : 'no longer an editable draft',
          ),
          findsOneWidget,
        );
        expect(find.text('Back to Assessments'), findsOneWidget);
        await tester.pumpWidget(const SizedBox());
      }
      expect(methods.every((method) => method == 'GET'), isTrue);
    },
  );
  testWidgets(
    'MOB-OPS-NAV-003 Logs cards open full details and return with pinned category tabs (ui-integration: coastal-operations-logs)',
    (tester) async {
      await open(
        tester,
        MockClient((request) async => fixture(request)),
        logs: true,
      );
      final before = tester.getTopLeft(find.text('Assessment logs'));
      await tester.drag(find.byType(ListView).first, const Offset(0, -300));
      await tester.pumpAndSettle();
      expect(tester.getTopLeft(find.text('Assessment logs')), before);
      await tester.ensureVisible(find.text('Review beach access'));
      await tester.tap(find.text('Review beach access'));
      await tester.pumpAndSettle();
      expect(find.text('Back to Logs'), findsOneWidget);
      expect(find.byTooltip('Search'), findsNothing);
      await tester.tap(find.text('Back to Logs'));
      await tester.pumpAndSettle();
      expect(find.text('Assessment logs'), findsOneWidget);
    },
  );
  for (final assessments in [true, false]) {
    testWidgets(
      'MOB-OPS-NAV-004 ${assessments ? 'assessments' : 'alerts'} cursor pages and all sizes retain pinned tabs (ui-integration: coastal-operations-${assessments ? 'assessment' : 'alerts'})',
      (tester) async {
        final calls = <http.Request>[];
        await open(
          tester,
          MockClient((request) async {
            calls.add(request);
            final older = request.url.queryParameters.containsKey('cursor');
            return json({
              'items': [
                {
                  ...(assessments ? review : alert),
                  'title': older ? 'Older page' : 'First page',
                },
              ],
              'nextCursor': older ? null : 'next',
            });
          }),
          assessments: assessments,
        );
        expect(calls.first.url.queryParameters['pageSize'], '25');
        final tab = find.widgetWithText(
          TextButton,
          assessments ? 'Assessments' : 'Alerts',
        );
        final before = tester.getTopLeft(tab);
        await tester.drag(find.byType(ListView).first, const Offset(0, -400));
        await tester.pumpAndSettle();
        expect(tester.getTopLeft(tab), before);
        await tester.ensureVisible(find.text('Next'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Next'));
        await tester.pumpAndSettle();
        expect(find.text('Older page'), findsOneWidget);
        expect(find.text('First page'), findsNothing);
        expect(calls.last.url.queryParameters['cursor'], 'next');
        await tester.ensureVisible(find.text('Previous'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Previous'));
        await tester.pumpAndSettle();
        expect(find.text('First page'), findsOneWidget);
        expect(calls.last.url.queryParameters.containsKey('cursor'), isFalse);
        final select = find.byType(DropdownButton<int>);
        expect(
          find.text('${assessments ? 'Assessments' : 'Alerts'} per page'),
          findsOneWidget,
        );
        for (final size in [5, 10, 25, 50, 100]) {
          await tester.ensureVisible(select);
          await tester.pumpAndSettle();
          await tester.tap(select);
          await tester.pumpAndSettle();
          await tester.tap(find.text('$size').last);
          await tester.pumpAndSettle();
          expect(calls.last.url.queryParameters['pageSize'], '$size');
          expect(calls.last.url.queryParameters.containsKey('cursor'), isFalse);
        }
      },
    );
  }
  testWidgets(
    'MOB-OPS-NAV-005 Logs empty state uses the assessment workspace copy (ui-integration: coastal-operations-logs)',
    (tester) async {
      await open(
        tester,
        MockClient((_) async => json({'items': [], 'nextCursor': null})),
        logs: true,
      );
      expect(find.text('No assessments to show yet'), findsOneWidget);
      expect(
        find.text('Saved drafts and submitted reviews will appear here.'),
        findsOneWidget,
      );
      expect(find.text('No records match these filters.'), findsNothing);
    },
  );
  testWidgets(
    'MOB-OPS-AUDIT-DETAIL-001 activity shows exact values person roles time and legacy gaps (ui-integration: coastal-operations-assessment)',
    (tester) async {
      final audit = {
        'items': [
          {
            'auditId': id,
            'action': 'UPDATED',
            'summary': 'Updated the draft (assessment)',
            'actorName': 'Coastal Steward',
            'actorRoles': ['Field Officer'],
            'recordTitle': 'Updated title',
            'actorId': id,
            'correlationId': 'edit',
            'createdAt': '2026-10-01T09:00Z',
            'changes': [
              {
                'field': 'Title',
                'before': 'Original title',
                'after': 'Updated title',
              },
            ],
          },
          {
            'auditId': 'older',
            'action': 'CREATED',
            'actorId': id,
            'correlationId': 'old',
            'createdAt': '2026-10-01T08:00Z',
          },
        ],
        'nextCursor': null,
      };
      final client = MockClient((_) async => json(audit));
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
      addTearDown(client.close);
      addTearDown(authClient.close);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: CoastalOperationsActivity(
              apiService: api,
              id: id,
              assessment: true,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        find.textContaining('Coastal Steward · Field Officer'),
        findsOneWidget,
      );
      await tester.tap(find.text('Updated the draft (assessment)'));
      await tester.pumpAndSettle();
      expect(
        find.text('Before: Original title\nAfter: Updated title'),
        findsOneWidget,
      );
      expect(find.textContaining('Role not recorded'), findsOneWidget);
      await tester.tap(find.text('Created a draft'));
      await tester.pumpAndSettle();
      expect(
        find.text('Field details were not recorded for this event.'),
        findsOneWidget,
      );
    },
  );
}
