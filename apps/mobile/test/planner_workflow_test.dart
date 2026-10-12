import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/models/planner_models.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/planner_api_service.dart';
import 'package:mobile/ui/planner_screens.dart';
import 'package:mobile/ui/planner_navigation.dart';
import 'package:mobile/ui/planner_workflow_screens.dart';

import 'support/mobile_test_support.dart';

Map<String, dynamic> stop(String id, String title, int hour) => {
  'itemId': id,
  'title': title,
  'destinationId': 'destination',
  'activityId': id,
  'offeringId': 'offering-$id',
  'scheduledStart': '2026-10-20T0$hour:00:00Z',
  'scheduledEnd': '2026-10-20T0${hour + 1}:00:00Z',
  'scheduledStartLocal':
      '2026-10-20T${(hour + 5).toString().padLeft(2, '0')}:30:00',
  'scheduledEndLocal':
      '2026-10-20T${(hour + 6).toString().padLeft(2, '0')}:30:00',
  'timeZone': 'Asia/Colombo',
  'lastAvailabilityStatus': 'AVAILABLE',
  'lastSuitabilityStatus': 'SUITABLE',
  'lastOperationalStatus': 'ACTIVE',
};
Map<String, dynamic> tripJson({String status = 'DRAFT'}) => {
  'itineraryId': 'trip',
  'title': 'Coastal day',
  'description': 'Bring water',
  'status': status,
  'startsAt': '2026-10-20T02:00:00Z',
  'endsAt': '2026-10-20T09:00:00Z',
  'startsAtLocal': '2026-10-20T07:30:00',
  'endsAtLocal': '2026-10-20T14:30:00',
  'timeZone': 'Asia/Colombo',
  'concurrencyVersion': 7,
  'items': [stop('first', 'Reef walk', 3), stop('second', 'Kayaking', 5)],
};
PlannerApiService service(
  FutureOr<http.Response> Function(http.Request) handler, {
  Duration timeout = const Duration(seconds: 55),
  ApiGatewayConfig? gateway,
}) => PlannerApiService(
  client: MockClient((request) async => handler(request)),
  storage: MemoryAuthCredentialStore(),
  requestTimeout: timeout,
  gateway: gateway,
);
http.Response ok(Object json) => http.Response(jsonEncode(json), 200);

class TwoGateways extends ApiGatewayConfig {
  @override
  List<Uri> get baseUris => [
    Uri.parse('http://localhost'),
    Uri.parse('http://127.0.0.1'),
  ];
}

