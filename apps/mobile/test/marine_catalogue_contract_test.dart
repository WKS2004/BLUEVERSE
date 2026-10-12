import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/repositories/marine_repository.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/marine_api_client.dart';
import 'package:mobile/data/services/marine_service.dart';
import 'package:mobile/ui/marine/suitability_screen.dart';

const canonicalId = '12345678-1234-1234-1234-123456789abc';
Map<String, dynamic> published() => {
  'id': canonicalId,
  'name': 'Canonical coast activity',
  'activityType': 'Surfing',
  'isActive': true,
};
MarineService service(http.Client client) => MarineService(
  repository: MarineRepository(
    client: MarineApiClient(client: client, gateway: const ApiGatewayConfig()),
  ),
);

void main() {
  test(
    'MOB-MARINE-CATALOG-001 reads canonical IDs only through the public API',
    () async {
      late http.Request received;
      final rows = await service(
        MockClient((request) async {
          received = request;
          return http.Response(jsonEncode([published()]), 200);
        }),
      ).activities();
      expect(received.method, 'GET');
      expect(received.url.path, '/api/marine/activities');
      expect(rows.single['id'], canonicalId);
      expect(rows.single['name'], 'Canonical coast activity');
    },
  );
  test(
    'MOB-MARINE-CATALOG-002 malformed or duplicate activity data fails safely',
    () async {
      for (final payload in [
        '{}',
        'null',
        '[null]',
        jsonEncode([published(), published()]),
        jsonEncode([
          {'id': 'invalid'},
        ]),
      ]) {
        final api = service(
          MockClient((_) async => http.Response(payload, 200)),
        );
        await expectLater(
          api.activities(),
          throwsA(
            isA<MarineApiException>().having(
              (e) => e.statusCode,
              'status',
              502,
            ),
          ),
        );
      }
    },
  );
  testWidgets(
    'MOB-MARINE-CATALOG-003 unavailable catalogue disables assessments and retry recovers',
    (tester) async {
      var available = false;
      final calls = <String>[];
      final api = service(
        MockClient((request) async {
          calls.add(request.url.path);
          return available
              ? http.Response(jsonEncode([published()]), 200)
              : http.Response(
                  jsonEncode({'detail': 'Activities temporarily unavailable.'}),
                  503,
                );
        }),
      );
      await tester.pumpWidget(
        MaterialApp(home: SuitabilityScreen(service: api)),
      );
      await tester.pumpAndSettle();
      expect(find.text('Activities temporarily unavailable.'), findsOneWidget);
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(
                FilledButton,
                'Check conditions & suitability',
              ),
            )
            .onPressed,
        isNull,
      );
      expect(calls, ['/api/marine/activities']);
      available = true;
      await tester.tap(find.text('Retry activities'));
      await tester.pumpAndSettle();
      expect(find.text('Canonical coast activity'), findsOneWidget);
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(
                FilledButton,
                'Check conditions & suitability',
              ),
            )
            .onPressed,
        isNotNull,
      );
    },
  );
  testWidgets(
    'MOB-MARINE-CATALOG-004 empty catalogue has no placeholder activity',
    (tester) async {
      final api = service(MockClient((_) async => http.Response('[]', 200)));
      await tester.pumpWidget(
        MaterialApp(home: SuitabilityScreen(service: api)),
      );
      await tester.pumpAndSettle();
      expect(
        find.text('No published activities are available.'),
        findsOneWidget,
      );
      expect(find.text('Surfing'), findsNothing);
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(
                FilledButton,
                'Check conditions & suitability',
              ),
            )
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<OutlinedButton>(
              find.widgetWithText(OutlinedButton, 'View conditions only'),
            )
            .onPressed,
        isNotNull,
      );
    },
  );

  testWidgets(
    'MOB-MARINE-CATALOG-005 condition-only reads work without a catalogue',
    (tester) async {
      final calls = <String>[];
      final api = service(
        MockClient((request) async {
          calls.add(request.url.path);
          if (request.url.path == '/api/marine/activities') {
            return http.Response(
              jsonEncode({'detail': 'Activities unavailable.'}),
              503,
            );
          }
          return http.Response(
            jsonEncode({
              'id': 'fixture-snapshot',
              'latitude': 6.025,
              'longitude': 80.216,
              'forecastTime': '2026-10-08T08:00:00Z',
              'retrievedAt': '2026-10-08T08:05:00Z',
              'windSpeed': 15,
              'waveHeight': 0.8,
              'swellHeight': 0.7,
              'rain': 0,
              'weatherCode': 0,
              'source': 'Open-Meteo',
              'freshnessStatus': 'FRESH',
              'missingFields': [],
            }),
            200,
          );
        }),
      );
      await tester.pumpWidget(
        MaterialApp(home: SuitabilityScreen(service: api)),
      );
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('View conditions only'));
      await tester.tap(find.text('View conditions only'));
      await tester.pumpAndSettle();
      expect(calls, ['/api/marine/activities', '/api/marine/current']);
      expect(find.text('Open-Meteo'), findsOneWidget);
      expect(calls.contains('/api/marine/evaluate'), isFalse);
    },
  );
}
