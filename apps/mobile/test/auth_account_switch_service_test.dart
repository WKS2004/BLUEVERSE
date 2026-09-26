import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/auth_credential_store.dart';

class _MemoryCredentialStore implements AuthCredentialStore {
  final values = <String, String>{};

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async => values[key] = value;

  @override
  Future<void> delete(String key) async => values.remove(key);
}

Map<String, dynamic> _authResponse({
  required String id,
  required String email,
  required String token,
  required String refreshToken,
}) => {
  'token': token,
  'expiresAt': '2026-09-24T13:15:00Z',
  'deviceId': 'same-device',
  'deviceKey': 'same-device-proof',
  'refreshToken': refreshToken,
  'sessionExpiresAt': '2026-10-24T13:00:00Z',
  'rememberMe': true,
  'user': _user(id, email),
};

Map<String, dynamic> _user(String id, String email) => {
  'id': id,
  'email': email,
  'fullName': 'User $id',
  'isActive': true,
  'createdAt': '2026-09-01T00:00:00Z',
  'roles': ['User'],
  'permissions': <String>[],
};

void main() {
  group('saved account switching', () {
    test('MOB-MULTI-ACCOUNT-001 keeps both credentials and switches the active account', () async {
      final storage = _MemoryCredentialStore();
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/login') {
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          final isFirst = body['email'] == 'first@example.test';
          return http.Response(
            jsonEncode(
              _authResponse(
                id: isFirst ? 'user-first' : 'user-second',
                email: body['email'] as String,
                token: isFirst ? 'token-first' : 'token-second',
                refreshToken: isFirst ? 'refresh-first' : 'refresh-second',
              ),
            ),
            200,
          );
        }
        if (request.url.path == '/api/auth/me') {
          final token = request.headers['authorization'];
          final isFirst = token == 'Bearer token-first';
          return http.Response(
            jsonEncode(
              _user(
                isFirst ? 'user-first' : 'user-second',
                isFirst ? 'first@example.test' : 'second@example.test',
              ),
            ),
            200,
          );
        }
        return http.Response('{}', 404);
      });
      addTearDown(client.close);
      final service = AuthApiService(client: client, storage: storage);

      await service.login(
        email: 'first@example.test',
        password: 'pass-one',
        rememberMe: true,
      );
      await service.login(
        email: 'second@example.test',
        password: 'pass-two',
        rememberMe: true,
      );

      expect(
        storage.values['blueverse.access_token.user-first'],
        'token-first',
      );
      expect(
        storage.values['blueverse.refresh_token.user-first'],
        'refresh-first',
      );
      expect(
        storage.values['blueverse.access_token.user-second'],
        'token-second',
      );
      expect(
        storage.values['blueverse.refresh_token.user-second'],
        'refresh-second',
      );
      expect(storage.values['blueverse.active_account_id'], 'user-second');

      final switched = await service.switchAccount('user-first');

      expect(switched.id, 'user-first');
      expect(storage.values['blueverse.active_account_id'], 'user-first');
      expect(
        storage.values['blueverse.access_token.user-first'],
        'token-first',
      );
      expect(
        storage.values['blueverse.access_token.user-second'],
        'token-second',
      );
      expect(
        (await service.accounts()).map((account) => account.id),
        containsAll(['user-first', 'user-second']),
      );
    });

    test('MOB-MULTI-ACCOUNT-002 refreshes an expired selected account without losing another', () async {
      final storage = _MemoryCredentialStore();
      late http.Request refreshRequest;
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/login') {
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          final isFirst = body['email'] == 'first@example.test';
          return http.Response(
            jsonEncode(
              _authResponse(
                id: isFirst ? 'user-first' : 'user-second',
                email: body['email'] as String,
                token: isFirst ? 'expired-first' : 'token-second',
                refreshToken: isFirst ? 'refresh-first' : 'refresh-second',
              ),
            ),
            200,
          );
        }
        if (request.url.path == '/api/auth/me') {
          if (request.headers['authorization'] == 'Bearer expired-first') {
            return http.Response('{"status":401,"detail":"Expired."}', 401);
          }
          return http.Response(
            jsonEncode(_user('user-first', 'first@example.test')),
            200,
          );
        }
        if (request.url.path == '/api/auth/refresh') {
          refreshRequest = request;
          return http.Response(
            jsonEncode(
              _authResponse(
                id: 'user-first',
                email: 'first@example.test',
                token: 'fresh-first',
                refreshToken: 'fresh-refresh-first',
              ),
            ),
            200,
          );
        }
        return http.Response('{}', 404);
      });
      addTearDown(client.close);
      final service = AuthApiService(client: client, storage: storage);
      await service.login(
        email: 'first@example.test',
        password: 'pass-one',
        rememberMe: true,
      );
      await service.login(
        email: 'second@example.test',
        password: 'pass-two',
        rememberMe: true,
      );

      final switched = await service.switchAccount('user-first');

      expect(switched.id, 'user-first');
      expect(jsonDecode(refreshRequest.body)['refreshToken'], 'refresh-first');
      expect(
        storage.values['blueverse.access_token.user-first'],
        'fresh-first',
      );
      expect(
        storage.values['blueverse.access_token.user-second'],
        'token-second',
      );
      expect(storage.values['blueverse.active_account_id'], 'user-first');
    });

    test('MOB-MULTI-ACCOUNT-003 removing one saved account preserves the active account', () async {
      final storage = _MemoryCredentialStore();
      late http.Request removeRequest;
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/login') {
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          final isFirst = body['email'] == 'first@example.test';
          return http.Response(
            jsonEncode(
              _authResponse(
                id: isFirst ? 'user-first' : 'user-second',
                email: body['email'] as String,
                token: isFirst ? 'token-first' : 'token-second',
                refreshToken: isFirst ? 'refresh-first' : 'refresh-second',
              ),
            ),
            200,
          );
        }
        if (request.url.path == '/api/auth/logout-account') {
          removeRequest = request;
          return http.Response('', 204);
        }
        return http.Response('{}', 404);
      });
      addTearDown(client.close);
      final service = AuthApiService(client: client, storage: storage);
      await service.login(
        email: 'first@example.test',
        password: 'pass-one',
        rememberMe: true,
      );
      await service.login(
        email: 'second@example.test',
        password: 'pass-two',
        rememberMe: true,
      );

      await service.removeAccountFromDevice('user-first');

      expect(removeRequest.headers['authorization'], 'Bearer token-second');
      expect(jsonDecode(removeRequest.body), {'userId': 'user-first'});
      expect(storage.values['blueverse.active_account_id'], 'user-second');
      expect(
        storage.values.containsKey('blueverse.access_token.user-first'),
        isFalse,
      );
      expect(
        storage.values.containsKey('blueverse.access_token.user-second'),
        isTrue,
      );
      expect((await service.accounts()).map((account) => account.id), [
        'user-second',
      ]);
    });
  });
}
