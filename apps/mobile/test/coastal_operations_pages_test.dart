import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/coastal_operations_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/coastal_operations_screen.dart';

import 'support/mobile_test_support.dart';

const _id = '11111111-2222-4333-8444-555555555555';
const _alert = {
  'alertId': _id,
  'targetType': 'ACTIVITY',
  'targetId': _id,
  'assessmentId': null,
  'title': 'Shoreline notice',
  'description': 'Use the marked path.',
  'severity': 'LOW',
  'visibility': 'PUBLIC',
  'lifecycle': 'PROPOSED',
  'validFrom': '2026-10-01T08:00:00Z',
  'validUntil': '2026-10-01T10:00:00Z',
  'version': 1,
};

void main() {
  Future<List<http.Request>> open(
    WidgetTester tester,
    CoastalOperationsSection section,
    List<String> grants, {
    bool alerts = false,
  }) async {
    final calls = <http.Request>[];
    final client = MockClient((request) async {
      calls.add(request);
      final items = request.url.path.endsWith('/audit')
          ? [
              {
                'auditId': _id,
                'action': 'CREATED',
                'actorId': _id,
                'correlationId': 'publication-test',
                'createdAt': '2026-10-01T08:00:00Z',
              },
            ]
          : alerts
          ? [_alert]
          : [];
      return http.Response(
        jsonEncode({'items': items, 'nextCursor': null}),
        200,
      );
    });
    final authTransport = MockClient((_) async => http.Response('{}', 401));
    final storage = MemoryAuthCredentialStore()
      ..values.addAll({
        'blueverse.active_account_id': 'test-operator',
        'blueverse.access_token.test-operator': 'synthetic-access',
      });
    final auth = AuthApiService(client: authTransport, storage: storage);
    final operations = CoastalOperationsApiService(
      authApiService: auth,
      client: client,
    );
    final user = mobileTestUser(permissions: grants);
    final repository = FakeAuthRepository(user: user);
    final model = AuthViewModel(repository: repository)..user = user;
    addTearDown(model.dispose);
    addTearDown(repository.close);
    addTearDown(client.close);
    addTearDown(authTransport.close);
    await tester.pumpWidget(
      mobileTestApp(
        home: CoastalOperationsScreen(
          section: section,
          viewModel: model,
          apiService: operations,
        ),
      ),
    );
    await tester.pumpAndSettle();
    return calls;
  }

  testWidgets(
    'MOB-OPS-PAGES-004 dedicated assessment page never loads alerts (ui-integration: coastal-operations-assessment)',
    (tester) async {
      final calls = await open(tester, CoastalOperationsSection.assessments, [
        'operations.assessment.read',
        'operations.alert.read',
      ]);
      expect(calls, hasLength(1));
      expect(calls.single.url.path, '/api/operations/assessments');
      expect(find.text('Find your coastal reviews'), findsOneWidget);
      expect(find.text('Useful updates for the coast'), findsNothing);
      expect(find.text('Prepare an advisory'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'MOB-OPS-PAGES-005 publish-only grant exposes publication and activity without CRUD (ui-integration: coastal-operations-alerts)',
    (tester) async {
      final calls = await open(tester, CoastalOperationsSection.alerts, [
        'operations.alert.publish',
        'operations.audit.read',
        'operations.assessment.read',
      ], alerts: true);
      expect(calls, hasLength(1));
      expect(calls.single.url.path, '/api/operations/alerts');
      expect(find.text('Publish advisory'), findsOneWidget);
      for (final action in [
        'Edit draft',
        'Withdraw draft',
        'Resolve advisory',
        'Prepare an advisory',
        'New assessment',
      ]) {
        expect(find.text(action), findsNothing);
      }
      await tester.ensureVisible(find.text('View activity'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('View activity'));
      await tester.pumpAndSettle();
      expect(find.text('Record activity'), findsOneWidget);
      expect(find.text('Created a draft'), findsOneWidget);
      expect(calls.last.url.path, '/api/operations/alerts/$_id/audit');
      expect(calls.last.headers['authorization'], 'Bearer synthetic-access');
      expect(tester.takeException(), isNull);
    },
  );
}
