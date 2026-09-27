import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/coastal_operations_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/coastal_operations_screen.dart';

import 'support/mobile_test_support.dart';

const _targetUuid = '00000000-0000-4000-8000-000000000001';
const _assessmentUuid = '00000000-0000-4000-8000-000000000002';

Map<String, dynamic> _assessmentJson() => {
  'assessmentId': _assessmentUuid,
  'workflowId': '00000000-0000-4000-8000-000000000003',
  'targetType': 'DESTINATION',
  'targetId': _targetUuid,
  'sourceWorkflowId': null,
  'periodStartsAt': '2026-10-01T09:00:00+05:30',
  'periodEndsAt': '2026-10-01T12:00:00+05:30',
  'objective': 'Review the access route after heavy rain.',
  'workflowStatus': 'SUBMITTED',
  'aiDependencyStatus': 'NOT_CONNECTED',
  'aiDispatchOutcome': 'NOT_STARTED',
  'aiDispatchRetryable': true,
  'componentDependencies': [],
  'version': 1,
  'createdAt': '2026-09-28T04:00:00Z',
  'updatedAt': '2026-09-28T04:00:00Z',
};

Map<String, dynamic> _alertJson() => {
  'alertId': 'alert-1',
  'targetType': 'DESTINATION',
  'targetId': _targetUuid,
  'assessmentId': null,
  'title': 'Rough water near the inlet',
  'description': 'Use the marked shoreline path.',
  'severity': 'HIGH',
  'visibility': 'PUBLIC',
  'lifecycle': 'PROPOSED',
  'validFrom': '2026-10-01T09:00:00Z',
  'validUntil': '2026-10-02T09:00:00Z',
  'version': 1,
};

