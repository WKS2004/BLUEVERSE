import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/feedback/blueverse_error_screen.dart';

void main() {
  testWidgets('MOBILE-ERROR-001 unknown named routes show a recoverable 404', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: const Scaffold(body: Text('Coastal home')),
        onUnknownRoute: blueverseUnknownRoute,
      ),
    );
    const unknownRouteSegment = 'lost-cove';
    final unknownRoute = String.fromCharCode(47) + unknownRouteSegment;
    Navigator.of(tester.element(find.text('Coastal home')))
        .pushNamed(unknownRoute);
    await tester.pumpAndSettle();

    expect(find.text('404'), findsOneWidget);
    expect(find.text('This cove isn’t on our chart.'), findsOneWidget);
    expect(
      find.text(
        'We couldn’t find the page you were looking for. The coast is still here, so let’s find another way in.',
      ),
      findsOneWidget,
    );
    expect(find.text('Back to the coast'), findsOneWidget);
    await tester.tap(find.text('Back to the coast'));
    await tester.pumpAndSettle();
    expect(find.text('Coastal home'), findsOneWidget);
  });

  testWidgets('MOBILE-ERROR-002 the 500 page offers a safe retry path', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: const Scaffold(body: Text('Last good page')),
        routes: {'/500': (_) => const BlueverseErrorScreen.server()},
      ),
    );
    Navigator.of(tester.element(find.text('Last good page'))).pushNamed('/500');
    await tester.pumpAndSettle();

    expect(find.text('500'), findsOneWidget);
    expect(find.text('A current interrupted the journey.'), findsOneWidget);
    expect(
      find.textContaining('Your account and saved details are safe.'),
      findsOneWidget,
    );
    await tester.tap(find.text('Try again'));
    await tester.pumpAndSettle();
    expect(find.text('Last good page'), findsOneWidget);
  });

  testWidgets(
    'MOBILE-ERROR-003 unexpected Flutter build errors use the 500 page',
    (tester) async {
      final errorWidget = blueverseUnexpectedErrorWidget(
        FlutterErrorDetails(exception: StateError('private internal detail')),
      );
      await tester.pumpWidget(MaterialApp(home: errorWidget));

      expect(find.text('500'), findsOneWidget);
      expect(find.text('A current interrupted the journey.'), findsOneWidget);
      expect(find.text('private internal detail'), findsNothing);
    },
  );
}
