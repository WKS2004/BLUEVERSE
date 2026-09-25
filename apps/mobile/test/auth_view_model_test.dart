import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/data/models/auth_models.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/ui/auth_view_model.dart';

import 'support/mobile_test_support.dart';

void main() {
  group('MOB-VM session state', () {
    testWidgets(
      'MOB-VM-001 restores a saved account once and publishes session/account data',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);

        final firstRestore = viewModel.restore();
        final duplicateRestore = viewModel.restore();
        await Future.wait([firstRestore, duplicateRestore]);
        await tester.pump(const Duration(seconds: 1));

        expect(repository.calls['restore'], hasLength(1));
        expect(repository.calls['sessions'], hasLength(1));
        expect(repository.calls['accounts'], hasLength(1));
        expect(viewModel.hasRestoredSession, isTrue);
        expect(viewModel.user?.id, 'user-current');
        expect(viewModel.sessions.single.id, 'session-current');
        expect(viewModel.accounts.single.id, 'user-current');
      },
    );

    testWidgets(
      'MOB-VM-002 expired saved accounts are skipped until an available account restores',
      (tester) async {
        const noAccount = AuthAccountSummary(
          id: 'user-expired',
          fullName: 'Expired Member',
          email: 'expired@example.test',
        );
        const available = AuthAccountSummary(
          id: 'user-available',
          fullName: 'Available Member',
          email: 'available@example.test',
        );
        final repository = FakeAuthRepository(accounts: [noAccount, available])
          ..restoreFailure = const AuthApiException(401, 'Expired session.')
          ..switchFailures['user-expired'] = const AuthApiException(
            401,
            'This account needs you to sign in again.',
          );
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);

        await viewModel.restore();
        await tester.pump(const Duration(seconds: 1));

        expect(repository.calls['switchAccount']?.map((call) => call['id']), [
          'user-expired',
          'user-available',
        ]);
        expect(viewModel.user?.id, 'user-available');
        expect(viewModel.errorMessage, isNull);
        expect(viewModel.hasRestoredSession, isTrue);
      },
    );

    testWidgets(
      'MOB-VM-003 failed restore completes as signed out and records the dependency error',
      (tester) async {
        final repository = FakeAuthRepository()
          ..restoreFailure = StateError('unexpected dependency detail');
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);
        addTearDown(viewModel.dispose);

        await viewModel.restore();
        await tester.pump(const Duration(seconds: 1));

        expect(viewModel.hasRestoredSession, isTrue);
        expect(viewModel.user, isNull);
        expect(
          viewModel.errorMessage,
          contains('unexpected dependency detail'),
        );
      },
    );

    testWidgets(
      'MOB-VM-004 registration stays successful when session details fail to load',
      (tester) async {
        final repository = FakeAuthRepository()
          ..sessionFailure = const AuthApiException(
            503,
            'Sessions unavailable.',
          );
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository);

        final created = await viewModel.createAccount(
          fullName: 'Coastal Member',
          email: 'member@example.test',
          password: 'synthetic-password',
          rememberMe: false,
        );
        await tester.pump(const Duration(seconds: 1));

        expect(created, isTrue);
        expect(viewModel.user?.email, 'member@example.test');
        expect(
          viewModel.errorMessage,
          'Your account was created, but session details could not be loaded.',
        );
        expect(viewModel.sessions, isEmpty);
        expect(viewModel.isLoading, isFalse);
        viewModel.dispose();
      },
    );

    testWidgets(
      'MOB-VM-005 denied sign-in preserves the current account and completes loading',
      (tester) async {
        final current = mobileTestUser(id: 'user-current');
        final repository = FakeAuthRepository(user: current)
          ..loginFailure = const AuthApiException(401, 'Invalid credentials.');
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = current;
        addTearDown(viewModel.dispose);

        await viewModel.signIn(
          email: 'second@example.test',
          password: 'wrong-password',
          rememberMe: false,
        );
        await tester.pump(const Duration(seconds: 1));

        expect(viewModel.user, same(current));
        expect(viewModel.errorMessage, 'Invalid credentials.');
        expect(viewModel.isLoading, isFalse);
        expect(repository.calls['rememberAccount'], [
          {'id': 'user-current'},
        ]);
      },
    );

    testWidgets(
      'MOB-VM-006 logout dependency failure leaves the active account available for recovery',
      (tester) async {
        final user = mobileTestUser();
        final repository = FakeAuthRepository(user: user)
          ..logoutFailure = const AuthApiException(503, 'Gateway unavailable.');
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);

        await viewModel.logoutCurrentDevice();
        await tester.pump(const Duration(seconds: 1));

        expect(viewModel.user, same(user));
        expect(viewModel.errorMessage, 'Gateway unavailable.');
        expect(viewModel.isLoading, isFalse);
        expect(repository.calls['removeAccountFromDevice'], [
          {'id': 'user-current'},
        ]);
      },
    );
  });
}
