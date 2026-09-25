import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/auth_api_service.dart';

import 'support/mobile_test_support.dart';

void main() {
  group('MOB-AUTH-014 auth registration contract', () {
    test('MOB-AUTH-014a registers through the public API and saves native credentials', () async {
      final storage = MemoryAuthCredentialStore()
        ..values.addAll({
          'blueverse.device_id': 'existing-installation',
          'blueverse.device_key': 'existing-proof',
        });
      late http.Request request;
      final client = MockClient((incoming) async {
        request = incoming;
        return http.Response(jsonEncode(mobileTestAuthResponse()), 201);
      });
      addTearDown(client.close);

      final result = await AuthApiService(client: client, storage: storage)
          .register(
            fullName: 'Coastal Member',
            email: 'member@example.test',
            password: 'synthetic-password',
            rememberMe: true,
          );

      expect(request.method, 'POST');
      expect(request.url.path, '/api/auth/register');
      expect(request.headers['content-type'], contains('application/json'));
      expect(jsonDecode(request.body), {
        'fullName': 'Coastal Member',
        'email': 'member@example.test',
        'password': 'synthetic-password',
        'deviceId': 'existing-installation',
        'deviceKey': 'existing-proof',
        'rememberMe': true,
        'useCookies': false,
      });
      expect(result.user.fullName, 'Coastal Member');
      expect(
        storage.values['blueverse.access_token.user-current'],
        'test-access-token',
      );
      expect(
        storage.values['blueverse.refresh_token.user-current'],
        'test-refresh-token',
      );
      expect(storage.values['blueverse.active_account_id'], 'user-current');
    });

    test('MOB-AUTH-014b duplicate registration gives a safe error and saves no session', () async {
      final storage = MemoryAuthCredentialStore();
      final client = MockClient(
        (_) async => http.Response(
          jsonEncode({
            'status': 400,
            'detail': 'An account with this email already exists.',
          }),
          400,
        ),
      );
      addTearDown(client.close);

      await expectLater(
        AuthApiService(client: client, storage: storage).register(
          fullName: 'Coastal Member',
          email: 'member@example.test',
          password: 'synthetic-password',
          rememberMe: false,
        ),
        throwsA(
          isA<AuthApiException>()
              .having((error) => error.statusCode, 'status', 400)
              .having(
                (error) => error.message,
                'safe message',
                contains('sign in instead'),
              ),
        ),
      );
      expect(storage.values, isEmpty);
    });
  });

  group('MOB-AUTH-015 profile API contract', () {
    test('MOB-AUTH-015a reads and updates the authenticated profile', () async {
      final storage = _authenticatedStorage();
      final requests = <http.Request>[];
      final client = MockClient((request) async {
        requests.add(request);
        if (request.method == 'GET') {
          return http.Response(jsonEncode(mobileTestUserJson()), 200);
        }
        return http.Response(
          jsonEncode(mobileTestUserJson(fullName: 'Updated Member')),
          200,
        );
      });
      addTearDown(client.close);
      final service = AuthApiService(client: client, storage: storage);

      final current = await service.currentUser();
      final updated = await service.updateCurrentUser(
        fullName: 'Updated Member',
      );

      expect(current.id, 'user-current');
      expect(updated.fullName, 'Updated Member');
      expect(requests.map((request) => request.method), ['GET', 'PUT']);
      expect(requests.map((request) => request.url.path), [
        '/api/auth/me',
        '/api/auth/me',
      ]);
      expect(
        requests.every(
          (request) =>
              request.headers['authorization'] == 'Bearer active-access',
        ),
        isTrue,
      );
      expect(jsonDecode(requests[1].body), {'fullName': 'Updated Member'});
      expect(storage.values['blueverse.active_account_id'], 'user-current');
    });
  });

  group('MOB-AUTH-016 password change boundary', () {
    test('MOB-AUTH-016a sends password verification and clears only the active local account after success', () async {
      final storage = _authenticatedStorage()
        ..values.addAll({
          'blueverse.access_token.user-other': 'other-access',
          'blueverse.refresh_token.user-other': 'other-refresh',
          'blueverse.account_ids': jsonEncode([
            {
              'id': 'user-current',
              'fullName': 'Coastal Member',
              'email': 'member@example.test',
            },
            {
              'id': 'user-other',
              'fullName': 'Other Member',
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

      await AuthApiService(client: client, storage: storage).changePassword(
        currentPassword: 'verified-current',
        newPassword: 'synthetic-new-password',
      );

      expect(request.method, 'POST');
      expect(request.url.path, '/api/auth/change-password');
      expect(request.headers['authorization'], 'Bearer active-access');
      expect(jsonDecode(request.body), {
        'currentPassword': 'verified-current',
        'newPassword': 'synthetic-new-password',
      });
      expect(
        storage.values.containsKey('blueverse.access_token.user-current'),
        isFalse,
      );
      expect(
        storage.values['blueverse.access_token.user-other'],
        'other-access',
      );
      expect(storage.values['blueverse.active_account_id'], 'user-other');
    });

    test(
      'MOB-AUTH-016b rejected password change preserves current credentials',
      () async {
        final storage = _authenticatedStorage();
        final before = Map<String, String>.from(storage.values);
        final client = MockClient(
          (_) async => http.Response(
            jsonEncode({
              'status': 400,
              'detail': 'Current password is incorrect.',
            }),
            400,
          ),
        );
        addTearDown(client.close);

        await expectLater(
          AuthApiService(client: client, storage: storage).changePassword(
            currentPassword: 'wrong-password',
            newPassword: 'synthetic-new-password',
          ),
          throwsA(
            isA<AuthApiException>().having(
              (error) => error.message,
              'detail',
              'Current password is incorrect.',
            ),
          ),
        );
        expect(storage.values, before);
      },
    );
  });

  group('MOB-ADMIN-003 administration read contract', () {
    test('MOB-ADMIN-003a all catalog reads use registered public API routes with the active bearer token', () async {
      final storage = _authenticatedStorage();
      final requests = <http.Request>[];
      final client = MockClient((request) async {
        requests.add(request);
        return http.Response('[]', 200);
      });
      addTearDown(client.close);
      final service = AuthApiService(client: client, storage: storage);

      expect(await service.adminPermissions(), isEmpty);
      expect(await service.adminRoles(), isEmpty);
      expect(await service.adminUsers(), isEmpty);

      expect(requests.map((request) => request.method), ['GET', 'GET', 'GET']);
      expect(requests.map((request) => request.url.path), [
        '/api/auth/permissions',
        '/api/auth/roles',
        '/api/auth/users',
      ]);
      expect(
        requests.every(
          (request) =>
              request.headers['authorization'] == 'Bearer active-access',
        ),
        isTrue,
      );
    });

    test(
      'MOB-ADMIN-003b malformed list data fails as a dependency error',
      () async {
        final client = MockClient((_) async => http.Response('[null]', 200));
        addTearDown(client.close);

        await expectLater(
          AuthApiService(
            client: client,
            storage: _authenticatedStorage(),
          ).adminRoles(),
          throwsA(isA<FormatException>()),
        );
      },
    );
  });

  group('administration mutation contracts', () {
      test('MOB-ADMIN-004 role create, update, permission assignment and delete preserve method, path and payload', () async {
        final storage = _authenticatedStorage();
        final requests = <http.Request>[];
        final client = MockClient((request) async {
          requests.add(request);
          if (request.method == 'DELETE') return http.Response('', 204);
          return http.Response('{}', 200);
        });
        addTearDown(client.close);
        final service = AuthApiService(client: client, storage: storage);

        await service.adminCreateRole(
          name: 'Coastal Guide',
          description: 'Local guide',
        );
        await service.adminUpdateRole(
          id: 'role-1',
          name: 'Senior Coastal Guide',
          description: 'Experienced local guide',
        );
        await service.adminSetRolePermissions(
          id: 'role-1',
          permissionCodes: ['destination.read', 'activity.read'],
        );
        await service.adminDeleteRole('role-1');

        expect(
          requests.map((request) => '${request.method} ${request.url.path}'),
          [
            'POST /api/auth/roles',
            'PUT /api/auth/roles/role-1',
            'POST /api/auth/roles/role-1/permissions',
            'DELETE /api/auth/roles/role-1',
          ],
        );
        expect(jsonDecode(requests[0].body), {
          'name': 'Coastal Guide',
          'description': 'Local guide',
        });
        expect(jsonDecode(requests[1].body), {
          'name': 'Senior Coastal Guide',
          'description': 'Experienced local guide',
        });
        expect(jsonDecode(requests[2].body), {
          'permissionCodes': ['destination.read', 'activity.read'],
        });
        expect(
          requests.every(
            (request) =>
                request.headers['authorization'] == 'Bearer active-access',
          ),
          isTrue,
        );
      });

      test('MOB-ADMIN-005 user update, role assignment and delete use only the public API', () async {
        final storage = _authenticatedStorage();
        final requests = <http.Request>[];
        final client = MockClient((request) async {
          requests.add(request);
          if (request.method == 'DELETE') return http.Response('', 204);
          return http.Response('{}', 200);
        });
        addTearDown(client.close);
        final service = AuthApiService(client: client, storage: storage);

        await service.adminUpdateUser(
          id: 'user-2',
          email: 'new@example.test',
          fullName: 'New Name',
          isActive: false,
        );
        await service.adminSetUserRoles(id: 'user-2', roleNames: ['Guide']);
        await service.adminDeleteUser('user-2');

        expect(
          requests.map((request) => '${request.method} ${request.url.path}'),
          [
            'PUT /api/auth/users/user-2',
            'POST /api/auth/users/user-2/roles',
            'DELETE /api/auth/users/user-2',
          ],
        );
        expect(jsonDecode(requests[0].body), {
          'email': 'new@example.test',
          'fullName': 'New Name',
          'isActive': false,
        });
        expect(jsonDecode(requests[1].body), {
          'roleNames': ['Guide'],
        });
        expect(
          requests.every(
            (request) =>
                request.headers['authorization'] == 'Bearer active-access',
          ),
          isTrue,
        );
      });

      test(
        'MOB-ADMIN-005a optional empty password is omitted from account update',
        () async {
          final client = MockClient((request) async {
            expect(jsonDecode(request.body), {
              'email': 'member@example.test',
              'fullName': 'Coastal Member',
              'isActive': true,
            });
            return http.Response('{}', 200);
          });
          addTearDown(client.close);

          await AuthApiService(
            client: client,
            storage: _authenticatedStorage(),
          ).adminUpdateUser(
            id: 'user-current',
            email: 'member@example.test',
            fullName: 'Coastal Member',
            isActive: true,
            newPassword: '',
          );
        },
      );
    },
  );
}

MemoryAuthCredentialStore _authenticatedStorage() => MemoryAuthCredentialStore()
  ..values.addAll({
    'blueverse.active_account_id': 'user-current',
    'blueverse.access_token.user-current': 'active-access',
    'blueverse.refresh_token.user-current': 'active-refresh',
    'blueverse.device_id': 'test-device',
    'blueverse.device_key': 'test-device-proof',
  });
