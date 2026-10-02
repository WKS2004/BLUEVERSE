import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/coastal_operations_search.dart';
import 'package:mobile/features/coastal_operations/coastal_operations_permissions.dart';

void main() {
  testWidgets(
    'MOB-OPS-PAGES-001 assessment filters send draft ownership and reset pagination (ui-integration: coastal-operations-assessment)',
    (tester) async {
      Map<String, String>? query;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: CoastalOperationsSearch(
                assessments: true,
                canManage: true,
                onApply: (value) => query = value,
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('Drafts'));
      await tester.pumpAndSettle();
      expect(query, {'onlyMine': 'true', 'workflowStatus': 'DRAFT'});
      await tester.enterText(find.byType(TextFormField).first, 'shoreline');
      await tester.ensureVisible(find.byTooltip('Search'));
      await tester.tap(find.byTooltip('Search'));
      await tester.pumpAndSettle();
      expect(query!['search'], 'shoreline');
      expect(query!.containsKey('cursor'), isFalse);
      await tester.tap(find.byTooltip('Reset filters'));
      await tester.pumpAndSettle();
      expect(query, isEmpty);
      expect(find.text('shoreline'), findsNothing);
    },
  );
  testWidgets(
    'MOB-OPS-PAGES-002 public alert readers only have public lifecycle controls (ui-integration: coastal-operations-alerts)',
    (tester) async {
      Map<String, String>? query;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: CoastalOperationsSearch(
                assessments: false,
                canManage: false,
                onApply: (value) => query = value,
              ),
            ),
          ),
        ),
      );
      expect(find.text('Drafts'), findsNothing);
      expect(find.text('History'), findsNothing);
      expect(find.text('Audience'), findsNothing);
      await tester.tap(find.text('Active'));
      await tester.pumpAndSettle();
      expect(query, {'lifecycle': 'ACTIVE'});
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'MOB-OPS-PAGES-003 invalid target ID prevents submitting search (ui-integration: coastal-operations-assessment)',
    (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: CoastalOperationsSearch(
                assessments: true,
                canManage: false,
                onApply: (_) => calls++,
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.byTooltip('Filters'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Advanced search'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.bySemanticsLabel('Coastal record ID'),
        'invalid',
      );
      await tester.ensureVisible(find.byTooltip('Search'));
      await tester.tap(find.byTooltip('Search'));
      await tester.pumpAndSettle();
      expect(calls, 0);
      expect(
        find.text('Enter a valid record ID in Advanced search.'),
        findsOneWidget,
      );
    },
  );
  test('MOB-OPS-PERMISSION-001 all additive alert grants provide alert navigation and audit alone grants no management', () {
    for (final permission in [
      CoastalOperationsPermissions.alertCreate,
      CoastalOperationsPermissions.alertUpdate,
      CoastalOperationsPermissions.alertDelete,
      CoastalOperationsPermissions.alertPublish,
      CoastalOperationsPermissions.alertResolve,
    ]) {
      expect(CoastalOperationsPermissions.hasAccess([permission]), isTrue);
      expect(CoastalOperationsPermissions.hasAlertAccess([permission]), isTrue);
      expect(
        CoastalOperationsPermissions.canManageAlerts([permission]),
        isTrue,
      );
      expect(
        CoastalOperationsPermissions.hasAssessmentAccess([permission]),
        isFalse,
      );
    }
    expect(
      CoastalOperationsPermissions.canManageAlerts([
        CoastalOperationsPermissions.auditRead,
      ]),
      isFalse,
    );
  });
}
