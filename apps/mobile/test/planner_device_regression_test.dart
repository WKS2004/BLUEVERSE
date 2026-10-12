import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/models/planner_models.dart';
import 'package:mobile/data/services/planner_api_service.dart';
import 'package:mobile/ui/planner_screens.dart';

import 'support/mobile_test_support.dart';

// Shared workflows: planner-plan and planner-saved-trips. Recovery and
// unobscured touch controls are required by the UI experience principles.
void main() {
  testWidgets(
    'MOB-PLANNER-025 unavailable catalogue retries once inline and recovers',
    (tester) async {
      var calls = 0;
      var overlayStarts = 0;
      final recovered = Completer<http.Response>();
      final credentials = MemoryAuthCredentialStore()
        ..values['blueverse.access_token'] = 'synthetic-device-test-token';
      final api = PlannerApiService(
        storage: credentials,
        onRequestStarted: () {
          overlayStarts++;
          return () {};
        },
        client: MockClient((request) async {
          expect(request.method, 'GET');
          expect(request.url.path, '/api/planner/catalogue');
          expect(
            request.headers['Authorization'],
            'Bearer synthetic-device-test-token',
          );
          calls++;
          if (calls == 1) {
            return http.Response(
              jsonEncode({
                'status': 'UNAVAILABLE',
                'message': 'Catalogue is temporarily unavailable.',
                'destinations': [],
              }),
              200,
            );
          }
          return recovered.future;
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerPlanScreen(
            user: mobileTestUser(
              permissions: ['planner.recommendations.create'],
            ),
            api: api,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('Try again'),
        500,
        scrollable: find.byType(Scrollable).first,
      );
      expect(calls, 1);
      await tester.tap(find.text('Try again'));
      await tester.tap(find.text('Try again'));
      await tester.pump();
      expect(calls, 2);
      expect(overlayStarts, 0);
      expect(find.text('Preparing your coastal planner…'), findsOneWidget);
      await tester.drag(find.byType(ListView).first, const Offset(0, 3000));
      await tester.pump();
      expect(find.text('Create your coastal plan.'), findsOneWidget);
      recovered.complete(
        http.Response(
          jsonEncode({
            'status': 'AVAILABLE',
            'destinations': [
              {
                'destinationId': 'unawatuna',
                'name': 'Unawatuna',
                'region': 'South coast',
                'timeZone': 'Asia/Colombo',
                'activities': [],
              },
            ],
          }),
          200,
        ),
      );
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('Destination'),
        500,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(
        find.byType(DropdownButtonFormField<PlannerDestination>),
      );
      await tester.pumpAndSettle();
      expect(find.text('Unawatuna · South coast'), findsWidgets);
      expect(find.text('Preparing your coastal planner…'), findsNothing);
      expect(find.text('Try again'), findsNothing);
      expect(calls, 2);
      expect(overlayStarts, 0);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'MOB-PLANNER-026 history paging stays above Android navigation inset',
    (tester) async {
      tester.view.physicalSize = const Size(400, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = PlannerApiService(
        storage: MemoryAuthCredentialStore(),
        client: MockClient((request) async {
          expect(request.method, 'GET');
          expect(request.url.path, '/api/planner/itineraries');
          return http.Response('[]', 200);
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          builder: (context, child) => MediaQuery(
            data: MediaQuery.of(context).copyWith(
              padding: const EdgeInsets.only(bottom: 80),
              viewPadding: const EdgeInsets.only(bottom: 80),
            ),
            child: child!,
          ),
          home: PlannerSavedTripsScreen(
            user: mobileTestUser(permissions: ['planner.itineraries.manage']),
            api: api,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.drag(find.byType(ListView).first, const Offset(0, -4000));
      await tester.pumpAndSettle();
      expect(
        find.text('No trips on this page. Your next coastal day awaits.'),
        findsOneWidget,
      );
      expect(tester.getRect(find.text('Next')).bottom, lessThanOrEqualTo(720));
      expect(
        tester.getRect(find.text('Previous')).bottom,
        lessThanOrEqualTo(720),
      );
      expect(tester.takeException(), isNull);
    },
  );
}
