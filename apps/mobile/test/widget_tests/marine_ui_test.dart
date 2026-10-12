import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/marine_api_client.dart';
import 'package:mobile/data/services/marine_service.dart';
import 'package:mobile/data/repositories/marine_repository.dart';
import 'package:mobile/ui/marine/marine_route_table.dart';
import 'package:mobile/ui/marine/suitability_screen.dart';

void main() {
  group('MarineRouteTable', () {
    test('M2-MOB-ROUTE-001 routes match only their registered destination', () {
      const full = '/marine/conditions';
      final path = MarinePath(full: full);

      expect(path.match(full).route, full);
      expect(path.match('/marine/history').route, isNull);
      expect(path.match('/marine/safety-profiles').route, isNull);
      expect(
        path
            .match(
              '/marine/safety-profiles/12345678-1234-1234-1234-123456789012',
            )
            .route,
        isNull,
      );
    });

    test('M2-MOB-ROUTE-002 unmatched routes return an empty payload', () {
      const full = '/unknown/marine/path';
      final path = MarinePath(full: full);

      expect(path.match(full).route, isNull);
      expect(path.match(full).params, isEmpty);
    });
  });

  group('MarinePath', () {
    test('M2-MOB-ROUTE-003 matches the registered marine destinations', () {
      expect(
        MarinePath(full: '/marine/conditions')
            .match('/marine/conditions')
            .route,
        '/marine/conditions',
      );
      expect(
        MarinePath(full: '/marine/history').match('/marine/history').route,
        '/marine/history',
      );
      expect(
        MarinePath(full: '/marine/safety-profiles')
            .match('/marine/safety-profiles')
            .route,
        '/marine/safety-profiles',
      );
      expect(
        MarinePath(
              full: '/marine/safety-profiles/12345678-1234-1234-1234-123456789012',
            )
            .match(
              '/marine/safety-profiles/12345678-1234-1234-1234-123456789012',
            )
            .route,
        isNull,
      );
    });

    test('M2-MOB-ROUTE-004 unmatched routes return an empty payload', () {
      const full = '/unknown/marine/path';
      final path = MarinePath(full: full);

      expect(path.match(full).route, isNull);
      expect(path.match(full).params, isEmpty);
    });
  });

  group('SuitabilityScreen', () {
    testWidgets(
      'M2-MOB-FORM-001 renders the query card and activity selector',
      (tester) async {
        await tester.pumpWidget(
          MaterialApp(home: SuitabilityScreen(service: _dummyService())),
        );

        expect(find.text('CONDITION QUERY'), findsOneWidget);
        expect(find.text('Latitude (−90 to 90)'), findsOneWidget);
        expect(find.text('Longitude (−180 to 180)'), findsOneWidget);
        expect(find.text('Requested time (UTC, optional)'), findsOneWidget);
        expect(find.text('Activity'), findsOneWidget);

        await tester.pumpAndSettle();
        expect(find.text('Check conditions & suitability'), findsOneWidget);
      },
    );

    testWidgets(
      'M2-MOB-FORM-002 rejects malformed assessment evidence without showing a result',
      (tester) async {
        await tester.pumpWidget(
          MaterialApp(home: SuitabilityScreen(service: _dummyService())),
        );

        expect(find.text('CONDITION QUERY'), findsOneWidget);
        expect(find.text('Check conditions & suitability'), findsOneWidget);

        await tester.enterText(
          find.widgetWithText(TextField, 'Latitude (−90 to 90)'),
          '6',
        );
        await tester.enterText(
          find.widgetWithText(TextField, 'Longitude (−180 to 180)'),
          '80',
        );

        await tester.tap(
          find.widgetWithText(FilledButton, 'Check conditions & suitability'),
        );
        await tester.pumpAndSettle();

        expect(
          find.text(
            'The marine service could not be reached. Check the gateway address and try again.',
          ),
          findsOneWidget,
        );
        expect(find.text('SUITABLE'), findsNothing);
        expect(find.text('UNKNOWN'), findsNothing);
      },
    );

    testWidgets(
      'M2-MOB-FORM-003 uses the activity identifiers returned by the public API',
      (tester) async {
        await tester.pumpWidget(
          MaterialApp(home: SuitabilityScreen(service: _dummyService())),
        );

        await tester.pumpAndSettle();
        final activityItems = tester
            .widget<DropdownButton<String>>(find.byType(DropdownButton<String>))
            .items!;

        expect(activityItems, isNotEmpty);
        final ids = activityItems.map((item) => item.value).toList();
        expect(ids, contains('33333333-3333-3333-3333-333333333301'));
        expect(ids, contains('33333333-3333-3333-3333-333333333302'));
        expect(ids, contains('33333333-3333-3333-3333-333333333303'));
        expect(ids, contains('33333333-3333-3333-3333-333333333304'));
        expect(ids, contains('33333333-3333-3333-3333-333333333305'));
      },
    );
  });
}

MarineService _dummyService() {
  final repository = MarineRepository(client: _dummyClient());
  return MarineService(repository: repository);
}

MarineApiClient _dummyClient() {
  return MarineApiClient(
    client: MockClient(
      (request) => Future.value(
        http.Response(
          request.url.path == '/api/marine/activities'
              ? jsonEncode(
                  List.generate(
                    5,
                    (index) => {
                      'id': '33333333-3333-3333-3333-33333333330${index + 1}',
                      'name': [
                        'Surfing',
                        'Snorkeling',
                        'Scuba Diving',
                        'Whale and Dolphin Watching',
                        'Coastal Boat Tour',
                      ][index],
                      'activityType': 'Fixture category',
                      'isActive': true,
                    },
                  ),
                )
              : '{}',
          200,
        ),
      ),
    ),
    gateway: const ApiGatewayConfig(),
  );
}