void main() {
  group('coastal-operations-assessment cross-platform workflow (ui-integration: coastal-operations-assessment)', () {
    testWidgets(
      'MOB-OPS-UI-001 reviewer permissions show their queue without exposing write actions',
      (tester) async {
        final requests = <http.Request>[];
        final client = MockClient((request) async {
          requests.add(request);
          expect(request.url.path, '/api/operations/assessments');
          expect(request.url.queryParameters, {'pageSize': '100'});
          return http.Response(
            jsonEncode({'items': [], 'nextCursor': null}),
            200,
          );
        });
        final authTransport = MockClient((_) async => http.Response('{}', 401));
        final storage = MemoryAuthCredentialStore()
          ..values.addAll({
            'blueverse.active_account_id': 'reviewer-1',
            'blueverse.access_token.reviewer-1': 'reviewer-access',
          });
        final operations = CoastalOperationsApiService(
          authApiService: AuthApiService(
            client: authTransport,
            storage: storage,
            gateway: const ApiGatewayConfig(),
          ),
          client: client,
          gateway: const ApiGatewayConfig(),
        );
        final user = mobileTestUser(
          permissions: const [
            'operations.assessment.read',
            'operations.assessment.queue.read',
          ],
        );
        final repository = FakeAuthRepository(user: user);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(client.close);
        addTearDown(authTransport.close);
        addTearDown(repository.close);
        addTearDown(viewModel.dispose);

        await tester.pumpWidget(
          mobileTestApp(
            home: CoastalOperationsScreen(
              viewModel: viewModel,
              apiService: operations,
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Review queue'), findsOneWidget);
        expect(find.text('No assessments to show yet'), findsOneWidget);
        expect(find.text('New assessment'), findsNothing);
        expect(find.text('Prepare an advisory'), findsNothing);
        expect(requests, hasLength(1));
        expect(
          requests.single.headers['authorization'],
          'Bearer reviewer-access',
        );
      },
    );

    testWidgets(
      'MOB-OPS-UI-002 assessment creators submit an offset-aware review without queue access',
      (tester) async {
        late http.Request createRequest;
        var requestCount = 0;
        final client = MockClient((request) async {
          requestCount++;
          createRequest = request;
          expect(request.method, 'POST');
          expect(request.url.path, '/api/operations/assessments');
          return http.Response(jsonEncode(_assessmentJson()), 201);
        });
        final authTransport = MockClient((_) async => http.Response('{}', 401));
        final storage = MemoryAuthCredentialStore()
          ..values.addAll({
            'blueverse.active_account_id': 'creator-1',
            'blueverse.access_token.creator-1': 'creator-access',
          });
        final operations = CoastalOperationsApiService(
          authApiService: AuthApiService(
            client: authTransport,
            storage: storage,
            gateway: const ApiGatewayConfig(),
          ),
          client: client,
          gateway: const ApiGatewayConfig(),
        );
        final user = mobileTestUser(
          permissions: const ['operations.assessment.create'],
        );
        final repository = FakeAuthRepository(user: user);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(client.close);
        addTearDown(authTransport.close);
        addTearDown(repository.close);
        addTearDown(viewModel.dispose);

        await tester.pumpWidget(
          mobileTestApp(
            home: CoastalOperationsScreen(
              viewModel: viewModel,
              apiService: operations,
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(requestCount, 0);
        await tester.tap(find.text('New assessment'));
        await tester.pumpAndSettle();
        await tester.tap(
          find.widgetWithText(FilledButton, 'Record assessment'),
        );
        await tester.pumpAndSettle();
        expect(find.text('Enter a valid UUID.'), findsOneWidget);
        expect(find.text('Include an explicit UTC offset.'), findsNWidgets(2));
        expect(requestCount, 0);

        Future<void> enter(String label, String value) async {
          final field = find.bySemanticsLabel(label);
          await tester.ensureVisible(field);
          await tester.enterText(field, value);
        }

        await enter('Canonical record ID', _targetUuid);
        await enter('Period starts at', '2026-10-01T09:00:00+05:30');
        await enter('Period ends at', '2026-10-01T12:00:00+05:30');
        await enter(
          'What should the team assess?',
          'Review the access route after heavy rain.',
        );
        await tester.tap(
          find.widgetWithText(FilledButton, 'Record assessment'),
        );
        await tester.pumpAndSettle();

        expect(requestCount, 1);
        expect(createRequest.headers['authorization'], 'Bearer creator-access');
        expect(createRequest.headers['idempotency-key'], startsWith('mobile-'));
        expect(jsonDecode(createRequest.body), {
          'targetType': 'DESTINATION',
          'targetId': _targetUuid,
          'sourceWorkflowId': null,
          'periodStartsAt': '2026-10-01T09:00:00+05:30',
          'periodEndsAt': '2026-10-01T12:00:00+05:30',
          'objective': 'Review the access route after heavy rain.',
        });
        expect(
          find.text(
            'The assessment was recorded. Its linked coastal context is ready to review.',
          ),
          findsOneWidget,
        );
        operations.close();
      },
    );

    testWidgets(
      'MOB-OPS-UI-003 advisory managers may edit a proposed draft but cannot publish without decide permission',
      (tester) async {
        final client = MockClient((request) async {
          expect(request.url.path, '/api/operations/alerts');
          return http.Response(
            jsonEncode({
              'items': [_alertJson()],
              'nextCursor': null,
            }),
            200,
          );
        });
        final authTransport = MockClient((_) async => http.Response('{}', 401));
        final operations = CoastalOperationsApiService(
          authApiService: AuthApiService(
            client: authTransport,
            storage: MemoryAuthCredentialStore(),
            gateway: const ApiGatewayConfig(),
          ),
          client: client,
          gateway: const ApiGatewayConfig(),
        );
        final user = mobileTestUser(
          permissions: const [
            'operations.alert.read',
            'operations.alert.manage',
          ],
        );
        final repository = FakeAuthRepository(user: user);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(client.close);
        addTearDown(authTransport.close);
        addTearDown(repository.close);
        addTearDown(viewModel.dispose);

        await tester.pumpWidget(
          mobileTestApp(
            home: CoastalOperationsScreen(
              viewModel: viewModel,
              apiService: operations,
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Rough water near the inlet'), findsOneWidget);
        expect(find.text('Edit draft'), findsOneWidget);
        expect(find.text('Publish advisory'), findsNothing);
        operations.close();
      },
    );

    testWidgets(
      'MOB-OPS-UI-004 status-only and history-only access can look up a canonical coastal record',
      (tester) async {
        final paths = <String>[];
        final client = MockClient((request) async {
          paths.add(request.url.path);
          if (request.url.path.endsWith('/status')) {
            return http.Response(
              jsonEncode({
                'targetType': 'DESTINATION',
                'targetId': _targetUuid,
                'operationalState': 'OPEN',
                'stateVersion': 1,
                'updatedAt': '2026-09-28T04:00:00Z',
              }),
              200,
            );
          }
          if (request.url.path.endsWith('/history')) {
            return http.Response(
              jsonEncode({
                'targetType': 'DESTINATION',
                'targetId': _targetUuid,
                'items': [],
                'nextCursor': null,
              }),
              200,
            );
          }
          throw StateError('Unexpected route ${request.url.path}');
        });
        final authTransport = MockClient((_) async => http.Response('{}', 401));
        final operations = CoastalOperationsApiService(
          authApiService: AuthApiService(
            client: authTransport,
            storage: MemoryAuthCredentialStore(),
            gateway: const ApiGatewayConfig(),
          ),
          client: client,
          gateway: const ApiGatewayConfig(),
        );
        final user = mobileTestUser(
          permissions: const [
            'operations.target.status.read',
            'operations.target.history.read',
          ],
        );
        final repository = FakeAuthRepository(user: user);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(client.close);
        addTearDown(authTransport.close);
        addTearDown(repository.close);
        addTearDown(viewModel.dispose);

        await tester.pumpWidget(
          mobileTestApp(
            home: CoastalOperationsScreen(
              viewModel: viewModel,
              apiService: operations,
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.enterText(
          find.bySemanticsLabel('Canonical record ID'),
          _targetUuid,
        );
        await tester.drag(find.byType(ListView), const Offset(0, -1000));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Check record'));
        await tester.pumpAndSettle();

        expect(find.text('Open'), findsOneWidget);
        expect(
          find.text('No state changes have been recorded for this record.'),
          findsOneWidget,
        );
        expect(paths, [
          '/api/operations/targets/DESTINATION/$_targetUuid/status',
          '/api/operations/targets/DESTINATION/$_targetUuid/history',
        ]);
        operations.close();
      },
    );
  });
}
