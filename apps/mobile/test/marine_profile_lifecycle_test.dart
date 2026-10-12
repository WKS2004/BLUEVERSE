import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/repositories/marine_repository.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/marine_api_client.dart';
import 'package:mobile/data/services/marine_service.dart';
import 'package:mobile/ui/marine/safety_profiles_screen.dart';

const activityId = '12345678-1234-1234-1234-123456789abc';
Map<String, Object?> profile(String name) => {
  'id': 'profile-1',
  'activityId': activityId,
  'activityName': name,
  'maxWindSpeed': 20,
  'maxWaveHeight': 1,
  'maxSwellHeight': 1,
  'windCriteriaSource': 'Cited manual',
  'windCriteriaRationale': 'Reviewed safety evidence',
  'waveCriteriaSource': 'Cited manual',
  'waveCriteriaRationale': 'Reviewed safety evidence',
  'swellCriteriaSource': 'Cited manual',
  'swellCriteriaRationale': 'Reviewed safety evidence',
  'isActive': true,
  'version': 1,
  'createdByUserId': 'author',
  'reviewedByUserId': 'reviewer',
  'reviewedAt': '2026-10-10T08:00:00Z',
  'createdAt': '2026-10-10T07:00:00Z',
  'updatedAt': '2026-10-10T08:00:00Z',
};

http.Response json(Object body) => http.Response(jsonEncode(body), 200);
MarineService service(Future<http.Response> Function(http.Request) handler) =>
    MarineService(
      repository: MarineRepository(
        client: MarineApiClient(
          client: MockClient(handler),
          gateway: const ApiGatewayConfig(),
        ),
      ),
    );

