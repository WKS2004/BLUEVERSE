import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/blueverse_theme.dart';
import 'package:mobile/ui/planner_motion.dart';

void main() {
  testWidgets(
    'MOB-PLANNER-023 create and history transitions enter in opposite directions',
    (tester) async {
      expect(
        BlueverseTheme.light.pageTransitionsTheme.builders.values,
        everyElement(isA<PlannerPageMotion>()),
      );
      for (final (routeName, direction) in [
        ('/planner/saved', 1.0),
        ('/planner/plan', -1.0),
      ]) {
        final route = PageRouteBuilder<void>(
          settings: RouteSettings(name: routeName),
          pageBuilder: (_, _, _) => const SizedBox.shrink(),
        );
        await tester.pumpWidget(
          MediaQuery(
            data: const MediaQueryData(),
            child: Directionality(
              textDirection: TextDirection.ltr,
              child: Builder(
                builder: (context) =>
                    const PlannerPageMotion().buildTransitions<void>(
                      route,
                      context,
                      const AlwaysStoppedAnimation<double>(0.5),
                      const AlwaysStoppedAnimation<double>(0),
                      const SizedBox(key: Key('planner-page')),
                    ),
              ),
            ),
          ),
        );
        final slide = tester.widget<SlideTransition>(
          find.byType(SlideTransition),
        );
        expect(slide.position.value.dx.sign, direction.sign);
      }
    },
  );

  testWidgets('MOB-PLANNER-024 planner transition respects reduced motion', (
    tester,
  ) async {
    final route = PageRouteBuilder<void>(
      settings: const RouteSettings(name: '/planner/saved'),
      pageBuilder: (_, _, _) => const SizedBox.shrink(),
    );
    await tester.pumpWidget(
      MediaQuery(
        data: const MediaQueryData(disableAnimations: true),
        child: Directionality(
          textDirection: TextDirection.ltr,
          child: Builder(
            builder: (context) =>
                const PlannerPageMotion().buildTransitions<void>(
                  route,
                  context,
                  const AlwaysStoppedAnimation<double>(0.5),
                  const AlwaysStoppedAnimation<double>(0),
                  const SizedBox(key: Key('planner-page')),
                ),
          ),
        ),
      ),
    );
    expect(find.byKey(const Key('planner-page')), findsOneWidget);
    expect(find.byType(SlideTransition), findsNothing);
    expect(find.byType(FadeTransition), findsNothing);
  });
}
