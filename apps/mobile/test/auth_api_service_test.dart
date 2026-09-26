import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/repositories/auth_repository.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/auth_credential_store.dart';

class MemoryAuthCredentialStore implements AuthCredentialStore {
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

Map<String, dynamic> authResponse({
  String token = 'synthetic-access',
  String refreshToken = 'synthetic-refresh',
}) => {
  'token': token,
  'expiresAt': '2026-09-23T12:15:00Z',
  'deviceId': 'synthetic-device',
  'deviceKey': 'synthetic-device-key',
  'refreshToken': refreshToken,
  'sessionExpiresAt': '2026-09-24T12:00:00Z',
  'rememberMe': false,
  'user': {
    'id': 'synthetic-user',
    'email': 'person@example.test',
    'fullName': 'Example Person',
    'isActive': true,
    'createdAt': '2026-09-01T00:00:00Z',
    'roles': ['User'],
    'permissions': <String>[],
  },
};

Map<String, dynamic> sessionResponse() => {
  'id': 'synthetic-session',
  'deviceId': 'synthetic-device',
  'createdAt': '2026-09-23T12:00:00Z',
  'lastSeenAt': '2026-09-23T12:01:00Z',
  'expiresAt': '2026-09-24T12:00:00Z',
  'rememberMe': false,
  'isCurrent': true,
};

void main() {
  // All cases trace to the registered auth-session-management workflow.
  group('auth-session-management public API boundary', () {
    test('MOB-AUTH-001 login sends native credentials to the public API and stores the session', () async {
      final storage = MemoryAuthCredentialStore();
      storage.values.addAll({
        'blueverse.device_id': 'existing-device',
        'blueverse.device_key': 'existing-device-key',
      });
      late http.Request request;
      final client = MockClient((incoming) async {
        request = incoming;
        return http.Response(jsonEncode(authResponse()), 200);
      });
      addTearDown(client.close);

      final result = await AuthApiService(client: client, storage: storage)
          .login(
            email: 'person@example.test',
            password: 'synthetic-password',
            rememberMe: true,
          );

      expect(request.method, 'POST');
      expect(request.url.toString(), 'http://localhost/api/auth/login');
      expect(request.headers['content-type'], contains('application/json'));
      expect(jsonDecode(request.body), {
        'email': 'person@example.test',
        'password': 'synthetic-password',
        'deviceId': 'existing-device',
        'deviceKey': 'existing-device-key',
        'rememberMe': true,
        'useCookies': false,
      });
      expect(result.user.email, 'person@example.test');
      expect(storage.values['blueverse.device_id'], 'synthetic-device');
      expect(storage.values['blueverse.device_key'], 'synthetic-device-key');
      expect(storage.values['blueverse.access_token'], 'synthetic-access');
      expect(storage.values['blueverse.refresh_token'], 'synthetic-refresh');
    });

    test('MOB-AUTH-002 rejected login surfaces the problem and stores no bearer tokens', () async {
      final storage = MemoryAuthCredentialStore();
      final client = MockClient(
        (_) async => http.Response(
          jsonEncode({
            'status': 401,
            'title': 'Authentication Failed',
            'detail': 'Invalid credentials.',
          }),
          401,
        ),
      );
      addTearDown(client.close);

      await expectLater(
        AuthApiService(client: client, storage: storage).login(
          email: 'person@example.test',
          password: 'synthetic-wrong-password',
          rememberMe: false,
        ),
        throwsA(
          isA<AuthApiException>()
              .having((error) => error.statusCode, 'status', 401)
              .having(
                (error) => error.message,
                'detail',
                'Invalid credentials.',
              ),
        ),
      );
      expect(storage.values, isEmpty);
    });

    test(
      'MOB-AUTH-003 refresh requires a stored token before making a request',
      () async {
        final storage = MemoryAuthCredentialStore();
        var requests = 0;
        final client = MockClient((_) async {
          requests++;
          return http.Response('{}', 200);
        });
        addTearDown(client.close);

        await expectLater(
          AuthApiService(client: client, storage: storage).refresh(),
          throwsA(
            isA<AuthApiException>().having(
              (error) => error.statusCode,
              'status',
              401,
            ),
          ),
        );
        expect(requests, 0);
        expect(storage.values, isEmpty);
      },
    );

    test(
      'MOB-AUTH-004 refresh sends device proof and replaces bearer tokens',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values.addAll({
          'blueverse.device_id': 'synthetic-device',
          'blueverse.device_key': 'synthetic-device-key',
          'blueverse.access_token': 'old-access',
          'blueverse.refresh_token': 'old-refresh',
        });
        late http.Request request;
        final client = MockClient((incoming) async {
          request = incoming;
          return http.Response(jsonEncode(authResponse()), 200);
        });
        addTearDown(client.close);

        await AuthApiService(client: client, storage: storage).refresh();

        expect(request.method, 'POST');
        expect(request.url.path, '/api/auth/refresh');
        expect(jsonDecode(request.body), {
          'refreshToken': 'old-refresh',
          'deviceId': 'synthetic-device',
          'deviceKey': 'synthetic-device-key',
          'useCookies': false,
        });
        expect(storage.values['blueverse.access_token'], 'synthetic-access');
        expect(storage.values['blueverse.refresh_token'], 'synthetic-refresh');
      },
    );

    test(
      'MOB-AUTH-005 restore refreshes after a 401 and returns the current user',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values.addAll({
          'blueverse.device_id': 'synthetic-device',
          'blueverse.device_key': 'synthetic-device-key',
          'blueverse.access_token': 'expired-access',
          'blueverse.refresh_token': 'old-refresh',
        });
        final paths = <String>[];
        final client = MockClient((request) async {
          paths.add(request.url.path);
          if (request.url.path == '/api/auth/me') {
            expect(request.headers['authorization'], 'Bearer expired-access');
            return http.Response('', 401);
          }
          expect(request.url.path, '/api/auth/refresh');
          return http.Response(jsonEncode(authResponse()), 200);
        });
        addTearDown(client.close);

        final user = await AuthRepository(
          apiService: AuthApiService(client: client, storage: storage),
        ).restore();

        expect(paths, ['/api/auth/me', '/api/auth/refresh']);
        expect(user.email, 'person@example.test');
        expect(storage.values['blueverse.access_token'], 'synthetic-access');
        expect(storage.values['blueverse.refresh_token'], 'synthetic-refresh');
      },
    );

    test(
      'MOB-AUTH-006 sessions uses the bearer token and parses active sessions',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values['blueverse.access_token'] = 'synthetic-access';
        late http.Request request;
        final client = MockClient((incoming) async {
          request = incoming;
          return http.Response(jsonEncode([sessionResponse()]), 200);
        });
        addTearDown(client.close);

        final sessions = await AuthApiService(
          client: client,
          storage: storage,
        ).sessions();

        expect(request.method, 'GET');
        expect(request.url.path, '/api/auth/sessions');
        expect(request.headers['authorization'], 'Bearer synthetic-access');
        expect(sessions, hasLength(1));
        expect(sessions.single.id, 'synthetic-session');
        expect(sessions.single.isCurrent, isTrue);
      },
    );

