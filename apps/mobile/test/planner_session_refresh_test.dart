import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/repositories/auth_repository.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/planner_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';

import 'support/mobile_test_support.dart';

String accessToken({required int expiresAt}) {
  final payload = base64Url
      .encode(
        utf8.encode(jsonEncode({'sub': 'user-current', 'exp': expiresAt})),
      )
      .replaceAll('=', '');
  return 'header.$payload.signature';
}

void main() {
  test(
    'MOB-PLANNER-027 refreshes an expired session before one itinerary write',
    () async {
      final storage = MemoryAuthCredentialStore()
        ..values['blueverse.active_account_id'] = 'user-current'
        ..values['blueverse.device_id'] = 'synthetic-device'
        ..values['blueverse.device_key'] = 'synthetic-device-proof'
        ..values['blueverse.refresh_token.user-current'] = 'old-refresh-token'
        ..values['blueverse.access_token.user-current'] = accessToken(
          expiresAt:
              DateTime.now()
                  .toUtc()
                  .subtract(const Duration(minutes: 1))
                  .millisecondsSinceEpoch ~/
              1000,
        );
      final events = <String>[];
      var requests = 0;
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/refresh') {
          events.add('refresh');
          expect(request.method, 'POST');
          expect(jsonDecode(request.body)['refreshToken'], 'old-refresh-token');
          return http.Response(
            jsonEncode(
              mobileTestAuthResponse(
                token: 'fresh-planner-token',
                refreshToken: 'rotated-refresh-token',
              ),
            ),
            200,
          );
        }
        requests++;
        events.add('request');
        expect(request.method, 'DELETE');
        expect(request.url.path, '/api/planner/itineraries/trip-1');
        expect(request.headers['Authorization'], 'Bearer fresh-planner-token');
        return http.Response('', 204);
      });
      addTearDown(client.close);
      final auth = AuthApiService(client: client, storage: storage);
      final viewModel = AuthViewModel(
        repository: AuthRepository(apiService: auth),
      )..user = mobileTestUser();
      addTearDown(viewModel.dispose);
      final api = PlannerApiService(
        storage: storage,
        onSessionRefreshRequired: viewModel.refreshActiveSession,
        client: client,
      );

      await api.deleteItinerary('trip-1');

      expect(events, ['refresh', 'request']);
      expect(requests, 1);
      expect(
        storage.values['blueverse.refresh_token.user-current'],
        'rotated-refresh-token',
      );
    },
  );

  test(
    'MOB-PLANNER-028 failed session refresh dispatches no itinerary write',
    () async {
      final storage = MemoryAuthCredentialStore()
        ..values['blueverse.active_account_id'] = 'user-current'
        ..values['blueverse.access_token.user-current'] = accessToken(
          expiresAt:
              DateTime.now()
                  .toUtc()
                  .subtract(const Duration(minutes: 1))
                  .millisecondsSinceEpoch ~/
              1000,
        );
      var requests = 0;
      final api = PlannerApiService(
        storage: storage,
        onSessionRefreshRequired: (_) async {
          throw StateError('Session refresh unavailable.');
        },
        client: MockClient((_) async {
          requests++;
          return http.Response('', 204);
        }),
      );

      await expectLater(
        api.deleteItinerary('trip-1'),
        throwsA(isA<StateError>()),
      );

      expect(requests, 0);
    },
  );

  test(
    'MOB-PLANNER-029 a current access token skips session refresh',
    () async {
      final storage = MemoryAuthCredentialStore()
        ..values['blueverse.active_account_id'] = 'user-current'
        ..values['blueverse.access_token.user-current'] = accessToken(
          expiresAt:
              DateTime.now()
                  .toUtc()
                  .add(const Duration(minutes: 5))
                  .millisecondsSinceEpoch ~/
              1000,
        );
      var refreshes = 0;
      final api = PlannerApiService(
        storage: storage,
        onSessionRefreshRequired: (_) async {
          refreshes++;
        },
        client: MockClient((request) async {
          expect(
            request.headers['Authorization'],
            startsWith('Bearer header.'),
          );
          return http.Response('[]', 200);
        }),
      );

      expect(await api.itineraries(), isEmpty);
      expect(refreshes, 0);
    },
  );
}
