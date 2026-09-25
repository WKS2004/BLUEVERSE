import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/data/models/auth_models.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/ui/auth_registration_screen.dart';
import 'package:mobile/ui/auth_screens.dart';
import 'package:mobile/ui/auth_view_model.dart';
import 'package:mobile/ui/onboarding_screen.dart';

import 'support/mobile_test_support.dart';

void main() {
  group('MOB-HOME onboarding workflow', () {
    testWidgets(
      'MOB-HOME-001 exposes the first slide and valid navigation states',
      (tester) async {
        await tester.pumpWidget(
          mobileTestApp(home: const BlueverseOnboardingScreen()),
        );

        expect(find.text('Closer to the coast.'), findsOneWidget);
        expect(find.text('Back'), findsNothing);
        expect(find.text('Next'), findsOneWidget);
        expect(find.text('Skip'), findsOneWidget);
        expect(find.bySemanticsLabel('Slide 1 of 4'), findsOneWidget);
        expect(
          tester
              .widget<IconButton>(
                find.byWidgetPredicate(
                  (widget) =>
                      widget is IconButton &&
                      widget.tooltip == 'Previous slide',
                ),
              )
              .onPressed,
          isNull,
        );

        await tester.tap(find.text('Next'));
        await tester.pumpAndSettle();

        expect(find.text('Places made personal.'), findsOneWidget);
        expect(find.text('Back'), findsOneWidget);
        expect(find.text('Skip'), findsOneWidget);
        expect(find.bySemanticsLabel('Slide 2 of 4'), findsOneWidget);
        expect(
          tester.widget<IconButton>(_arrowButton('Previous slide')).onPressed,
          isNotNull,
        );
      },
    );

    testWidgets(
      'MOB-HOME-002 swipe, side arrows and Back reach adjacent slides',
      (tester) async {
        await tester.pumpWidget(
          mobileTestApp(home: const BlueverseOnboardingScreen()),
        );
        await tester.flingFrom(
          const Offset(790, 100),
          const Offset(-600, 0),
          1000,
        );
        await tester.pumpAndSettle();
        expect(find.text('Places made personal.'), findsOneWidget);

        await tester.tap(find.byTooltip('Next slide'));
        await tester.pumpAndSettle();
        expect(find.text('Stay in tune with the sea.'), findsOneWidget);

        await tester.tap(find.text('Back'));
        await tester.pumpAndSettle();
        expect(find.text('Places made personal.'), findsOneWidget);
      },
    );

    testWidgets(
      'MOB-HOME-003 Skip reaches the final account choices and routes to registration',
      (tester) async {
        await tester.pumpWidget(
          mobileTestApp(
            home: const BlueverseOnboardingScreen(),
            routes: {
              '/signup': (_) =>
                  const Scaffold(body: Text('Registration destination')),
              '/signin': (_) =>
                  const Scaffold(body: Text('Sign-in destination')),
            },
          ),
        );

        await tester.tap(find.text('Skip'));
        await tester.pumpAndSettle();
        expect(find.text('Find your place by the sea.'), findsOneWidget);
        expect(find.text('Create your account'), findsOneWidget);
        expect(
          find.text('I already have an account · Sign in'),
          findsOneWidget,
        );
        expect(find.text('Next'), findsNothing);
        expect(find.text('Skip'), findsNothing);

        await tester.tap(find.text('Create your account'));
        await tester.pumpAndSettle();
        expect(find.text('Registration destination'), findsOneWidget);
      },
    );

    testWidgets(
      'MOB-HOME-005 final sign-in choice opens the registered sign-in route',
      (tester) async {
        await tester.pumpWidget(
          mobileTestApp(
            home: const BlueverseOnboardingScreen(),
            routes: {
              '/signin': (_) =>
                  const Scaffold(body: Text('Sign-in destination')),
            },
          ),
        );

        await tester.tap(find.text('Skip'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('I already have an account · Sign in'));
        await tester.pumpAndSettle();

        expect(find.text('Sign-in destination'), findsOneWidget);
      },
    );

    testWidgets(
      'MOB-HOME-004 onboarding remains usable on a compact device viewport',
      (tester) async {
        await tester.binding.setSurfaceSize(const Size(320, 600));
        addTearDown(() async => tester.binding.setSurfaceSize(null));
        await tester.pumpWidget(
          mobileTestApp(home: const BlueverseOnboardingScreen()),
        );
        expect(tester.takeException(), isNull);

        await tester.tap(find.text('Skip'));
        await tester.pumpAndSettle();

        expect(find.text('Create your account'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  });

  group('MOB-AUTH-UI sign-in workflow', () {
    testWidgets(
      'MOB-AUTH-UI-001 validates missing credentials without making a request',
      (tester) async {
        final repository = FakeAuthRepository();
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthLoginScreen(viewModel: viewModel),
            routes: {
              '/signup': (_) =>
                  const Scaffold(body: Text('Registration destination')),
            },
          ),
        );

        await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
        await tester.pump();

        expect(find.text('Enter your email address.'), findsOneWidget);
        expect(find.text('Enter your password.'), findsOneWidget);
        expect(repository.calls['login'], isNull);
      },
    );

    testWidgets(
      'MOB-AUTH-UI-002 rejected credentials show a recoverable error and keep the form',
      (tester) async {
        final repository = FakeAuthRepository()
          ..loginFailure = const AuthApiException(401, 'Invalid credentials.');
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthLoginScreen(viewModel: viewModel)),
        );
        await tester.enterText(
          find.byType(TextFormField).at(0),
          'member@example.test',
        );
        await tester.enterText(
          find.byType(TextFormField).at(1),
          'wrong-password',
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
        await tester.pump();
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();

        expect(find.text('Invalid credentials.'), findsOneWidget);
        expect(find.text('Welcome back to BLUEVERSE'), findsOneWidget);
        expect(viewModel.user, isNull);
        expect(repository.calls['login'], hasLength(1));
      },
    );

    testWidgets(
      'MOB-AUTH-UI-003 valid sign-in keeps the selected 30-day option and opens Dashboard',
      (tester) async {
        final repository = FakeAuthRepository();
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthLoginScreen(viewModel: viewModel),
            routes: {
              '/dashboard': (_) =>
                  const Scaffold(body: Text('Dashboard destination')),
            },
          ),
        );
        await tester.enterText(
          find.byType(TextFormField).at(0),
          'member@example.test',
        );
        await tester.enterText(
          find.byType(TextFormField).at(1),
          'synthetic-password',
        );
        await tester.tap(find.text('Keep me signed in for 30 days'));
        await tester.pump();
        await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
        await tester.pump();
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();

        expect(find.text('Dashboard destination'), findsOneWidget);
        expect(repository.calls['login']?.single, {
          'email': 'member@example.test',
          'password': 'synthetic-password',
          'rememberMe': true,
        });
        expect(viewModel.user?.email, 'member@example.test');
      },
    );

    testWidgets(
      'MOB-AUTH-UI-004 account limit disables adding a sixth signed-in account',
      (tester) async {
        final current = mobileTestUser();
        final accounts = List.generate(
          5,
          (index) => AuthAccountSummary(
            id: 'user-$index',
            fullName: 'Member $index',
            email: 'member$index@example.test',
          ),
        );
        final repository = FakeAuthRepository(
          user: current,
          accounts: accounts,
        );
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)
          ..user = current
          ..accounts = accounts;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthLoginScreen(viewModel: viewModel),
            routes: {
              '/signup': (_) =>
                  const Scaffold(body: Text('Registration destination')),
            },
          ),
        );

        expect(find.text('Accounts on this device (5/5)'), findsOneWidget);
        final loginToAnother = tester.widget<FilledButton>(
          find.widgetWithText(FilledButton, 'Account limit reached'),
        );
        expect(loginToAnother.onPressed, isNull);
        final registerAnother = tester.widget<OutlinedButton>(
          find.widgetWithText(OutlinedButton, 'Create another account'),
        );
        expect(registerAnother.onPressed, isNull);
        expect(repository.calls['login'], isNull);
      },
    );
  });

  group('MOB-REGISTER account creation workflow', () {
    testWidgets(
      'MOB-REGISTER-001 reports each invalid field and does not submit',
      (tester) async {
        final repository = FakeAuthRepository();
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthRegistrationScreen(viewModel: viewModel)),
        );

        final createButton = find.widgetWithText(
          FilledButton,
          'Create account',
        );
        await tester.ensureVisible(createButton);
        await tester.tap(createButton);
        await tester.pump();

        expect(find.text('Enter your name.'), findsOneWidget);
        expect(find.text('Enter your email.'), findsOneWidget);
        expect(find.text('Use at least 8 characters.'), findsNWidgets(2));
        expect(find.text('Confirm your password.'), findsOneWidget);
        expect(repository.calls['register'], isNull);
      },
    );

    testWidgets(
      'MOB-REGISTER-002 enforces email, password length and confirmation before submit',
      (tester) async {
        final repository = FakeAuthRepository();
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthRegistrationScreen(viewModel: viewModel)),
        );
        await tester.enterText(
          find.byType(TextFormField).at(0),
          'Coastal Member',
        );
        await tester.enterText(
          find.byType(TextFormField).at(1),
          'not-an-email',
        );
        await tester.enterText(find.byType(TextFormField).at(2), 'short');
        await tester.enterText(find.byType(TextFormField).at(3), 'different');
        final createButton = find.widgetWithText(
          FilledButton,
          'Create account',
        );
        await tester.ensureVisible(createButton);
        await tester.tap(createButton);
        await tester.pump();

        expect(find.text('Enter a valid email address.'), findsOneWidget);
        expect(find.text('Use at least 8 characters.'), findsNWidgets(2));
        expect(find.text('Your passwords do not match.'), findsOneWidget);
        expect(repository.calls['register'], isNull);
      },
    );

    testWidgets(
      'MOB-REGISTER-003 successful registration opens Profile with the submitted account details',
      (tester) async {
        final repository = FakeAuthRepository();
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthRegistrationScreen(viewModel: viewModel),
            routes: {
              '/profile': (_) =>
                  const Scaffold(body: Text('Profile destination')),
            },
          ),
        );
        await tester.enterText(
          find.byType(TextFormField).at(0),
          'Coastal Member',
        );
        await tester.enterText(
          find.byType(TextFormField).at(1),
          'member@example.test',
        );
        await tester.enterText(
          find.byType(TextFormField).at(2),
          'synthetic-password',
        );
        await tester.ensureVisible(find.byType(TextFormField).at(3));
        await tester.enterText(
          find.byType(TextFormField).at(3),
          'synthetic-password',
        );
        await tester.tap(find.text('Keep me signed in for 30 days'));
        await tester.pump();
        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Create account'),
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
        await tester.pump();
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();

        expect(find.text('Profile destination'), findsOneWidget);
        expect(repository.calls['register']?.single, {
          'fullName': 'Coastal Member',
          'email': 'member@example.test',
          'password': 'synthetic-password',
          'rememberMe': true,
        });
        expect(viewModel.user?.fullName, 'Coastal Member');
      },
    );

    testWidgets(
      'MOB-REGISTER-004 account device limit prevents a sixth sign-in',
      (tester) async {
        final accounts = List.generate(
          5,
          (index) => AuthAccountSummary(
            id: 'user-$index',
            fullName: 'Member $index',
            email: 'member$index@example.test',
          ),
        );
        final repository = FakeAuthRepository(accounts: accounts);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)
          ..accounts = accounts;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthRegistrationScreen(viewModel: viewModel)),
        );

        expect(
          find.textContaining('already has five signed-in accounts'),
          findsOneWidget,
        );
        final createButton = tester.widget<FilledButton>(
          find.widgetWithText(FilledButton, 'Create account'),
        );
        expect(createButton.onPressed, isNull);
        expect(repository.calls['register'], isNull);
      },
    );
  });
}

Finder _arrowButton(String label) => find.byWidgetPredicate(
  (widget) => widget is IconButton && widget.tooltip == label,
);