    test(
      'MOB-AUTH-007 malformed session items fail instead of disappearing',
      () async {
        final storage = MemoryAuthCredentialStore();
        final client = MockClient(
          (_) async => http.Response(
            jsonEncode([sessionResponse(), 'invalid-item']),
            200,
          ),
        );
        addTearDown(client.close);

        await expectLater(
          AuthApiService(client: client, storage: storage).sessions(),
          throwsA(isA<FormatException>()),
        );
      },
    );

    test(
      'MOB-AUTH-008 successful logout removes only the active account locally',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values.addAll({
          'blueverse.device_id': 'synthetic-device',
          'blueverse.device_key': 'synthetic-device-key',
          'blueverse.active_account_id': 'user-current',
          'blueverse.access_token.user-current': 'synthetic-access',
          'blueverse.refresh_token.user-current': 'synthetic-refresh',
          'blueverse.access_token.user-other': 'other-access',
          'blueverse.refresh_token.user-other': 'other-refresh',
          'blueverse.account_ids': jsonEncode([
            {
              'id': 'user-current',
              'fullName': 'Current User',
              'email': 'current@example.test',
            },
            {
              'id': 'user-other',
              'fullName': 'Other User',
              'email': 'other@example.test',
            },
          ]),
        });
        late http.Request request;
        final client = MockClient((incoming) async {
          request = incoming;
          return http.Response('', 204);
        });
        addTearDown(client.close);

        await AuthApiService(
          client: client,
          storage: storage,
        ).logoutCurrentDevice();

        expect(request.method, 'POST');
        expect(request.url.path, '/api/auth/logout-account');
        expect(request.headers['authorization'], 'Bearer synthetic-access');
        expect(jsonDecode(request.body), {'userId': 'user-current'});
        expect(
          storage.values['blueverse.access_token.user-other'],
          'other-access',
        );
        expect(
          storage.values['blueverse.refresh_token.user-other'],
          'other-refresh',
        );
        expect(storage.values['blueverse.active_account_id'], 'user-other');
        expect(
          storage.values.containsKey('blueverse.access_token.user-current'),
          isFalse,
        );
      },
    );

    test(
      'MOB-AUTH-009 failed logout preserves the local session for recovery',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values.addAll({
          'blueverse.active_account_id': 'user-current',
          'blueverse.access_token.user-current': 'synthetic-access',
          'blueverse.account_ids': jsonEncode([
            {
              'id': 'user-current',
              'fullName': 'Current User',
              'email': 'current@example.test',
            },
          ]),
        });
        final client = MockClient(
          (_) async => http.Response(
            jsonEncode({'status': 503, 'detail': 'Service unavailable.'}),
            503,
          ),
        );
        addTearDown(client.close);

        await expectLater(
          AuthApiService(
            client: client,
            storage: storage,
          ).logoutCurrentDevice(),
          throwsA(
            isA<AuthApiException>().having(
              (error) => error.statusCode,
              'status',
              503,
            ),
          ),
        );
        expect(
          storage.values['blueverse.access_token.user-current'],
          'synthetic-access',
        );
      },
    );

    test('MOB-AUTH-011 ending all sessions sends password and preserves other accounts', () async {
      final storage = MemoryAuthCredentialStore();
      storage.values.addAll({
        'blueverse.active_account_id': 'user-current',
        'blueverse.access_token.user-current': 'current-access',
        'blueverse.refresh_token.user-current': 'current-refresh',
        'blueverse.access_token.user-other': 'other-access',
        'blueverse.refresh_token.user-other': 'other-refresh',
        'blueverse.account_ids': jsonEncode([
          {
            'id': 'user-current',
            'fullName': 'Current User',
            'email': 'current@example.test',
          },
          {
            'id': 'user-other',
            'fullName': 'Other User',
            'email': 'other@example.test',
          },
        ]),
      });
      late http.Request request;
      final client = MockClient((incoming) async {
        request = incoming;
        return http.Response('', 204);
      });
      addTearDown(client.close);

      await AuthApiService(
        client: client,
        storage: storage,
      ).logoutAllDevices('verified-password');

      expect(request.method, 'POST');
      expect(request.url.path, '/api/auth/logout-all-devices');
      expect(jsonDecode(request.body), {
        'currentPassword': 'verified-password',
      });
      expect(
        storage.values['blueverse.access_token.user-other'],
        'other-access',
      );
      expect(storage.values['blueverse.active_account_id'], 'user-other');
      expect(
        storage.values.containsKey('blueverse.access_token.user-current'),
        isFalse,
      );
    });

    test(
      'MOB-AUTH-012 remote session revocation sends password verification',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values.addAll({
          'blueverse.active_account_id': 'user-current',
          'blueverse.access_token.user-current': 'current-access',
        });
        late http.Request request;
        final client = MockClient((incoming) async {
          request = incoming;
          return http.Response('', 204);
        });
        addTearDown(client.close);

        await AuthApiService(
          client: client,
          storage: storage,
        ).revokeSession('remote-session', currentPassword: 'verified-password');

        expect(request.method, 'DELETE');
        expect(request.url.path, '/api/auth/sessions/remote-session');
        expect(jsonDecode(request.body), {
          'currentPassword': 'verified-password',
        });
        expect(request.headers['authorization'], 'Bearer current-access');
      },
    );

    test(
      'MOB-AUTH-013 self deletion uses the authenticated public endpoint',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values.addAll({
          'blueverse.active_account_id': 'user-current',
          'blueverse.access_token.user-current': 'current-access',
        });
        late http.Request request;
        final client = MockClient((incoming) async {
          request = incoming;
          return http.Response('', 204);
        });
        addTearDown(client.close);

        await AuthApiService(
          client: client,
          storage: storage,
        ).deleteCurrentUser();

        expect(request.method, 'DELETE');
        expect(request.url.path, '/api/auth/me');
        expect(request.headers['authorization'], 'Bearer current-access');
      },
    );

    test(
      'MOB-AUTH-010 malformed login response cannot create a stored session',
      () async {
        final storage = MemoryAuthCredentialStore();
        final client = MockClient((_) async => http.Response('[]', 200));
        addTearDown(client.close);

        await expectLater(
          AuthApiService(client: client, storage: storage).login(
            email: 'person@example.test',
            password: 'synthetic-password',
            rememberMe: false,
          ),
          throwsA(isA<FormatException>()),
        );
        expect(storage.values, isEmpty);
      },
    );

    test('MOB-AUTH-011 network failure reports the gateway and keeps storage empty', () async {
      final storage = MemoryAuthCredentialStore();
      final client = MockClient(
        (_) async => throw http.ClientException('offline'),
      );
      addTearDown(client.close);

      await expectLater(
        AuthApiService(client: client, storage: storage).login(
          email: 'person@example.test',
          password: 'synthetic-password',
          rememberMe: false,
        ),
        throwsA(
          isA<AuthApiException>()
              .having((error) => error.statusCode, 'status', 0)
              .having((error) => error.message, 'message', contains('gateway')),
        ),
      );
      expect(storage.values, isEmpty);
    });

    test(
      'MOB-AUTH-012 missing native login token preserves the prior session',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values.addAll({
          'blueverse.device_id': 'old-device',
          'blueverse.device_key': 'old-device-key',
          'blueverse.access_token': 'old-access',
          'blueverse.refresh_token': 'old-refresh',
        });
        final priorSession = Map<String, String>.from(storage.values);
        final malformed = authResponse()..remove('token');
        final client = MockClient(
          (_) async => http.Response(jsonEncode(malformed), 200),
        );
        addTearDown(client.close);

        await expectLater(
          AuthApiService(client: client, storage: storage).login(
            email: 'person@example.test',
            password: 'synthetic-password',
            rememberMe: false,
          ),
          throwsA(isA<FormatException>()),
        );
        expect(storage.values, priorSession);
      },
    );

    test(
      'MOB-AUTH-013 missing native refresh token preserves the prior session',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values.addAll({
          'blueverse.device_id': 'old-device',
          'blueverse.device_key': 'old-device-key',
          'blueverse.access_token': 'old-access',
          'blueverse.refresh_token': 'old-refresh',
        });
        final priorSession = Map<String, String>.from(storage.values);
        final malformed = authResponse()..remove('refreshToken');
        final client = MockClient(
          (_) async => http.Response(jsonEncode(malformed), 200),
        );
        addTearDown(client.close);

        await expectLater(
          AuthApiService(client: client, storage: storage).refresh(),
          throwsA(isA<FormatException>()),
        );
        expect(storage.values, priorSession);
      },
    );

    test(
      'MOB-ADMIN-001 permission assignment uses the public role route',
      () async {
        final storage = MemoryAuthCredentialStore();
        storage.values['blueverse.access_token'] = 'admin-token';
        late http.Request request;
        final client = MockClient((incoming) async {
          request = incoming;
          return http.Response(
            jsonEncode({
              'id': 'role-123',
              'name': 'Coastal Editor',
              'permissions': ['auth.user.read'],
            }),
            200,
          );
        });
        addTearDown(client.close);

        final result = await AuthApiService(client: client, storage: storage)
            .adminSetRolePermissions(
              id: 'role-123',
              permissionCodes: ['auth.user.read'],
            );

        expect(request.method, 'POST');
        expect(request.url.path, '/api/auth/roles/role-123/permissions');
        expect(request.headers['authorization'], 'Bearer admin-token');
        expect(jsonDecode(request.body), {
          'permissionCodes': ['auth.user.read'],
        });
        expect(result['permissions'], ['auth.user.read']);
      },
    );

    test('MOB-ADMIN-002 user creation submits account details through the public API', () async {
      final storage = MemoryAuthCredentialStore();
      storage.values['blueverse.access_token'] = 'admin-token';
      late http.Request request;
      final client = MockClient((incoming) async {
        request = incoming;
        return http.Response(
          jsonEncode({
            'id': 'user-123',
            'email': 'new@example.test',
            'fullName': 'New Coastal Member',
            'roles': ['Viewer'],
          }),
          201,
        );
      });
      addTearDown(client.close);

      final result = await AuthApiService(client: client, storage: storage)
          .adminCreateUser(
            email: 'new@example.test',
            password: 'synthetic-password',
            fullName: 'New Coastal Member',
            roleNames: ['Viewer'],
          );

      expect(request.method, 'POST');
      expect(request.url.path, '/api/auth/users');
      expect(request.headers['authorization'], 'Bearer admin-token');
      expect(jsonDecode(request.body), {
        'email': 'new@example.test',
        'password': 'synthetic-password',
        'fullName': 'New Coastal Member',
        'roleNames': ['Viewer'],
      });
      expect(result['fullName'], 'New Coastal Member');
    });
  });
}