void main() {
  testWidgets(
    'MOB-PLANNER-022 guest introduction sends create action to sign-in with a safe return',
    (tester) async {
      RouteSettings? received;
      await tester.pumpWidget(
        MaterialApp(
          onGenerateRoute: (settings) {
            received = settings;
            return MaterialPageRoute<void>(
              settings: settings,
              builder: (_) => const Scaffold(body: Text('Sign-in destination')),
            );
          },
          home: PlannerHomeScreen(user: null, api: service((_) => ok([]))),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('A coastal day, made for you.'), findsOneWidget);
      await tester.tap(find.text('Let’s plan your trip'));
      await tester.pumpAndSettle();
      expect(received?.name, '/signin');
      final returnRoute = plannerLoginReturn(received?.arguments);
      expect(returnRoute.$1, '/planner/plan');
      expect(returnRoute.$2, isNull);
    },
  );

  test('MOB-PLANNER-021 a wildlife result must match the requested destination and activity', () async {
    final metadata = {
      'modelVersion': 'fixture-v1',
      'inferenceTimestamp': '2026-10-12T02:00:00Z',
    };
    for (final identity in [('other', 'surf'), ('destination', 'other')]) {
      final api = service(
        (_) => ok({
          'contractVersion': 1,
          'status': 'available',
          'destinationId': identity.$1,
          'activityId': identity.$2,
          'modelMetadata': metadata,
          'provenance': {...metadata, 'provider': 'fixture-provider'},
          'predictedSpecies': [],
          'limitations': 'Context only.',
        }),
      );
      await expectLater(
        api.biodiversityPredictions(
          destinationId: 'destination',
          activityId: 'surf',
        ),
        throwsA(
          isA<PlannerApiException>().having((e) => e.statusCode, 'status', 502),
        ),
      );
    }
  });
  test('MOB-PLANNER-020 malformed species never become displayable wildlife predictions', () {
    final validSpecies = {
      'speciesId': 'species',
      'commonName': 'Dolphin',
      'scientificName': 'Delphinus delphis',
      'confidenceLevel': 'context-only',
      'habitatSuitability': 0.7,
    };
    final metadata = {
      'modelVersion': 'fixture-v1',
      'inferenceTimestamp': '2026-10-12T02:00:00Z',
    };
    Map<String, dynamic> payload(Object species) => {
      'contractVersion': 1,
      'destinationId': 'destination',
      'status': 'available',
      'predictedSpecies': species,
      'modelMetadata': metadata,
      'provenance': {...metadata, 'provider': 'fixture-provider'},
      'limitations': 'Context only.',
    };
    final valid = PlannerBiodiversityPrediction.fromJson(
      payload([validSpecies]),
    );
    expect(valid.status, 'available');
    expect(valid.predictedSpecies.single.habitatSuitability, 0.7);
    for (final boundary in [0, 1]) {
      final result = PlannerBiodiversityPrediction.fromJson(
        payload([
          {...validSpecies, 'habitatSuitability': boundary},
        ]),
      );
      expect(result.status, 'available');
      expect(
        result.predictedSpecies.single.habitatSuitability,
        boundary.toDouble(),
      );
    }
    for (final invalid in [
      {
        ...payload([validSpecies]),
        'contractVersion': 1.1,
      },
      {
        ...payload([validSpecies]),
        'modelMetadata': {
          ...metadata,
          'inferenceTimestamp': '2026-10-12T02:00:00',
        },
      },
      {
        ...payload([validSpecies]),
        'provenance': {
          ...metadata,
          'provider': 'fixture-provider',
          'inferenceTimestamp': '2026-10-12T03:00:00Z',
        },
      },
    ]) {
      final result = PlannerBiodiversityPrediction.fromJson(invalid);
      expect(result.status, 'unavailable');
      expect(result.predictedSpecies, isEmpty);
    }
    for (final species in [
      [42],
      [validSpecies, validSpecies],
      [
        {...validSpecies, 'habitatSuitability': -0.01},
      ],
      [
        {...validSpecies, 'habitatSuitability': 1.01},
      ],
      [
        {...validSpecies, 'habitatSuitability': double.nan},
      ],
      [
        {...validSpecies, 'commonName': ''},
      ],
    ]) {
      final result = PlannerBiodiversityPrediction.fromJson(payload(species));
      expect(result.status, 'unavailable');
      expect(result.predictedSpecies, isEmpty);
      expect(result.modelMetadata, isNull);
    }
  });
  testWidgets(
    'MOB-PLANNER-018 history failure can retry without changing the itinerary',
    (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerReviewHistory(
            trip: PlannerItinerary.fromJson(tripJson()),
            api: service((request) {
              expect(request.method, 'GET');
              expect(
                request.url.path,
                '/api/planner/itineraries/trip/re-evaluations',
              );
              calls++;
              return calls == 1 ? http.Response('{}', 503) : ok([]);
            }),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('History unavailable · Try again'));
      await tester.pumpAndSettle();
      expect(find.text('No condition reviews yet.'), findsOneWidget);
      expect(calls, 2);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'MOB-PLANNER-019 unverifiable wildlife response shows neutral guidance and no species',
    (tester) async {
      final candidate = PlannerCandidate.fromJson({
        'destinationId': 'destination',
        'activityId': 'surf',
        'title': 'Surf',
        'scheduledStart': '2026-10-20T02:00:00Z',
        'scheduledEnd': '2026-10-20T03:00:00Z',
        'availabilityStatus': 'AVAILABLE',
        'operationalStatus': 'OPEN',
        'suitability': {'status': 'SUITABLE'},
        'fitScore': 0.8,
        'reasons': [],
      });
      var calls = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PlannerWildlifeContextCard(
              candidate: candidate,
              api: service((request) {
                calls++;
                expect(request.method, 'GET');
                expect(
                  request.url.path,
                  '/api/planner/biodiversity/predictions',
                );
                expect(request.url.queryParameters, {
                  'destinationId': 'destination',
                  'activityId': 'surf',
                });
                return ok({
                  'contractVersion': 1,
                  'status': 'available',
                  'destinationId': 'destination',
                  'message': 'Unverified provider message',
                  'predictedSpecies': [
                    {'commonName': 'Invented species'},
                  ],
                });
              }),
            ),
          ),
        ),
      );
      await tester.tap(find.text('Check current wildlife context'));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Wildlife predictions are temporarily unavailable. Please try again later.',
        ),
        findsOneWidget,
      );
      expect(find.textContaining('Invented species'), findsNothing);
      expect(find.text('Unverified provider message'), findsNothing);
      expect(calls, 1);
      expect(tester.takeException(), isNull);
    },
  );
  test('MOB-PLANNER-013 shared loading finishes after both success and transport failure', () async {
    for (final fail in [false, true]) {
      var starts = 0, finishes = 0;
      final api = PlannerApiService(
        storage: MemoryAuthCredentialStore(),
        gateway: TwoGateways(),
        onRequestStarted: () {
          starts++;
          return () {
            finishes++;
          };
        },
        client: MockClient((_) async {
          if (fail) throw http.ClientException('Transport unavailable');
          return ok(tripJson());
        }),
      );
      if (fail) {
        await expectLater(
          api.itinerary('trip'),
          throwsA(isA<PlannerApiException>()),
        );
      } else {
        expect((await api.itinerary('trip')).id, 'trip');
      }
      expect(starts, 1);
      expect(finishes, 1);
    }
  });
  testWidgets(
    'MOB-PLANNER-014 denied recommendation read does not load snapshots or workflows',
    (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerRecommendationScreen(
            api: service((_) {
              calls++;
              return ok({});
            }),
            user: mobileTestUser(),
            recommendationId: 'snapshot',
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        find.text('Your account cannot view these trip ideas.'),
        findsOneWidget,
      );
      expect(find.text('Save draft'), findsNothing);
      expect(calls, 0);
    },
  );
  testWidgets(
    'MOB-PLANNER-015 unavailable result differs from a true empty match and honors read-only grants',
    (tester) async {
      final calls = <String>[];
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerRecommendationScreen(
            user: mobileTestUser(permissions: ['planner.recommendations.read']),
            recommendationId: 'snapshot',
            api: service((request) {
              calls.add(request.url.path);
              return ok({
                'recommendationId': 'snapshot',
                'workflowId': 'workflow',
                'status': 'COMPLETED',
                'generatedAt': '2026-10-20T02:00:00Z',
                'outcome': 'DEPENDENCIES_UNAVAILABLE',
                'candidates': [],
                'excludedCandidatesCount': 0,
                'uncertaintyNotes': ['Marine evidence is unavailable.'],
              });
            }),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Current experience information is unavailable. Please try again later.',
        ),
        findsOneWidget,
      );
      expect(find.text('Marine evidence is unavailable.'), findsOneWidget);
      expect(find.text('Save draft'), findsNothing);
      expect(calls, ['/api/planner/recommendations/snapshot']);
    },
  );
  testWidgets(
    'MOB-PLANNER-016 saved trips can reach a second page and refresh after failure',
    (tester) async {
      final pages = <String?>[];
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerSavedTripsScreen(
            user: mobileTestUser(permissions: ['planner.itineraries.manage']),
            api: service((request) {
              pages.add(request.url.queryParameters['page']);
              return ok(
                pages.length == 1
                    ? List.generate(
                        20,
                        (i) => {...tripJson(), 'title': 'Day $i'},
                      )
                    : [],
              );
            }),
          ),
        ),
      );
      await tester.pumpAndSettle();
      final historyScroll = tester
          .state<ScrollableState>(find.byType(Scrollable).first)
          .position;
      expect(historyScroll.maxScrollExtent, greaterThan(0));
      historyScroll.jumpTo(historyScroll.maxScrollExtent);
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      expect(find.text('Page 2'), findsOneWidget);
      expect(
        find.text('No trips on this page. Your next coastal day awaits.'),
        findsOneWidget,
      );
      expect(pages, ['1', '2']);
    },
  );
  testWidgets(
    'MOB-PLANNER-017 editor conflict preserves the draft and unsaved name',
    (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerDraftEditor(
            trip: PlannerItinerary.fromJson(tripJson()),
            api: service((request) {
              calls++;
              final body = jsonDecode(request.body) as Map;
              expect(body['title'], 'My revised day');
              expect(body['concurrencyVersion'], 7);
              return http.Response('{"detail":"Reload the latest trip."}', 409);
            }),
          ),
        ),
      );
      await tester.enterText(find.byType(TextField).first, 'My revised day');
      await tester.scrollUntilVisible(
        find.text('Save draft changes'),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Save draft changes'));
      await tester.pumpAndSettle();
      expect(find.text('Reload the latest trip.'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.widgetWithText(TextField, 'My revised day'),
        -500,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('My revised day'), findsOneWidget);
      expect(calls, 1);
    },
  );
  test(
    'MOB-PLANNER-001 pagination preserves the path and sends query values',
    () async {
      final requests = <http.Request>[];
      final api = service((request) {
        requests.add(request);
        return ok([tripJson()]);
      });
      final trips = await api.itineraries(page: 2, pageSize: 20);
      expect(requests.single.method, 'GET');
      expect(requests.single.url.path, '/api/planner/itineraries');
      expect(requests.single.url.queryParameters, {
        'page': '2',
        'pageSize': '20',
      });
      expect(trips.single.items.first.id, 'first');
      expect(trips.single.concurrencyVersion, 7);
      expect(trips.single.startsAtLocal, '2026-10-20T07:30:00');
      await expectLater(api.itineraries(page: 0), throwsArgumentError);
      expect(requests, hasLength(1));
    },
  );
  test('MOB-PLANNER-002 title edits preserve item identities, ordering and evidence version', () async {
    final trip = PlannerItinerary.fromJson(tripJson());
    late Map<String, dynamic> body;
    final api = service((request) {
      expect(request.method, 'PUT');
      expect(request.url.path, '/api/planner/itineraries/trip');
      body = jsonDecode(request.body) as Map<String, dynamic>;
      return ok({...tripJson(), 'title': 'New name', 'concurrencyVersion': 8});
    });
    final changed = await api.updateItinerary(
      trip,
      title: 'New name',
      description: '',
    );
    expect(changed.title, 'New name');
    expect(changed.concurrencyVersion, 8);
    expect(body['description'], isNull);
    expect(body['concurrencyVersion'], 7);
    final items = body['items'] as List;
    expect(items.map((item) => item['itemId']), ['first', 'second']);
    expect(items.map((item) => item['orderIndex']), [0, 1]);
    expect(items.first['scheduledStart'], '2026-10-20T03:00:00Z');
  });
  test('MOB-PLANNER-003 adding a suggestion includes snapshot and retains prior IDs', () async {
    final candidate = PlannerCandidate.fromJson({
      'destinationId': 'destination',
      'activityId': 'surf',
      'offeringId': 'surf-offering',
      'title': 'Surf',
      'timeZone': 'Asia/Colombo',
      'scheduledStart': '2026-10-20T10:00:00Z',
      'scheduledEnd': '2026-10-20T11:00:00Z',
      'availabilityStatus': 'AVAILABLE',
      'suitability': {'status': 'SUITABLE'},
      'operationalStatus': 'ACTIVE',
      'fitScore': 0.8,
      'reasons': ['Fits your request'],
    });
    final api = service((request) {
      final body = jsonDecode(request.body) as Map;
      expect(body['recommendationId'], 'snapshot');
      expect(body['endsAt'], '2026-10-20T11:00:00Z');
      expect(body['startsAt'], '2026-10-20T02:00:00Z');
      expect((body['items'] as List).map((i) => i['itemId']), [
        'first',
        'second',
        null,
      ]);
      expect((body['items'] as List).last['orderIndex'], 2);
      return ok(tripJson());
    });
    await api.addCandidate(
      PlannerItinerary.fromJson(tripJson()),
      candidate,
      'snapshot',
    );
  });
  test('MOB-PLANNER-004 ambiguous write timeout is never replayed on another gateway', () async {
    var calls = 0;
    final api = service(
      (request) {
        calls++;
        return Completer<http.Response>().future;
      },
      timeout: const Duration(milliseconds: 1),
      gateway: TwoGateways(),
    );
    await expectLater(
      api.confirmItinerary('trip'),
      throwsA(
        isA<PlannerApiException>()
            .having((e) => e.statusCode, 'status', 0)
            .having(
              (e) => e.message,
              'recovery',
              contains('Refresh your saved trips'),
            ),
      ),
    );
    expect(calls, 1);
  });
  test('MOB-PLANNER-005 stale updates preserve the conflict response without retry', () async {
    var calls = 0;
    final api = service((request) {
      calls++;
      return http.Response('{"detail":"Reload the latest trip."}', 409);
    });
    await expectLater(
      api.updateItinerary(
        PlannerItinerary.fromJson(tripJson()),
        title: 'New name',
        description: '',
      ),
      throwsA(
        isA<PlannerApiException>()
            .having((e) => e.statusCode, 'status', 409)
            .having((e) => e.message, 'message', 'Reload the latest trip.'),
      ),
    );
    expect(calls, 1);
  });
  test('MOB-PLANNER-006 review history uses the public GET endpoint and rejects malformed lists', () async {
    final api = service((request) {
      expect(request.method, 'GET');
      expect(request.url.path, '/api/planner/itineraries/trip/re-evaluations');
      return ok([
        {'evaluationId': 'review', 'hasChanges': true, 'items': []},
      ]);
    });
    expect(
      (await api.evaluationHistory('trip')).single['evaluationId'],
      'review',
    );
    await expectLater(
      service((_) => ok([42])).evaluationHistory('trip'),
      throwsFormatException,
    );
  });
  test(
    'MOB-PLANNER-007 unauthorized responses never become trip data',
    () async {
      await expectLater(
        service((_) => http.Response('{}', 403)).itinerary('trip'),
        throwsA(
          isA<PlannerApiException>().having((e) => e.statusCode, 'status', 403),
        ),
      );
      await expectLater(
        service((_) => http.Response('malformed', 200)).itinerary('trip'),
        throwsFormatException,
      );
    },
  );
  testWidgets(
    'MOB-PLANNER-008 editor reorder and save retain IDs with contiguous ordering',
    (tester) async {
      Map<String, dynamic>? sent;
      final api = service((request) {
        sent = jsonDecode(request.body) as Map<String, dynamic>;
        return ok(tripJson());
      });
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerDraftEditor(
            trip: PlannerItinerary.fromJson(tripJson()),
            api: api,
          ),
        ),
      );
      await tester.ensureVisible(find.byTooltip('Move later').first);
      await tester.tap(find.byTooltip('Move later').first);
      await tester.pump();
      expect(
        tester.getTopLeft(find.text('Kayaking')).dy,
        lessThan(tester.getTopLeft(find.text('Reef walk')).dy),
      );
      await tester.scrollUntilVisible(
        find.text('Save draft changes'),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Save draft changes'));
      await tester.pumpAndSettle();
      expect(sent, isNotNull);
      expect(sent!['concurrencyVersion'], 7);
      expect((sent!['items'] as List).map((item) => item['itemId']), [
        'second',
        'first',
      ]);
      expect((sent!['items'] as List).map((item) => item['orderIndex']), [
        0,
        1,
      ]);
      expect(sent!['startsAt'], '2026-10-20T07:30:00');
      expect(
        (sent!['items'] as List).first['scheduledStart'],
        '2026-10-20T10:30:00',
      );
      expect(sent!['timeZone'], 'Asia/Colombo');
      expect(sent!['confirmPlan'], false);
    },
  );
  testWidgets('MOB-PLANNER-009 empty editor does not send a write', (
    tester,
  ) async {
    var calls = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: PlannerDraftEditor(
          trip: PlannerItinerary.fromJson({...tripJson(), 'items': []}),
          api: service((_) {
            calls++;
            return ok(tripJson());
          }),
        ),
      ),
    );
    await tester.tap(find.text('Save draft changes'));
    await tester.pump();
    expect(
      find.text('Name your trip and keep at least one experience.'),
      findsOneWidget,
    );
    expect(calls, 0);
  });
  testWidgets(
    'MOB-PLANNER-010 denied trip permissions perform no network requests',
    (tester) async {
      var calls = 0;
      final api = service((_) {
        calls++;
        return ok(tripJson());
      });
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerItineraryScreen(
            api: api,
            itineraryId: 'trip',
            user: mobileTestUser(),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        find.text('Your account cannot manage this trip.'),
        findsOneWidget,
      );
      expect(find.text('Edit draft'), findsNothing);
      expect(calls, 0);
    },
  );
  testWidgets(
    'MOB-PLANNER-011 review comparison names the stable item and does not mutate choices',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PlannerReviewCard(
              trip: PlannerItinerary.fromJson(tripJson()),
              review: {
                'summary': 'Conditions changed',
                'requiresReview': true,
                'items': [
                  {
                    'itemId': 'first',
                    'previousAvailability': 'AVAILABLE',
                    'currentAvailability': 'UNAVAILABLE',
                    'previousSuitability': 'SUITABLE',
                    'currentSuitability': 'CAUTION',
                    'previousOperationalStatus': 'ACTIVE',
                    'currentOperationalStatus': 'SUSPENDED',
                    'suggestedAction': 'REMOVE',
                    'advisoryMessage':
                        'Operator has suspended this experience.',
                  },
                ],
              },
            ),
          ),
        ),
      );
      expect(find.text('Reef walk'), findsOneWidget);
      expect(
        find.text('Availability: AVAILABLE → UNAVAILABLE'),
        findsOneWidget,
      );
      expect(
        find.text('Marine suitability: SUITABLE → CAUTION'),
        findsOneWidget,
      );
      expect(find.text('Operations: ACTIVE → SUSPENDED'), findsOneWidget);
      expect(find.text('Suggested action: REMOVE'), findsOneWidget);
      expect(
        find.text('Your selected experiences remain unchanged.'),
        findsOneWidget,
      );
    },
  );
  testWidgets(
    'MOB-PLANNER-012 recommendation-only accounts can plan without management grants',
    (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: PlannerPlanScreen(
            user: mobileTestUser(
              permissions: ['planner.recommendations.create'],
            ),
            api: service((request) {
              calls++;
              expect(request.url.path, '/api/planner/catalogue');
              return ok({
                'status': 'UNAVAILABLE',
                'message': 'Try later',
                'destinations': [],
              });
            }),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Create a plan'), findsNWidgets(2));
      expect(
        find.text('Your account cannot create and manage coastal trips.'),
        findsNothing,
      );
      expect(calls, 1);
    },
  );
}
