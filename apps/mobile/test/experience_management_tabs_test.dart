import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/experiences/catalogue_management_screen.dart';
import 'package:mobile/ui/experiences/experience_view_model.dart';

import 'experience_rebuild_test.dart' show Repo;
import 'support/mobile_test_support.dart';

void main() {
  // experience-catalogue-management: no unused leading tab offset at any width.
  for (final width in [320.0, 400.0, 1200.0]) {
    testWidgets(
      'MOB-EXP-049 management tabs start without a leading gap at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 800);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final vm = ExperienceViewModel(repository: Repo());
        addTearDown(vm.dispose);
        final user = mobileTestUser(
          permissions: ['experiences.catalogue.manage'],
        );
        final repo = FakeAuthRepository(user: user);
        addTearDown(repo.close);
        final auth = AuthViewModel(repository: repo)..user = user;
        addTearDown(auth.dispose);
        await tester.pumpWidget(
          MaterialApp(
            home: CatalogueManagementScreen(viewModel: vm, authViewModel: auth),
          ),
        );
        await tester.pumpAndSettle();
        final bar = tester.widget<TabBar>(find.byType(TabBar));
        expect(bar.tabAlignment, TabAlignment.start);
        expect(bar.padding, EdgeInsets.zero);
        final first = find.ancestor(
          of: find.text('Destinations'),
          matching: find.byType(Tab),
        );
        // A normal 16px label inset is retained; Material 3's extra 52px is absent.
        expect(
          tester.getRect(first).left - tester.getRect(find.byType(TabBar)).left,
          closeTo(16, 0.1),
        );
        await tester.tap(find.text('Activities'));
        await tester.pumpAndSettle();
        expect(vm.managementTab, 1);
        expect(tester.takeException(), isNull);
      },
    );
  }
}
