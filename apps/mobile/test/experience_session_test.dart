import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/auth_credential_store.dart';
import 'package:mobile/data/services/experience_api_service.dart';

class _Store implements AuthCredentialStore {
  final values = <String, String>{
    'blueverse.active_account_id': 'person',
    'blueverse.access_token.person': _jwt(0),
    'blueverse.refresh_token.person': 'synthetic-refresh',
    'blueverse.device_id': 'synthetic-device',
    'blueverse.device_key': 'synthetic-key',
  };
  @override
  Future<String?> read(String key) async => values[key];
  @override
  Future<void> write(String key, String value) async => values[key] = value;
  @override
  Future<void> delete(String key) async => values.remove(key);
}

String _jwt(int expiry) =>
    'synthetic.${base64Url.encode(utf8.encode(jsonEncode({'sub': 'person', 'exp': expiry})))}.signature';

Map<String, Object?> _session({String userId = 'person'}) => {
  'token': _jwt(4102444800),
  'expiresAt': '2100-01-01T00:00:00Z',
  'refreshToken': 'rotated-synthetic-refresh',
  'deviceId': 'synthetic-device',
  'deviceKey': 'synthetic-key',
  'sessionExpiresAt': '2100-01-02T00:00:00Z',
  'rememberMe': false,
  'user': {
    'id': userId,
    'email': 'person@example.test',
    'fullName': 'Synthetic Person',
    'isActive': true,
    'createdAt': '2026-01-01T00:00:00Z',
    'roles': ['User'],
    'permissions': ['experiences.catalogue.manage'],
  },
};

