import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/feedback/blueverse_loading_screen.dart';
import 'package:mobile/ui/feedback/loading_screen_controller.dart';

void main() {
  testWidgets(
    'MOBILE-AUTH-LOADING-001 keeps a quick sign-in transition visible',
    (tester) async {
      final controller = LoadingScreenController();
      addTearDown(controller.dispose);

      final finish = controller.begin(
        message: const LoadingScreenMessage.signIn(),
      );
      finish();

      await tester.pump(const Duration(milliseconds: 200));
      expect(controller.isVisible, isTrue);
      expect(controller.message.title, 'Signing you in');

      await tester.pump(const Duration(milliseconds: 700));
      expect(controller.isVisible, isFalse);
    },
  );

  testWidgets(
    'MOBILE-AUTH-LOADING-002 gives each account transition a visible secure screen',
    (tester) async {
      final controller = LoadingScreenController();
      addTearDown(controller.dispose);
      await tester.pumpWidget(_harness(controller));

      const messages = [
        LoadingScreenMessage.signIn(),
        LoadingScreenMessage.registration(),
        LoadingScreenMessage.switchAccount(),
        LoadingScreenMessage.password(),
        LoadingScreenMessage.signOut(),
        LoadingScreenMessage.endSession(),
        LoadingScreenMessage.deleteAccount(),
      ];

      for (final message in messages) {
        final finish = controller.begin(message: message);
        await tester.pump();
        expect(find.text(message.title), findsOneWidget);
        expect(find.text('SECURE ACCOUNT TRANSITION'), findsOneWidget);

        finish();
        await tester.pump(const Duration(milliseconds: 200));
        expect(controller.isVisible, isTrue);

        await tester.pump(const Duration(milliseconds: 700));
        expect(controller.isVisible, isFalse);
      }
    },
  );

  testWidgets(
    'MOBILE-AUTH-LOADING-003 keeps the transition message through initial page data',
    (tester) async {
      final controller = LoadingScreenController();
      addTearDown(controller.dispose);
      await tester.pumpWidget(_harness(controller));

      final finishTransition = controller.begin(
        message: const LoadingScreenMessage.registration(),
      );
      await tester.pump();
      finishTransition();

      final finishPageData = controller.begin(
        message: const LoadingScreenMessage.sessions(),
      );
      await tester.pump();
      expect(find.text('Preparing your account'), findsOneWidget);
      expect(find.text('Checking your sessions'), findsNothing);
      expect(find.text('SECURE ACCOUNT TRANSITION'), findsOneWidget);

      finishPageData();
      await tester.pump(const Duration(milliseconds: 200));
      expect(controller.isVisible, isTrue);
      expect(find.text('Preparing your account'), findsOneWidget);

      await tester.pump(const Duration(milliseconds: 700));
      expect(controller.isVisible, isFalse);
    },
  );
}

Widget _harness(LoadingScreenController controller) {
  return MaterialApp(
    builder: (context, child) => ListenableBuilder(
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
    home: const Scaffold(body: Center(child: Text('Account profile'))),
  );
}
