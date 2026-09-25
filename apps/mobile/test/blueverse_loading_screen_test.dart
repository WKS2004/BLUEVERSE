import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/feedback/blueverse_loading_screen.dart';
import 'package:mobile/ui/feedback/loading_screen_controller.dart';

Widget _harness(
  LoadingScreenController controller, {
  bool reducedMotion = false,
}) {
  return MaterialApp(
    builder: (context, child) => MediaQuery(
      data: MediaQuery.of(context).copyWith(disableAnimations: reducedMotion),
      child: ListenableBuilder(
        listenable: controller,
        builder: (context, _) => Stack(
          fit: StackFit.expand,
          children: [
            ?child,
            if (controller.isVisible)
              BlueverseLoadingScreen(
                message: controller.message,
                isExiting: controller.isExiting,
                exitDuration: controller.exitAnimationDuration,
              ),
          ],
        ),
      ),
    ),
    home: const Scaffold(body: Center(child: Text('Account profile'))),
  );
}

void main() {
  testWidgets('MOBILE-LOADING-001 one screen covers overlapping work', (
    tester,
  ) async {
    final controller = LoadingScreenController();
    addTearDown(controller.dispose);

    await tester.pumpWidget(_harness(controller));
    expect(find.text('Account profile'), findsOneWidget);
    expect(find.text('Loading BLUEVERSE'), findsNothing);

    final finishFirst = controller.begin();
    await tester.pump();
    expect(find.text('Loading BLUEVERSE'), findsOneWidget);
    expect(find.text('A moment by the water'), findsOneWidget);
    expect(
      find.byKey(const ValueKey('blueverse-loading-mark')),
      findsOneWidget,
    );

    final finishSecond = controller.begin();
    finishFirst();
    await tester.pump();
    expect(find.text('Loading BLUEVERSE'), findsOneWidget);

    finishSecond();
    await tester.pump(const Duration(milliseconds: 180));
    expect(find.text('Loading BLUEVERSE'), findsNothing);
  });

  testWidgets('MOBILE-LOADING-002 loading mark respects reduced motion', (
    tester,
  ) async {
    final controller = LoadingScreenController();
    addTearDown(controller.dispose);

    await tester.pumpWidget(_harness(controller, reducedMotion: true));
    final finish = controller.begin();
    await tester.pump();

    expect(find.text('Loading BLUEVERSE'), findsOneWidget);
    expect(
      find.byKey(const ValueKey('blueverse-loading-mark')),
      findsOneWidget,
    );

    finish();
    await tester.pump(const Duration(milliseconds: 180));
    expect(find.text('Loading BLUEVERSE'), findsNothing);
  });

  testWidgets(
    'MOBILE-LOADING-003 slow authentication reveals the page through a wave',
    (tester) async {
      final controller = LoadingScreenController(
        minimumVisibleDuration: const Duration(milliseconds: 100),
        slowLoadThreshold: const Duration(milliseconds: 250),
        exitAnimationDuration: const Duration(milliseconds: 300),
      );
      addTearDown(controller.dispose);

      await tester.pumpWidget(_harness(controller));
      final finish = controller.begin(
        message: const LoadingScreenMessage.signIn(),
      );
      await tester.pump();
      expect(find.text('SECURE ACCOUNT TRANSITION'), findsOneWidget);
      expect(find.text('Signing you in'), findsOneWidget);

      await tester.pump(const Duration(milliseconds: 250));
      expect(controller.hasBeenSlow, isTrue);
      finish();
      await tester.pump(const Duration(milliseconds: 100));
      expect(controller.isVisible, isTrue);
      expect(controller.isExiting, isFalse);

      await tester.pump(const Duration(milliseconds: 500));
      expect(controller.isExiting, isTrue);
      expect(
        find.byKey(const ValueKey('blueverse-loading-wave-exit')),
        findsOneWidget,
      );

      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('Loading BLUEVERSE'), findsNothing);
      expect(controller.isVisible, isFalse);
    },
  );
}