void main() {
  // experience-catalogue-management + auth-session-management workflows.
  test(
    'MOB-EXP-034 concurrent experience reads renew once before dispatch',
    () async {
      final storage = _Store();
      final release = Completer<void>();
      final started = Completer<void>();
      var refreshes = 0;
      final requests = <http.Request>[];
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/refresh') {
          refreshes++;
          expect(request.method, 'POST');
          expect(jsonDecode(request.body), {
            'refreshToken': 'synthetic-refresh',
            'deviceId': 'synthetic-device',
            'deviceKey': 'synthetic-key',
            'useCookies': false,
          });
          started.complete();
          await release.future;
          return http.Response(jsonEncode(_session()), 200);
        }
        requests.add(request);
        return http.Response(
          '{"total":0,"page":1,"pageSize":100,"items":[]}',
          200,
        );
      });
      addTearDown(client.close);
      final auth = AuthApiService(client: client, storage: storage);
      final api = ExperienceApiService(
        client: client,
        accessTokenProvider: auth.accessTokenForRequest,
      );
      final loads = Future.wait([
        api.getDestinations(status: 'DRAFT'),
        api.getDestinations(status: 'ARCHIVED'),
        api.getOfferings(status: 'DRAFT'),
      ]);
      await started.future;
      expect(requests, isEmpty);
      release.complete();
      final results = await loads;
      expect(refreshes, 1);
      expect(requests, hasLength(3));
      expect(
        requests.every(
          (r) => r.headers['authorization'] == 'Bearer ${_jwt(4102444800)}',
        ),
        isTrue,
      );
      expect(results.every((r) => r.total == 0 && r.items.isEmpty), isTrue);
      expect(
        storage.values['blueverse.refresh_token.person'],
        'rotated-synthetic-refresh',
      );
    },
  );

  test(
    'MOB-EXP-035 a real permission denial is not refreshed or replayed',
    () async {
      final storage = _Store()
        ..values['blueverse.access_token.person'] = _jwt(4102444800);
      var calls = 0;
      final client = MockClient((request) async {
        calls++;
        expect(request.url.path, '/api/experiences/destinations');
        expect(request.headers['authorization'], 'Bearer ${_jwt(4102444800)}');
        return http.Response('{"message":"Not allowed"}', 403);
      });
      addTearDown(client.close);
      final auth = AuthApiService(client: client, storage: storage);
      final api = ExperienceApiService(
        client: client,
        accessTokenProvider: auth.accessTokenForRequest,
      );
      await expectLater(
        api.getDestinations(status: 'DRAFT'),
        throwsA(
          isA<ExperienceApiException>().having(
            (e) => e.statusCode,
            'status',
            403,
          ),
        ),
      );
      expect(calls, 1);
      expect(
        storage.values['blueverse.refresh_token.person'],
        'synthetic-refresh',
      );
    },
  );

  test('MOB-EXP-036 revoked renewal prevents business requests and keeps credentials', () async {
    final storage = _Store();
    final before = Map<String, String>.from(storage.values);
    var calls = 0;
    final client = MockClient((request) async {
      calls++;
      expect(request.url.path, '/api/auth/refresh');
      return http.Response('{"message":"Session revoked"}', 401);
    });
    addTearDown(client.close);
    final auth = AuthApiService(client: client, storage: storage);
    final api = ExperienceApiService(
      client: client,
      accessTokenProvider: auth.accessTokenForRequest,
    );
    await expectLater(
      api.getDestinations(status: 'DRAFT'),
      throwsA(
        isA<AuthApiException>().having((e) => e.statusCode, 'status', 401),
      ),
    );
    expect(calls, 1);
    expect(storage.values, before);
  });

  test(
    'MOB-EXP-037 account switch during renewal cannot restore the old account',
    () async {
      final storage = _Store();
      final client = MockClient((request) async {
        expect(request.url.path, '/api/auth/refresh');
        storage.values['blueverse.active_account_id'] = 'other';
        storage.values['blueverse.access_token.other'] = 'other-account-token';
        return http.Response(jsonEncode(_session()), 200);
      });
      addTearDown(client.close);
      final auth = AuthApiService(client: client, storage: storage);
      await expectLater(
        auth.accessTokenForRequest(),
        throwsA(isA<AuthApiException>()),
      );
      expect(storage.values['blueverse.active_account_id'], 'other');
      expect(
        storage.values['blueverse.access_token.other'],
        'other-account-token',
      );
      expect(
        storage.values['blueverse.refresh_token.person'],
        'synthetic-refresh',
      );
      expect(storage.values['blueverse.access_token.person'], _jwt(0));
    },
  );

  test(
    'MOB-EXP-038 mismatched refresh identity is rejected before persistence',
    () async {
      final storage = _Store();
      final before = Map<String, String>.from(storage.values);
      final client = MockClient(
        (_) async => http.Response(jsonEncode(_session(userId: 'other')), 200),
      );
      addTearDown(client.close);
      await expectLater(
        AuthApiService(
          client: client,
          storage: storage,
        ).accessTokenForRequest(),
        throwsA(isA<AuthApiException>()),
      );
      expect(storage.values, before);
    },
  );

  test(
    'MOB-EXP-039 malformed JWT stays subject to server authorization',
    () async {
      final storage = _Store()
        ..values['blueverse.access_token.person'] = 'not.a-jwt.signature';
      final client = MockClient(
        (_) async => throw StateError('Must not renew a malformed token'),
      );
      addTearDown(client.close);
      expect(
        await AuthApiService(
          client: client,
          storage: storage,
        ).accessTokenForRequest(),
        'not.a-jwt.signature',
      );
    },
  );

  test('MOB-EXP-040 another account legacy token is never inherited', () async {
    final storage = _Store()
      ..values.remove('blueverse.access_token.person')
      ..values['blueverse.access_token'] = 'unrelated-account-token';
    final client = MockClient(
      (_) async =>
          throw StateError('Must not refresh without the scoped access token'),
    );
    addTearDown(client.close);
    expect(
      await AuthApiService(
        client: client,
        storage: storage,
      ).accessTokenForRequest(),
      isNull,
    );
    expect(storage.values['blueverse.active_account_id'], 'person');
  });
}
