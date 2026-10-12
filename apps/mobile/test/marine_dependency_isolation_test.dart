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
Map<String, Object?> unavailable() => {
  'status': 503,
  'detail': 'Activity data is unavailable.',
  'availability': 'UNAVAILABLE',
  'dependency': 'experience-biodiversity',
  'code': 'ACTIVITY_DATA_UNAVAILABLE',
  'reason': 'UNREACHABLE',
  'missingFields': ['activityCatalogue'],
  'retryable': true,
  'data': null,
};
Map<String, Object?> profile(bool active) => {
  'id': 'profile',
  'activityId': activityId,
  'activityName': 'Saved surfing profile',
  'maxWindSpeed': 20,
  'maxWaveHeight': 1,
  'maxSwellHeight': 1,
  'windCriteriaSource': 'Cited manual',
  'windCriteriaRationale': 'Reviewed safety evidence',
  'waveCriteriaSource': 'Cited manual',
  'waveCriteriaRationale': 'Reviewed safety evidence',
  'swellCriteriaSource': 'Cited manual',
  'swellCriteriaRationale': 'Reviewed safety evidence',
  'isActive': active,
  'version': 1,
  'createdByUserId': 'author',
  'reviewedByUserId': active ? 'reviewer' : null,
  'reviewedAt': active ? '2026-10-08T08:00:00Z' : null,
  'createdAt': '2026-10-08T07:00:00Z',
  'updatedAt': '2026-10-08T08:00:00Z',
};

void main() {
  test('MOB-MARINE-ISOLATION-001 parses unavailable fields and keeps an empty catalogue distinct', () async {
    var payload = unavailable();
    var status = 503;
    final api = MarineApiClient(
      client: MockClient(
        (_) async => http.Response(jsonEncode(payload), status),
      ),
      gateway: const ApiGatewayConfig(),
    );
    await expectLater(
      api.getActivities(),
      throwsA(
        isA<MarineApiException>()
            .having((e) => e.statusCode, 'status', 503)
            .having(
              (e) => e.dependencyAvailability?.availability,
              'availability',
              'UNAVAILABLE',
            )
            .having(
              (e) => e.dependencyAvailability?.dependency,
              'dependency',
              'experience-biodiversity',
            )
            .having(
              (e) => e.dependencyAvailability?.missingFields,
              'missing fields',
              ['activityCatalogue'],
            ),
      ),
    );
    payload = {
      ...unavailable(),
      'missingFields': [123],
    };
    await expectLater(
      api.getActivities(),
      throwsA(
        isA<MarineApiException>()
            .having((e) => e.statusCode, 'status', 503)
            .having(
              (e) => e.dependencyAvailability,
              'invalid optional metadata',
              isNull,
            ),
      ),
    );
    status = 200;
    final empty = MarineApiClient(
      client: MockClient((_) async => http.Response('[]', 200)),
      gateway: const ApiGatewayConfig(),
    );
    expect(await empty.getActivities(), isEmpty);
  });

  for (final active in [true, false]) {
    testWidgets(
      'MOB-MARINE-ISOLATION-002 saved ${active ? 'approved' : 'draft'} profile remains visible during an outage and retry restores actions',
      (tester) async {
        var available = false;
        final calls = <String>[];
        final client = MarineApiClient(
          client: MockClient((request) async {
            calls.add('${request.method} ${request.url.path}');
            if (request.url.path.endsWith('/activities')) {
              return available
                  ? http.Response(
                      jsonEncode([
                        {
                          'id': activityId,
                          'name': 'Surfing',
                          'activityType': 'Surfing',
                          'isActive': true,
                        },
                      ]),
                      200,
                    )
                  : http.Response(jsonEncode(unavailable()), 503);
            }
            return http.Response(jsonEncode([profile(active)]), 200);
          }),
          gateway: const ApiGatewayConfig(),
        );
        await tester.pumpWidget(
          MaterialApp(
            home: SafetyProfilesScreen(
              canManage: true,
              currentUserId: 'reviewer',
              service: MarineService(
                repository: MarineRepository(client: client),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('Activity data unavailable'), findsOneWidget);
        expect(
          find.text('Condition lookup and saved records remain accessible.'),
          findsOneWidget,
        );
        expect(find.textContaining('Saved surfing profile'), findsWidgets);
        expect(
          tester
              .widget<OutlinedButton>(
                find.widgetWithText(OutlinedButton, 'Create a new version'),
              )
              .onPressed,
          isNull,
        );
        if (active) {
          expect(
            tester
                .widget<FilledButton>(
                  find.widgetWithText(FilledButton, 'Deactivate profile'),
                )
                .onPressed,
            isNotNull,
          );
        } else {
          expect(
            tester
                .widget<FilledButton>(
                  find.widgetWithText(FilledButton, 'Review and activate'),
                )
                .onPressed,
            isNull,
          );
        }
        expect(calls.every((call) => call.startsWith('GET ')), isTrue);
        available = true;
        await tester.ensureVisible(find.text('Retry activities'));
        await tester.tap(find.text('Retry activities'));
        await tester.pumpAndSettle();
        expect(find.text('Activity data unavailable'), findsNothing);
        expect(
          tester
              .widget<OutlinedButton>(
                find.widgetWithText(OutlinedButton, 'Create a new version'),
              )
              .onPressed,
          isNotNull,
        );
        if (!active) {
          expect(
            tester
                .widget<FilledButton>(
                  find.widgetWithText(FilledButton, 'Review and activate'),
                )
                .onPressed,
            isNotNull,
          );
        }
      },
    );
  }
}
