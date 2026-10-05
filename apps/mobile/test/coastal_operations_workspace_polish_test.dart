import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/coastal_operations_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/coastal_operations_activity.dart';
import 'package:mobile/ui/coastal_operations_logs_screen.dart';
import 'package:mobile/ui/coastal_operations_screen.dart';

import 'support/mobile_test_support.dart';

const _id = '11111111-2222-4333-8444-555555555555';
const _alert = {
  'alertId': _id,
  'title': 'Coastal notice',
  'description': 'Stay on marked paths',
  'lifecycle': 'PROPOSED',
  'targetType': 'ACTIVITY',
  'targetId': _id,
  'version': 2,
  'severity': 'LOW',
  'visibility': 'PUBLIC',
  'validFrom': '2026-10-01T08:00Z',
  'validUntil': '2026-10-01T10:00Z',
  'createdAt': '2026-10-01T08:00Z',
  'updatedAt': '2026-10-01T09:00Z',
};

http.Response _json(Object value) => http.Response(
  jsonEncode(value),
  200,
  headers: {'content-type': 'application/json'},
);

class _RouteRecorder extends NavigatorObserver {
  final names = <String>[];
  @override
  void didReplace({Route<dynamic>? newRoute, Route<dynamic>? oldRoute}) {
    if (newRoute?.settings.name != null) names.add(newRoute!.settings.name!);
    super.didReplace(newRoute: newRoute, oldRoute: oldRoute);
  }
}

void main() {
  Future<
    (CoastalOperationsApiService, AuthViewModel, FakeAuthRepository, MockClient)
  >
  setup(MockClient apiClient, List<String> permissions) async {
    final authClient = MockClient((_) async => _json({}));
    final storage = MemoryAuthCredentialStore()
      ..values.addAll({
        'blueverse.active_account_id': 'test-operator',
        'blueverse.access_token.test-operator': 'synthetic-access',
      });
    final api = CoastalOperationsApiService(
      authApiService: AuthApiService(client: authClient, storage: storage),
      client: apiClient,
    );
    final user = mobileTestUser(permissions: permissions);
    final repository = FakeAuthRepository(user: user);
    final viewModel = AuthViewModel(repository: repository)..user = user;
    return (api, viewModel, repository, authClient);
  }

  testWidgets(
    'MOB-OPS-UX-001 Logs selection is written into its route and restored after replacement (ui-integration: coastal-operations-logs)',
    (tester) async {
      final requests = <http.Request>[];
      final client = MockClient((request) async {
        requests.add(request);
        final alert = request.url.path.endsWith('/alerts');
        return _json({
          'items': alert ? [_alert] : [],
          'nextCursor': null,
        });
      });
      final (api, viewModel, repository, authClient) = await setup(client, [
        'operations.audit.read',
        'operations.assessment.read',
        'operations.alert.read',
      ]);
      addTearDown(viewModel.dispose);
      addTearDown(repository.close);
      addTearDown(authClient.close);
      addTearDown(client.close);
      final routes = _RouteRecorder();
      await tester.pumpWidget(
        MaterialApp(
          navigatorObservers: [routes],
          home: CoastalOperationsLogsScreen(
            viewModel: viewModel,
            apiService: api,
          ),
          onGenerateRoute: (settings) {
            final uri = Uri.parse(settings.name!);
            if (uri.path != '/operations/logs') return null;
            return MaterialPageRoute<void>(
              settings: settings,
              builder: (_) => CoastalOperationsLogsScreen(
                viewModel: viewModel,
                apiService: api,
                initialAlerts: uri.queryParameters['kind'] == 'alerts',
              ),
            );
          },
        ),
      );
      await tester.pumpAndSettle();
      expect(requests.last.url.path, '/api/operations/logs/assessments');
      await tester.tap(find.text('Alert logs'));
      await tester.pumpAndSettle();
      expect(routes.names, contains('/operations/logs?kind=alerts'));
      expect(requests.last.url.path, '/api/operations/logs/alerts');
      expect(find.text('Coastal notice'), findsOneWidget);
      final alertTab = tester.widget<ChoiceChip>(
        find.widgetWithText(ChoiceChip, 'Alert logs'),
      );
      expect(alertTab.selected, isTrue);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'MOB-OPS-UX-002 Logs detail is read-only and its activity shows a reference without actor UUID (ui-integration: coastal-operations-logs)',
    (tester) async {
      final client = MockClient((request) async {
        if (request.url.path.endsWith('/audit')) {
          return _json({
            'items': [
              {
                'auditId': _id,
                'action': 'UPDATED',
                'summary': 'Updated the advisory',
                'actorId': _id,
                'actorName': 'Coastal Steward',
                'actorRoles': ['Field Officer'],
                'correlationId': 'coastal-ref-42',
                'createdAt': '2026-10-01T09:00Z',
                'changes': [],
              },
            ],
            'nextCursor': null,
          });
        }
        return _json({
          'items': [_alert],
          'nextCursor': null,
        });
      });
      final (api, viewModel, repository, authClient) = await setup(client, [
        'operations.audit.read',
        'operations.alert.read',
        'operations.alert.update',
        'operations.alert.delete',
        'operations.alert.publish',
      ]);
      addTearDown(viewModel.dispose);
      addTearDown(repository.close);
      addTearDown(authClient.close);
      addTearDown(client.close);
      await tester.pumpWidget(
        MaterialApp(
          home: CoastalOperationsScreen(
            viewModel: viewModel,
            apiService: api,
            section: CoastalOperationsSection.alerts,
            initialView: 'detail',
            initialRecordId: _id,
            fromLogs: true,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Back to Logs'), findsOneWidget);
      expect(
        find.textContaining('records opened from Logs are read-only'),
        findsOneWidget,
      );
      for (final action in [
        'Edit draft',
        'Withdraw draft',
        'Publish advisory',
        'Resolve advisory',
      ]) {
        expect(find.text(action), findsNothing);
      }
      await tester.tap(find.text('View activity'));
      await tester.pumpAndSettle();
      expect(find.byType(CoastalOperationsActivity), findsOneWidget);
      await tester.tap(find.text('Updated the advisory'));
      await tester.pumpAndSettle();
      expect(find.text('Reference details'), findsOneWidget);
      expect(find.text('coastal-ref-42'), findsOneWidget);
      expect(find.textContaining('Actor: $_id'), findsNothing);
      expect(find.textContaining('Coastal Steward'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
}
