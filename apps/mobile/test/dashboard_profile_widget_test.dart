import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/data/models/auth_models.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/ui/account_screens.dart';
import 'package:mobile/ui/auth_view_model.dart';

import 'support/mobile_test_support.dart';

void main() {
  group('MOB-DASH coastal overview workflow', () {
    testWidgets(
      'MOB-DASH-001 shows the signed-in member and honest future-service previews',
      (tester) async {
        final user = mobileTestUser(fullName: 'Maya Coastal');
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthDashboardScreen(viewModel: viewModel),
            routes: {
              '/profile': (_) =>
                  const Scaffold(body: Text('Profile destination')),
            },
          ),
        );

        expect(find.text('Welcome, Maya.'), findsOneWidget);
        expect(find.text('Coastal discovery'), findsOneWidget);
        expect(find.text('Marine awareness'), findsOneWidget);
        expect(find.text('Shared stewardship'), findsOneWidget);
        expect(find.text('SOON'), findsNWidgets(3));
        expect(
          find.text(
            'Your account details today, with room for coastal experiences as they become available.',
          ),
          findsOneWidget,
        );

        await tester.tap(find.text('Edit your profile'));
        await tester.pumpAndSettle();
        expect(find.text('Profile destination'), findsOneWidget);
      },
    );
  });

  group('MOB-PROFILE profile and session workflow', () {
    testWidgets(
      'MOB-PROFILE-001 shows server-owned details and assigned roles',
      (tester) async {
        final user = mobileTestUser(
          fullName: 'Maya Coastal',
          email: 'maya@example.test',
          roles: const ['Visitor', 'Coastal Guide'],
        );
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        expect(find.text('Your profile, in your hands.'), findsOneWidget);
        expect(find.text('Maya Coastal'), findsOneWidget);
        expect(find.text('maya@example.test'), findsOneWidget);
        expect(find.text('Visitor'), findsOneWidget);
        expect(find.text('Coastal Guide'), findsOneWidget);
        expect(find.text('September 2026'), findsOneWidget);
        final emailField = _field('Email address');
        expect(tester.widget<TextField>(emailField).readOnly, isTrue);
      },
    );

    testWidgets(
      'MOB-PROFILE-002 blank profile name is rejected without a request',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.enterText(_field('Full name'), '   ');
        await tester.tap(find.widgetWithText(FilledButton, 'Save changes'));
        await tester.pump();

        expect(find.text('Enter your name.'), findsOneWidget);
        expect(repository.calls['updateCurrentUser'], isNull);
        expect(viewModel.user?.fullName, 'Coastal Member');
      },
    );

    testWidgets(
      'MOB-PROFILE-003 saves a trimmed name and reports the server result',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.enterText(_field('Full name'), '  Maya Coastal  ');
        await tester.tap(find.widgetWithText(FilledButton, 'Save changes'));
        await _finishAccountTransition(tester);

        expect(repository.calls['updateCurrentUser']?.single, {
          'fullName': 'Maya Coastal',
        });
        expect(viewModel.user?.fullName, 'Maya Coastal');
        expect(find.text('Your profile has been updated.'), findsOneWidget);
      },
    );

    testWidgets(
      'MOB-PROFILE-004 failed profile save keeps the original account and offers retry',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user)
          ..profileFailure = StateError('temporary network failure');
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.enterText(_field('Full name'), 'Maya Coastal');
        await tester.tap(find.widgetWithText(FilledButton, 'Save changes'));
        await _finishAccountTransition(tester);

        expect(viewModel.user?.fullName, 'Coastal Member');
        expect(
          find.text(
            'We couldn’t save your changes just now. Check your connection and try again.',
          ),
          findsOneWidget,
        );
        expect(repository.calls['updateCurrentUser'], hasLength(1));
      },
    );

    testWidgets(
      'MOB-PROFILE-005 password validation prevents a mismatched update',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.enterText(_field('Current password'), 'current-password');
        await tester.enterText(_field('New password'), 'new-password');
        await tester.enterText(
          _field('Confirm new password'),
          'different-password',
        );
        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Update password'),
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Update password'));
        await tester.pump();

        expect(find.text('Your new passwords do not match.'), findsOneWidget);
        expect(repository.calls['changePassword'], isNull);
      },
    );

    testWidgets(
      'MOB-PROFILE-006 valid password change sends both credentials and signs the active account out',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.enterText(_field('Current password'), 'current-password');
        await tester.enterText(_field('New password'), 'new-password');
        await tester.enterText(_field('Confirm new password'), 'new-password');
        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Update password'),
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Update password'));
        await _finishAccountTransition(tester);

        expect(repository.calls['changePassword']?.single, {
          'currentPassword': 'current-password',
          'newPassword': 'new-password',
        });
        expect(viewModel.user, isNull);
        expect(find.text('Sign in to see your BLUEVERSE.'), findsOneWidget);
      },
    );

    testWidgets(
      'MOB-PROFILE-007 remote session ending requires password verification and cancel is side-effect free',
      (tester) async {
        final user = mobileTestUser();
        final sessions = [
          mobileTestSession(),
          mobileTestSession(id: 'session-remote', isCurrent: false),
        ];
        final repository = FakeAuthRepository(user: user, sessions: sessions);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)
          ..user = user
          ..sessions = sessions;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.ensureVisible(find.text('Signed-in device'));
        await tester.tap(find.widgetWithText(TextButton, 'End'));
        await tester.pumpAndSettle();
        expect(find.text('End this device session?'), findsOneWidget);
        expect(
          find.text(
            'Enter your current password to confirm this session change.',
          ),
          findsOneWidget,
        );
        await tester.tap(find.text('Cancel'));
        await tester.pumpAndSettle();
        expect(repository.calls['revokeSession'], isNull);

        await tester.ensureVisible(find.text('Signed-in device'));
        await tester.tap(find.widgetWithText(TextButton, 'End'));
        await tester.pumpAndSettle();
        final password = find.descendant(
          of: find.byType(AlertDialog),
          matching: _field('Current password'),
        );
        await tester.enterText(password, 'verified-password');
        await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
        await _finishAccountTransition(tester);

        expect(repository.calls['revokeSession']?.single, {
          'sessionId': 'session-remote',
          'currentPassword': 'verified-password',
        });
        expect(viewModel.sessions.map((session) => session.id), [
          'session-current',
        ]);
      },
    );

    testWidgets(
      'MOB-PROFILE-008 current-device sign-out needs no remote-session password dialog',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)
          ..user = user
          ..sessions = [mobileTestSession()];
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.ensureVisible(find.text('This device'));
        await tester.tap(find.widgetWithText(TextButton, 'Sign out'));
        await _finishAccountTransition(tester);

        expect(find.text('End this device session?'), findsNothing);
        expect(repository.calls['revokeSession']?.single, {
          'sessionId': 'session-current',
          'currentPassword': '',
        });
        expect(viewModel.user, isNull);
      },
    );

    testWidgets(
      'MOB-PROFILE-009 sign-out-everywhere verifies the current password',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.ensureVisible(
          find.text('Sign out this account everywhere'),
        );
        await tester.tap(find.text('Sign out this account everywhere'));
        await tester.pumpAndSettle();
        expect(find.text('Sign out this account everywhere?'), findsOneWidget);
        final password = find.descendant(
          of: find.byType(AlertDialog),
          matching: _field('Current password'),
        );
        await tester.enterText(password, 'verified-password');
        await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
        await _finishAccountTransition(tester);

        expect(repository.calls['logoutAllDevices']?.single, {
          'currentPassword': 'verified-password',
        });
        expect(viewModel.user, isNull);
      },
    );

    testWidgets(
      'MOB-PROFILE-010 account deletion requires final confirmation',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Delete account'),
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Delete account'));
        await tester.pumpAndSettle();
        expect(find.text('Delete this account permanently?'), findsOneWidget);
        await tester.tap(find.text('Cancel'));
        await tester.pumpAndSettle();
        expect(repository.calls['deleteCurrentUser'], isNull);
        expect(viewModel.user, same(user));

        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Delete account'),
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Delete account'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Yes, delete account'));
        await _finishAccountTransition(tester);
        expect(repository.calls['deleteCurrentUser'], hasLength(1));
        expect(repository.calls['forgetAccountLocally']?.single, {
          'id': 'user-current',
        });
        expect(viewModel.user, isNull);
      },
    );

    testWidgets(
      'MOB-PROFILE-011 account switcher keeps the selected profile identity in sync',
      (tester) async {
        final current = mobileTestUser();
        final other = AuthAccountSummary(
          id: 'user-other',
          fullName: 'Other Member',
          email: 'other@example.test',
        );
        final repository = FakeAuthRepository(
          user: current,
          accounts: [
            AuthAccountSummary(
              id: current.id,
              fullName: current.fullName,
              email: current.email,
            ),
            other,
          ],
        );
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)
          ..user = current
          ..accounts = repository.accountsValue;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.tap(find.byTooltip('Switch BLUEVERSE account'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Other Member'));
        await _finishAccountTransition(tester);

        expect(repository.calls['switchAccount']?.single, {'id': 'user-other'});
        expect(viewModel.user?.id, 'user-other');
        expect(find.text('Other Member'), findsOneWidget);
      },
    );

    testWidgets(
      'MOB-PROFILE-012 empty session state offers a retry and reports a dependency failure',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user, sessions: const [])
          ..sessionFailure = const AuthApiException(
            503,
            'Sessions are temporarily unavailable.',
          );
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)
          ..user = user
          ..sessions = const [];
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        expect(
          find.text('No login sessions are available right now.'),
          findsOneWidget,
        );
        await tester.ensureVisible(find.text('Try again'));
        await tester.tap(find.text('Try again'));
        await _finishAccountTransition(tester);

        expect(repository.calls['sessions'], hasLength(1));
        expect(
          find.text('Sessions are temporarily unavailable.'),
          findsWidgets,
        );
        expect(viewModel.user?.id, 'user-current');
      },
    );

    testWidgets(
      'MOB-PROFILE-013 failed password change preserves the session and entered fields',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user)
          ..passwordFailure = const AuthApiException(
            400,
            'Current password is incorrect.',
          );
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.ensureVisible(_field('Current password'));
        await tester.enterText(_field('Current password'), 'wrong-password');
        await tester.ensureVisible(_field('New password'));
        await tester.enterText(_field('New password'), 'new-password');
        await tester.ensureVisible(_field('Confirm new password'));
        await tester.enterText(_field('Confirm new password'), 'new-password');
        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Update password'),
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Update password'));
        await _finishAccountTransition(tester);

        expect(viewModel.user, same(user));
        expect(find.text('Current password is incorrect.'), findsWidgets);
        expect(
          tester.widget<TextField>(_field('Current password')).controller!.text,
          'wrong-password',
        );
        expect(
          find.text('Password changed. Sign in again to continue.'),
          findsNothing,
        );
      },
    );

    testWidgets(
      'MOB-PROFILE-014 failed account deletion keeps the account and presents the error',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user)
          ..deleteFailure = const AuthApiException(
            409,
            'System-managed accounts cannot delete themselves.',
          );
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: viewModel)),
        );

        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Delete account'),
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Delete account'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Yes, delete account'));
        await _finishAccountTransition(tester);

        expect(viewModel.user, same(user));
        expect(
          find.text('System-managed accounts cannot delete themselves.'),
          findsWidgets,
        );
        expect(repository.calls['forgetAccountLocally'], isNull);
      },
    );

    testWidgets(
      'MOB-PROFILE-015 signed-out profile recovery offers sign-in and registration',
      (tester) async {
        final repository = FakeAuthRepository();
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthProfileScreen(viewModel: viewModel),
            routes: {
              '/signin': (_) =>
                  const Scaffold(body: Text('Sign-in destination')),
              '/signup': (_) =>
                  const Scaffold(body: Text('Registration destination')),
            },
          ),
        );
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();

        expect(find.text('Sign in to see your BLUEVERSE.'), findsOneWidget);
        await tester.tap(find.text('Sign in'));
        await tester.pumpAndSettle();
        expect(find.text('Sign-in destination'), findsOneWidget);
      },
    );
  });
}

Finder _field(String label) => find.byWidgetPredicate(
  (widget) => widget is TextField && widget.decoration?.labelText == label,
);

Future<void> _finishAccountTransition(WidgetTester tester) async {
  await tester.pump();
  await tester.pump(const Duration(seconds: 1));
  await tester.pumpAndSettle();
}
