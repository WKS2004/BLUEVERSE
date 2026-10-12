import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/coastal_operations_api_service.dart';
import 'package:mobile/data/services/auth_credential_store.dart';

class _MemoryCredentialStore implements AuthCredentialStore {
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

Map<String, dynamic> _assessmentJson({String id = 'assessment-1'}) => {
  'assessmentId': id,
  'workflowId': 'workflow-1',
  'targetType': 'DESTINATION',
  'targetId': 'destination-1',
  'sourceWorkflowId': null,
  'periodStartsAt': '2026-10-01T09:00:00+05:30',
  'periodEndsAt': '2026-10-01T12:00:00+05:30',
  'objective': 'Review the access route after heavy rain.',
  'workflowStatus': 'SUBMITTED',
  'aiDependencyStatus': 'NOT_CONNECTED',
  'aiDispatchOutcome': 'NOT_STARTED',
  'aiDispatchRetryable': true,
  'componentDependencies': [
    {
      'service': 'experience-biodiversity',
      'status': 'AVAILABLE',
      'retryable': false,
      'checkedAt': '2026-09-28T04:00:00Z',
    },
  ],
  'version': 1,
  'createdAt': '2026-09-28T04:00:00Z',
  'updatedAt': '2026-09-28T04:00:00Z',
};

Map<String, dynamic> _alertJson({String lifecycle = 'PROPOSED'}) => {
  'alertId': 'alert-1',
  'targetType': 'DESTINATION',
  'targetId': 'destination-1',
  'assessmentId': null,
  'title': 'Rough water near the inlet',
  'description': 'Use the marked shoreline path.',
  'severity': 'HIGH',
  'visibility': 'PUBLIC',
  'lifecycle': lifecycle,
  'validFrom': '2026-10-01T09:00:00Z',
  'validUntil': '2026-10-02T09:00:00Z',
  'version': 1,
};

Map<String, dynamic> _authRefreshJson() => {
  'token': 'renewed-access',
  'expiresAt': '2026-10-02T12:00:00Z',
  'deviceId': 'device-1',
  'deviceKey': 'device-proof',
  'refreshToken': 'renewed-refresh',
  'sessionExpiresAt': '2026-11-02T12:00:00Z',
  'rememberMe': false,
  'user': {
    'id': 'account-1',
    'email': 'member@example.test',
    'fullName': 'Coastal Member',
    'isActive': true,
    'createdAt': '2026-09-01T00:00:00Z',
    'roles': ['Reviewer'],
    'permissions': ['operations.assessment.read'],
  },
};

void main() {
  group('coastal-operations-assessment public API boundary (ui-integration: coastal-operations-assessment)', () {
    late _MemoryCredentialStore storage;
    late http.Client authClient;
    late AuthApiService authApi;

    setUp(() {
      storage = _MemoryCredentialStore()
        ..values.addAll({
          'blueverse.active_account_id': 'account-1',
          'blueverse.access_token.account-1': 'current-access',
        });
      authClient = MockClient((_) async => http.Response('{}', 401));
      authApi = AuthApiService(
        client: authClient,
        storage: storage,
        gateway: const ApiGatewayConfig(),
      );
    });

    tearDown(() {
      authClient.close();
    });

    test('MOB-OPS-API-001 queue reads the public assessment route using the active account token', () async {
      late http.Request request;
      final client = MockClient((incoming) async {
        request = incoming;
        return http.Response(
          jsonEncode({
            'items': [_assessmentJson()],
            'nextCursor': 'next-page',
          }),
          200,
          headers: {'content-type': 'application/json'},
        );
      });
      addTearDown(client.close);
      final service = CoastalOperationsApiService(
        authApiService: authApi,
        client: client,
        gateway: const ApiGatewayConfig(),
      );

      final result = await service.listAssessments();

      expect(request.method, 'GET');
      expect(request.url.path, '/api/operations/assessments');
      expect(request.url.queryParameters, {'pageSize': '100'});
      expect(request.headers['authorization'], 'Bearer current-access');
      expect(request.headers['accept'], 'application/json');
      expect(
        result.items.single.objective,
        'Review the access route after heavy rain.',
      );
      expect(result.nextCursor, 'next-page');
      service.close();
    });

    test('MOB-OPS-API-002 assessment creation preserves explicit offsets and adds an idempotency key', () async {
      late http.Request request;
      final client = MockClient((incoming) async {
        request = incoming;
        return http.Response(jsonEncode(_assessmentJson()), 201);
      });
      addTearDown(client.close);
      final service = CoastalOperationsApiService(
        authApiService: authApi,
        client: client,
        gateway: const ApiGatewayConfig(),
      );

      final created = await service.createAssessment(
        targetType: 'DESTINATION',
        targetId: 'destination-1',
        periodStartsAt: '2026-10-01T09:00:00+05:30',
        periodEndsAt: '2026-10-01T12:00:00+05:30',
        objective: 'Review the access route after heavy rain.',
      );

      expect(request.method, 'POST');
      expect(request.url.path, '/api/operations/assessments');
      expect(request.headers['authorization'], 'Bearer current-access');
      expect(
        request.headers['idempotency-key'],
        matches(RegExp(r'^mobile-\d+-\d+$')),
      );
      expect(jsonDecode(request.body), {
        'targetType': 'DESTINATION',
        'targetId': 'destination-1',
        'sourceWorkflowId': null,
        'periodStartsAt': '2026-10-01T09:00:00+05:30',
        'periodEndsAt': '2026-10-01T12:00:00+05:30',
        'objective': 'Review the access route after heavy rain.',
      });
      expect(created.aiDependencyStatus, 'NOT_CONNECTED');
      expect(
        created.componentDependencies.single.service,
        'experience-biodiversity',
      );
      service.close();
    });

    test('MOB-OPS-API-003 alert creation and update use public draft routes and preserve optimistic versions', () async {
      final requests = <http.Request>[];
      final client = MockClient((incoming) async {
        requests.add(incoming);
        return http.Response(
          jsonEncode(_alertJson()),
          incoming.method == 'POST' ? 201 : 200,
        );
      });
      addTearDown(client.close);
      final service = CoastalOperationsApiService(
        authApiService: authApi,
        client: client,
        gateway: const ApiGatewayConfig(),
      );

      await service.createAlertDraft(
        targetType: 'DESTINATION',
        targetId: 'destination-1',
        title: 'Rough water near the inlet',
        description: 'Use the marked shoreline path.',
        severity: 'HIGH',
        visibility: 'PUBLIC',
        validFrom: '2026-10-01T09:00:00Z',
        validUntil: '2026-10-02T09:00:00Z',
      );
      await service.updateAlertDraft(
        alertId: 'alert-1',
        expectedVersion: 1,
        title: 'Rough water near the inlet',
        description: 'Stay on the marked shoreline path.',
        severity: 'HIGH',
        visibility: 'PUBLIC',
        validFrom: '2026-10-01T09:00:00Z',
        validUntil: '2026-10-02T09:00:00Z',
      );

      expect(requests.map((request) => request.url.path), [
        '/api/operations/alerts',
        '/api/operations/alerts/alert-1',
      ]);
      expect(jsonDecode(requests[0].body)['assessmentId'], isNull);
      expect(jsonDecode(requests[0].body)['visibility'], 'PUBLIC');
      expect(jsonDecode(requests[1].body), {
        'expectedVersion': 1,
        'title': 'Rough water near the inlet',
        'description': 'Stay on the marked shoreline path.',
        'severity': 'HIGH',
        'visibility': 'PUBLIC',
        'validFrom': '2026-10-01T09:00:00Z',
        'validUntil': '2026-10-02T09:00:00Z',
      });
      service.close();
    });

    test('MOB-OPS-API-004 publishing an advisory requires the public decision route and idempotency key', () async {
      late http.Request request;
      final client = MockClient((incoming) async {
        request = incoming;
        return http.Response('{}', 200);
      });
      addTearDown(client.close);
      final service = CoastalOperationsApiService(
        authApiService: authApi,
        client: client,
        gateway: const ApiGatewayConfig(),
      );

      await service.decideAlert(
        alertId: 'alert-1',
        decision: 'PUBLISH',
        expectedVersion: 1,
      );

      expect(request.method, 'POST');
      expect(request.url.path, '/api/operations/alerts/alert-1/decisions');
      expect(request.headers['authorization'], 'Bearer current-access');
      expect(request.headers['idempotency-key'], startsWith('mobile-'));
      expect(jsonDecode(request.body), {
        'decision': 'PUBLISH',
        'expectedVersion': 1,
      });
      service.close();
    });

    test('MOB-OPS-API-005 evidence upload is multipart PNG and evidence retrieval rejects non-PNG content', () async {
      var requestCount = 0;
      final client = MockClient((incoming) async {
        requestCount++;
        if (requestCount == 1) {
          expect(incoming.method, 'POST');
          expect(
            incoming.url.path,
            '/api/operations/assessments/assessment-1/evidence',
          );
          expect(incoming.headers['authorization'], 'Bearer current-access');
          expect(
            incoming.headers['content-type'],
            startsWith('multipart/form-data; boundary='),
          );
          return http.Response(
            jsonEncode({
              'evidenceId': 'evidence-1',
              'assessmentVersion': 1,
              'mediaType': 'image/png',
              'byteLength': 4,
              'inspectionStatus': 'VALIDATED',
              'uploadedAt': '2026-09-28T04:00:00Z',
              'expiresAt': '2027-09-28T04:00:00Z',
            }),
            201,
          );
        }
        return http.Response(
          'private response',
          200,
          headers: {'content-type': 'application/json'},
        );
      });
      addTearDown(client.close);
      final service = CoastalOperationsApiService(
        authApiService: authApi,
        client: client,
        gateway: const ApiGatewayConfig(),
      );

      final evidence = await service.uploadAssessmentEvidence(
        assessmentId: 'assessment-1',
        fileName: 'shoreline.png',
        bytes: Uint8List.fromList([137, 80, 78, 71]),
      );
      expect(evidence.mediaType, 'image/png');
      await expectLater(
        service.getEvidenceImage(
          assessmentId: 'assessment-1',
          evidenceId: 'evidence-1',
        ),
        throwsA(
          isA<CoastalOperationsApiException>().having(
            (error) => error.statusCode,
            'statusCode',
            502,
          ),
        ),
      );
      service.close();
    });

    test('MOB-OPS-API-006 a 401 refreshes the selected account once and retries with its new token', () async {
      storage.values['blueverse.refresh_token.account-1'] = 'old-refresh';
      var attempts = 0;
      final observedAuthorization = <String?>[];
      final client = MockClient((incoming) async {
        attempts++;
        observedAuthorization.add(incoming.headers['authorization']);
        if (attempts == 1) return http.Response('{}', 401);
        return http.Response(
          jsonEncode({'items': [], 'nextCursor': null}),
          200,
        );
      });
      authClient.close();
      authClient = MockClient((incoming) async {
        expect(incoming.url.path, '/api/auth/refresh');
        expect(jsonDecode(incoming.body)['refreshToken'], 'old-refresh');
        return http.Response(jsonEncode(_authRefreshJson()), 200);
      });
      authApi = AuthApiService(
        client: authClient,
        storage: storage,
        gateway: const ApiGatewayConfig(),
      );
      addTearDown(client.close);
      final service = CoastalOperationsApiService(
        authApiService: authApi,
        client: client,
        gateway: const ApiGatewayConfig(),
      );

      final result = await service.listAssessments();

      expect(result.items, isEmpty);
      expect(attempts, 2);
      expect(observedAuthorization, [
        'Bearer current-access',
        'Bearer renewed-access',
      ]);
      expect(
        storage.values['blueverse.refresh_token.account-1'],
        'renewed-refresh',
      );
      service.close();
    });

    test('MOB-OPS-API-007 permission errors normalize safely and malformed page schemas fail closed', () async {
      final forbiddenClient = MockClient(
        (_) async => http.Response(
          jsonEncode({'detail': 'private upstream hostname and trace'}),
          403,
        ),
      );
      addTearDown(forbiddenClient.close);
      final forbiddenService = CoastalOperationsApiService(
        authApiService: authApi,
        client: forbiddenClient,
        gateway: const ApiGatewayConfig(),
      );
      await expectLater(
        forbiddenService.listAssessments(),
        throwsA(
          isA<CoastalOperationsApiException>()
              .having((error) => error.statusCode, 'statusCode', 403)
              .having(
                (error) => error.message,
                'message',
                'Your current permissions do not allow this action.',
              ),
        ),
      );
      forbiddenService.close();

      final malformedClient = MockClient(
        (_) async => http.Response(
          jsonEncode({'items': 'not-an-array', 'nextCursor': null}),
          200,
        ),
      );
      addTearDown(malformedClient.close);
      final malformedService = CoastalOperationsApiService(
        authApiService: authApi,
        client: malformedClient,
        gateway: const ApiGatewayConfig(),
      );
      await expectLater(
        malformedService.listAssessments(),
        throwsA(
          isA<CoastalOperationsApiException>()
              .having((error) => error.statusCode, 'statusCode', 502)
              .having(
                (error) => error.message,
                'message',
                contains('could not be read'),
              ),
        ),
      );
      malformedService.close();
    });
  });
}