void main() {
  testWidgets(
    'M2-MOB-WORKFLOW-001 cited draft, new version, independent review and deactivation use the shared public contract',
    (tester) async {
      Map<String, Object?>? saved;
      final writes = <String>[];
      final actor = ValueNotifier('author');
      addTearDown(actor.dispose);
      final api = service((request) async {
        final path = request.url.path;
        if (path.endsWith('/activities')) {
          return json([
            {
              'id': activityId,
              'name': 'Coastal kayaking',
              'activityType': 'kayaking',
              'isActive': true,
            },
          ]);
        }
        if (request.method == 'GET') return json(saved == null ? [] : [saved]);
        writes.add('${request.method} $path');
        if (request.method == 'DELETE') {
          saved = {...saved!, 'isActive': false};
          return http.Response('', 204);
        }
        if (path.endsWith('/review')) {
          saved = {
            ...saved!,
            'isActive': true,
            'reviewedByUserId': 'reviewer',
            'reviewedAt': '2026-10-10T09:00:00Z',
          };
          return json(saved!);
        }
        final body = jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['maxWindSpeed'], request.method == 'POST' ? 20 : 21);
        expect(body['maxWaveHeight'], 1.25);
        expect(body['maxSwellHeight'], 0.5);
        for (final factor in ['wind', 'wave', 'swell']) {
          expect(body['${factor}CriteriaSource'], 'Cited coastal guide');
          expect(
            body['${factor}CriteriaRationale'],
            'Reviewed local safety evidence',
          );
        }
        saved = {
          ...profile('Coastal kayaking'),
          ...body,
          'id': request.method == 'POST' ? 'profile-1' : 'profile-2',
          'version': request.method == 'POST' ? 1 : 2,
          'createdByUserId': 'author',
          'isActive': false,
          'reviewedByUserId': null,
          'reviewedAt': null,
        };
        return json(saved!);
      });
      await tester.pumpWidget(
        MaterialApp(
          home: ValueListenableBuilder<String>(
            valueListenable: actor,
            builder: (_, userId, _) => SafetyProfilesScreen(
              service: api,
              canManage: true,
              currentUserId: userId,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      Future<void> tap(String text) async {
        final target = find.text(text);
        await tester.ensureVisible(target);
        await tester.pumpAndSettle();
        await tester.tap(target);
        await tester.pumpAndSettle();
        await tester.pump(const Duration(milliseconds: 200));
      }

      Future<void> fill(String label, String value) async {
        final field = find.widgetWithText(TextFormField, label);
        await tester.ensureVisible(field);
        await tester.enterText(field, value);
      }

      await tap('Create draft');
      await tap('Save draft');
      expect(writes, isEmpty);
      expect(find.textContaining('Enter a value from'), findsWidgets);
      await fill('Maximum wind (km/h)', '20');
      await fill('Maximum wave (m)', '1.25');
      await fill('Maximum swell (m)', '0.50');
      for (final factor in ['Wind', 'Wave', 'Swell']) {
        await fill('$factor source reference', 'Cited coastal guide');
        await fill('$factor rationale', 'Reviewed local safety evidence');
      }
      await tap('Save draft');
      expect(saved!['version'], 1);
      expect(saved!['isActive'], false);
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'Review and activate'),
            )
            .onPressed,
        isNull,
      );
      await tap('Create a new version');
      await fill('Maximum wind (km/h)', '21');
      await tap('Save draft');
      expect(saved!['version'], 2);
      expect(saved!['reviewedAt'], isNull);
      actor.value = 'reviewer';
      await tester.pump();
      await tap('Review and activate');
      expect(saved!['isActive'], true);
      expect(saved!['reviewedByUserId'], 'reviewer');
      await tap('Deactivate profile');
      await tap('Cancel');
      expect(writes.where((call) => call.startsWith('DELETE')), isEmpty);
      await tap('Deactivate profile');
      await tap('Deactivate');
      expect(saved!['isActive'], false);
      expect(writes, [
        'POST /api/marine/safety-profiles',
        'PUT /api/marine/safety-profiles/profile-1',
        'POST /api/marine/safety-profiles/profile-2/review',
        'DELETE /api/marine/safety-profiles/profile-2',
      ]);
      expect(tester.takeException(), isNull);
    },
  );
  // ui-integration: marine-safety-profile-management. A confirmation must not
  // dispatch a mutation after the protected workspace has been removed.
  testWidgets(
    'M2-MOB-LIFETIME-001 removed profile page cancels pending confirmation',
    (tester) async {
      var deletes = 0;
      final visible = ValueNotifier(true);
      addTearDown(visible.dispose);
      final api = service((request) async {
        if (request.method == 'DELETE') {
          deletes++;
          return http.Response('', 204);
        }
        return request.url.path.endsWith('/safety-profiles')
            ? json([profile('Reviewed coastal profile')])
            : json([]);
      });
      await tester.pumpWidget(
        MaterialApp(
          home: ValueListenableBuilder<bool>(
            valueListenable: visible,
            builder: (_, shown, _) => shown
                ? SafetyProfilesScreen(
                    service: api,
                    canManage: true,
                    currentUserId: 'reviewer',
                  )
                : const Scaffold(body: Text('Workspace closed')),
          ),
        ),
      );
      await tester.pumpAndSettle();
      final button = find.text('Deactivate profile');
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();
      expect(find.text('Deactivate profile?'), findsWidgets);
      visible.value = false;
      await tester.pump();
      await tester.tap(find.text('Deactivate'));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.text('Workspace closed'), findsOneWidget);
      expect(deletes, 0);
    },
  );

  // The latest requested read must remain authoritative when network replies
  // arrive in the opposite order (including an initial bootstrap reply).
  testWidgets(
    'M2-MOB-LIFETIME-002 older profile response cannot overwrite refreshed data',
    (tester) async {
      final replies = <Completer<http.Response>>[];
      final api = service((request) async {
        if (!request.url.path.endsWith('/safety-profiles')) return json([]);
        final reply = Completer<http.Response>();
        replies.add(reply);
        return reply.future;
      });
      await tester.pumpWidget(
        MaterialApp(home: SafetyProfilesScreen(service: api, canManage: false)),
      );
      await tester.pump();
      expect(replies, hasLength(1));
      final refresh = tester
          .widget<RefreshIndicator>(find.byType(RefreshIndicator))
          .onRefresh();
      await tester.pump();
      expect(replies, hasLength(2));
      replies[1].complete(json([profile('Latest refreshed profile')]));
      await refresh;
      await tester.pump();
      expect(find.text('Latest refreshed profile'), findsWidgets);
      replies[0].complete(json([profile('Older bootstrap profile')]));
      await tester.pumpAndSettle();
      // Explicitly advance the shared loading feedback's settling timer.
      await tester.pump(const Duration(milliseconds: 200));
      expect(find.text('Latest refreshed profile'), findsWidgets);
      expect(find.text('Older bootstrap profile'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
}
