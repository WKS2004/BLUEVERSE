import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/services/api_gateway_config.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/coastal_operations_api_service.dart';
import 'package:mobile/ui/coastal_operations_draft_forms.dart';

import 'support/mobile_test_support.dart';

const _options = {
  'timeZones': [
    {
      'id': 'Etc/UTC',
      'country': 'Worldwide',
      'location': 'UTC',
      'rulesAvailable': true,
    },
    {
      'id': 'Asia/Colombo',
      'country': 'Sri Lanka',
      'location': 'Colombo',
      'rulesAvailable': true,
    },
  ],
  'targets': {'status': 'NOT_CONNECTED', 'items': []},
  'plans': {'status': 'NOT_CONNECTED', 'items': []},
  'assessments': [],
};

void main() {
  Future<CoastalOperationsApiService> render(
    WidgetTester tester,
    MockClient client,
  ) async {
    final authClient = MockClient((_) async => http.Response('{}', 401));
    final api = CoastalOperationsApiService(
      authApiService: AuthApiService(
        client: authClient,
        storage: MemoryAuthCredentialStore(),
        gateway: const ApiGatewayConfig(),
      ),
      client: client,
      gateway: const ApiGatewayConfig(),
    );
    addTearDown(authClient.close);
    addTearDown(client.close);
    await tester.pumpWidget(
      mobileTestApp(
        home: Scaffold(
          body: Builder(
            builder: (context) => FilledButton(
              onPressed: () => showDialog<Object>(
                context: context,
                builder: (_) => CoastalAssessmentDraftDialog(apiService: api),
              ),
              child: const Text('Start draft'),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('Start draft'));
    await tester.pumpAndSettle();
    return api;
  }

  testWidgets(
    'MOB-OPS-RECORD-001 one database zone, title and named associations replace UUID inputs (ui-integration: coastal-operations-assessment)',
    (tester) async {
      final paths = <String>[];
      await render(
        tester,
        MockClient((request) async {
          paths.add(request.url.path);
          return http.Response(jsonEncode(_options), 200);
        }),
      );
      expect(paths, ['/api/operations/form-options']);
      expect(find.text('Assessment title'), findsOneWidget);
      expect(find.text('Time zone'), findsOneWidget);
      expect(find.text('Related coastal plan'), findsOneWidget);
      expect(find.text('Coastal record ID'), findsNothing);
      expect(find.text('Related coastal plan ID'), findsNothing);
      expect(find.textContaining('offset at'), findsNothing);
      expect(
        find.text('Coastal plans will appear when the planner is connected.'),
        findsOneWidget,
      );
      final dates = find.ancestor(
        of: find.bySemanticsLabel('Starts at'),
        matching: find.byType(TextFormField),
      );
      expect(tester.widget<TextFormField>(dates).controller!.text, isEmpty);
      await tester.ensureVisible(find.bySemanticsLabel('Starts at'));
      await tester.pumpAndSettle();
      await tester.tap(find.bySemanticsLabel('Starts at'));
      await tester.pumpAndSettle();
      expect(find.byType(DatePickerDialog), findsOneWidget);
      await tester.tap(find.text('OK').last);
      await tester.pumpAndSettle();
      expect(find.byType(TimePickerDialog), findsOneWidget);
      await tester.tap(find.text('OK').last);
      await tester.pumpAndSettle();
      expect(
        tester.widget<TextFormField>(dates).controller!.text,
        matches(RegExp(r'^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$')),
      );
    },
  );
  testWidgets(
    'MOB-OPS-RECORD-002 named planner choices keep IDs internal (ui-integration: coastal-operations-assessment)',
    (tester) async {
      const id = '00000000-0000-4000-8000-000000000010';
      await render(
        tester,
        MockClient(
          (_) async => http.Response(
            jsonEncode({
              ..._options,
              'plans': {
                'status': 'AVAILABLE',
                'items': [
                  {'id': id, 'title': 'Bentota coastal day'},
                ],
              },
            }),
            200,
          ),
        ),
      );
      final dropdown = find.widgetWithText(
        DropdownButtonFormField<String>,
        'Related coastal plan',
      );
      await tester.ensureVisible(dropdown);
      await tester.pumpAndSettle();
      await tester.tap(dropdown);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Bentota coastal day').last);
      await tester.pumpAndSettle();
      expect(find.text('Bentota coastal day'), findsOneWidget);
      expect(find.text(id), findsNothing);
      expect(
        tester.widget<DropdownButtonFormField<String>>(dropdown).initialValue,
        id,
      );
    },
  );
  testWidgets(
    'MOB-OPS-RECORD-003 unavailable option loading disables draft save and can recover (ui-integration: coastal-operations-assessment)',
    (tester) async {
      var fail = true;
      await render(
        tester,
        MockClient(
          (_) async =>
              http.Response(jsonEncode(fail ? {} : _options), fail ? 503 : 200),
        ),
      );
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'Save draft'),
            )
            .onPressed,
        isNull,
      );
      fail = false;
      await tester.tap(find.text('Retry selection lists'));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'Save draft'),
            )
            .onPressed,
        isNotNull,
      );
    },
  );
}
