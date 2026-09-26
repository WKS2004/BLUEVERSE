import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/account_screens.dart';
import 'package:mobile/ui/auth_admin_screen.dart';
import 'package:mobile/ui/auth_view_model.dart';

import 'support/mobile_test_support.dart';

void main() {
  group('MOB-ADMIN administration navigation and authorization', () {
    testWidgets(
      'MOB-ADMIN-UI-001 administration navigation appears only for a read grant',
      (tester) async {
        final deniedUser = mobileTestUser();
        final deniedRepository = FakeAuthRepository(user: deniedUser);
        addTearDown(deniedRepository.close);
        final deniedModel = AuthViewModel(repository: deniedRepository)
          ..user = deniedUser;
        addTearDown(deniedModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: deniedModel)),
        );

        expect(find.byTooltip('Administration'), findsNothing);

        final permittedUser = mobileTestUser(
          permissions: const ['auth.role.read'],
        );
        final permittedRepository = FakeAuthRepository(user: permittedUser);
        addTearDown(permittedRepository.close);
        final permittedModel = AuthViewModel(repository: permittedRepository)
          ..user = permittedUser;
        await tester.pumpWidget(
          mobileTestApp(home: AuthProfileScreen(viewModel: permittedModel)),
        );
        addTearDown(permittedModel.dispose);

        expect(find.byTooltip('Administration'), findsOneWidget);
        await tester.tap(find.byTooltip('Administration'));
        await tester.pumpAndSettle();
        expect(find.text('Roles'), findsOneWidget);
        expect(find.text('Permissions'), findsNothing);
        expect(find.text('User accounts'), findsNothing);
      },
    );

    testWidgets(
      'MOB-ADMIN-UI-002 denied role access does not fetch role data',
      (tester) async {
        final user = mobileTestUser(permissions: const ['auth.user.read']);
        final repository = FakeAuthRepository(user: user)
          ..rolesValue = [
            {
              'id': 'role-1',
              'name': 'Coastal Editor',
              'description': 'Manage coastal content.',
              'isSystemRole': false,
              'permissions': <String>[],
            },
          ];
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthAdminScreen(
              viewModel: viewModel,
              section: AuthAdminSection.roles,
            ),
          ),
        );
        await tester.pump(const Duration(seconds: 1));

        expect(
          find.text('Your current account does not have access to this page.'),
          findsOneWidget,
        );
        expect(find.text('Coastal Editor'), findsNothing);
        expect(repository.calls['adminRoles'], isNull);
      },
    );

    testWidgets(
      'MOB-ADMIN-UI-003 role assignment controls require all read and update grants and protect system roles',
      (tester) async {
        final user = mobileTestUser(
          permissions: const [
            'auth.role.read',
            'auth.role.update',
            'auth.role.delete',
            'auth.permission.read',
          ],
        );
        final repository = FakeAuthRepository(user: user)
          ..permissionsValue = [
            {'code': 'auth.user.read', 'description': 'Read account records.'},
            {'code': 'auth.role.read', 'description': 'Read role records.'},
          ]
          ..rolesValue = [
            {
              'id': 'role-system',
              'name': 'Administrator',
              'description': 'System role.',
              'isSystemRole': true,
              'permissions': ['auth.role.read'],
            },
            {
              'id': 'role-guide',
              'name': 'Coastal Guide',
              'description': 'Support coastal visitors.',
              'isSystemRole': false,
              'permissions': ['auth.user.read'],
            },
          ];
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthAdminScreen(
              viewModel: viewModel,
              section: AuthAdminSection.roles,
            ),
          ),
        );
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();

        expect(repository.calls['adminRoles'], hasLength(1));
        expect(repository.calls['adminPermissions'], hasLength(1));
        await tester.tap(find.text('Administrator'));
        await tester.pumpAndSettle();
        expect(
          find.text(
            'System role names, permissions and deletion are deployment-controlled.',
          ),
          findsOneWidget,
        );
        expect(find.text('Edit details'), findsNothing);
        expect(find.text('Delete role'), findsNothing);

        await tester.tap(find.text('Coastal Guide'));
        await tester.pumpAndSettle();
        expect(find.text('Edit details'), findsOneWidget);
        expect(find.text('Delete role'), findsOneWidget);
        expect(
          find.widgetWithText(CheckboxListTile, 'auth.user.read'),
          findsOneWidget,
        );

        final roleReadCheckbox = find.widgetWithText(
          CheckboxListTile,
          'auth.role.read',
        );
        await tester.ensureVisible(roleReadCheckbox);
        await tester.pumpAndSettle();
        await tester.tap(roleReadCheckbox);
        await tester.pump();
        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Save permissions'),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'Save permissions'));
        await _finishAccountTransition(tester);

        expect(repository.calls['adminSetRolePermissions']?.single, {
          'id': 'role-guide',
          'permissionCodes': ['auth.user.read', 'auth.role.read'],
        });
      },
    );

    testWidgets(
      'MOB-ADMIN-UI-004 user changes skip the active account and lock system-role operations',
      (tester) async {
        final user = mobileTestUser(
          permissions: const [
            'auth.user.read',
            'auth.user.update',
            'auth.user.delete',
            'auth.role.read',
          ],
        );
        final repository = FakeAuthRepository(user: user)
          ..rolesValue = [
            {
              'id': 'role-admin',
              'name': 'Administrator',
              'description': 'System access.',
              'isSystemRole': true,
              'permissions': <String>[],
            },
            {
              'id': 'role-guide',
              'name': 'Guide',
              'description': 'Guide access.',
              'isSystemRole': false,
              'permissions': <String>[],
            },
          ]
          ..adminUsersValue = [
            {
              'id': 'user-current',
              'fullName': 'Coastal Member',
              'email': 'member@example.test',
              'isActive': true,
              'roles': ['Visitor'],
            },
            {
              'id': 'user-system',
              'fullName': 'Protected Operator',
              'email': 'operator@example.test',
              'isActive': true,
              'roles': ['Administrator'],
            },
            {
              'id': 'user-other',
              'fullName': 'Other Member',
              'email': 'other@example.test',
              'isActive': true,
              'roles': ['Guide'],
            },
          ];
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthAdminScreen(
              viewModel: viewModel,
              section: AuthAdminSection.users,
            ),
          ),
        );
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();

        await tester.tap(find.text('Coastal Member'));
        await tester.pumpAndSettle();
        expect(find.text('Edit account'), findsNothing);
        expect(find.text('Delete account'), findsNothing);

        await tester.tap(find.text('Protected Operator'));
        await tester.pumpAndSettle();
        expect(find.text('System account'), findsOneWidget);
        expect(find.text('Edit account'), findsOneWidget);
        expect(find.text('Delete account'), findsNothing);
        expect(
          tester
              .widget<TextButton>(
                find.widgetWithText(TextButton, 'System account'),
              )
              .onPressed,
          isNull,
        );

        await tester.tap(find.text('Other Member'));
        await tester.pumpAndSettle();
        final otherMemberCard = find.ancestor(
          of: find.text('Other Member'),
          matching: find.byType(Card),
        );
        expect(
          find.descendant(
            of: otherMemberCard,
            matching: find.widgetWithText(OutlinedButton, 'Edit account'),
          ),
          findsOneWidget,
        );
        expect(
          find.descendant(
            of: otherMemberCard,
            matching: find.widgetWithText(TextButton, 'Delete account'),
          ),
          findsOneWidget,
        );
        expect(find.text('Assigned roles'), findsOneWidget);
        expect(
          find.widgetWithText(CheckboxListTile, 'Administrator · system'),
          findsOneWidget,
        );
        final protectedSystemRole = tester.widget<CheckboxListTile>(
          find.widgetWithText(CheckboxListTile, 'Administrator · system'),
        );
        expect(protectedSystemRole.onChanged, isNull);
      },
    );

    testWidgets(
      'MOB-ADMIN-UI-005 role creation validates the name and reports successful changes',
      (tester) async {
        final user = mobileTestUser(
          permissions: const ['auth.role.read', 'auth.role.create'],
        );
        final repository = FakeAuthRepository(user: user);
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthAdminScreen(
              viewModel: viewModel,
              section: AuthAdminSection.roles,
            ),
          ),
        );
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();

        await tester.tap(find.text('Create role'));
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'Save'));
        await tester.pumpAndSettle();
        expect(find.text('Enter a role name.'), findsOneWidget);
        expect(repository.calls['adminCreateRole'], isNull);

        await tester.enterText(find.byType(TextField).at(0), 'Coastal Guide');
        await tester.enterText(
          find.byType(TextField).at(1),
          'Support coastal visitors.',
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Save'));
        await _finishAccountTransition(tester);

        expect(repository.calls['adminCreateRole']?.single, {
          'name': 'Coastal Guide',
          'description': 'Support coastal visitors.',
        });
        expect(
          find.text('Role created with no permissions assigned.'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'MOB-ADMIN-UI-006 account creation validates required fields and assigns an optional role',
      (tester) async {
        final user = mobileTestUser(
          permissions: const [
            'auth.user.read',
            'auth.user.create',
            'auth.role.read',
          ],
        );
        final repository = FakeAuthRepository(user: user)
          ..rolesValue = [
            {
              'id': 'role-guide',
              'name': 'Guide',
              'description': 'Guide access.',
              'isSystemRole': false,
              'permissions': <String>[],
            },
          ];
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthAdminScreen(
              viewModel: viewModel,
              section: AuthAdminSection.users,
            ),
          ),
        );
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();

        await tester.tap(find.text('Create account'));
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'Create'));
        await tester.pump();
        expect(find.text('Enter a full name.'), findsOneWidget);
        expect(find.text('Enter an email address.'), findsOneWidget);
        expect(find.text('Use at least 8 characters.'), findsOneWidget);
        expect(repository.calls['adminCreateUser'], isNull);

        await tester.enterText(_adminField('Full name'), 'New Coastal Member');
        await tester.enterText(_adminField('Email'), 'not-an-email');
        await tester.enterText(
          _adminField('Temporary password (8+ characters)'),
          'synthetic-password',
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Create'));
        await tester.pump();
        expect(find.text('Enter a valid email address.'), findsOneWidget);
        expect(repository.calls['adminCreateUser'], isNull);

        await tester.enterText(_adminField('Email'), 'new@example.test');
        await tester.enterText(
          _adminField('Temporary password (8+ characters)'),
          'short',
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Create'));
        await tester.pumpAndSettle();
        expect(find.text('Use at least 8 characters.'), findsOneWidget);
        expect(repository.calls['adminCreateUser'], isNull);

        await tester.enterText(
          _adminField('Temporary password (8+ characters)'),
          'synthetic-password',
        );
        await tester.tap(find.widgetWithText(CheckboxListTile, 'Guide'));
        await tester.tap(find.widgetWithText(FilledButton, 'Create'));
        await _finishAccountTransition(tester);

        expect(repository.calls['adminCreateUser']?.single, {
          'email': 'new@example.test',
          'password': 'synthetic-password',
          'fullName': 'New Coastal Member',
          'roleNames': ['Guide'],
        });
        expect(find.text('Account created.'), findsOneWidget);
      },
    );

    testWidgets(
      'MOB-ADMIN-UI-007 authorized account edit submits active state and password',
      (tester) async {
        final user = mobileTestUser(
          permissions: const ['auth.user.read', 'auth.user.update'],
        );
        final repository = FakeAuthRepository(user: user)
          ..adminUsersValue = [
            {
              'id': 'user-other',
              'fullName': 'Other Member',
              'email': 'other@example.test',
              'isActive': true,
              'roles': <String>[],
            },
          ];
        addTearDown(repository.close);
        final viewModel = AuthViewModel(repository: repository)..user = user;
        addTearDown(viewModel.dispose);
        await tester.pumpWidget(
          mobileTestApp(
            home: AuthAdminScreen(
              viewModel: viewModel,
              section: AuthAdminSection.users,
            ),
          ),
        );
        await tester.pump(const Duration(seconds: 1));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Other Member'));
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(OutlinedButton, 'Edit account'));
        await tester.pumpAndSettle();

        await tester.enterText(_adminField('Full name'), '   ');
        await tester.enterText(_adminField('Email'), 'not-an-email');
        await tester.enterText(_adminField('New password (optional)'), 'short');
        await tester.tap(find.widgetWithText(FilledButton, 'Save'));
        await tester.pump();
        expect(find.text('Enter a full name.'), findsOneWidget);
        expect(find.text('Enter a valid email address.'), findsOneWidget);
        expect(find.text('Use at least 8 characters.'), findsOneWidget);
        expect(repository.calls['adminUpdateUser'], isNull);

        await tester.enterText(_adminField('Full name'), 'Updated Member');
        await tester.enterText(_adminField('Email'), 'updated@example.test');
        await tester.enterText(
          _adminField('New password (optional)'),
          'synthetic-password',
        );
        await tester.tap(find.text('Account is active'));
        await tester.tap(find.widgetWithText(FilledButton, 'Save'));
        await _finishAccountTransition(tester);

        expect(repository.calls['adminUpdateUser']?.single, {
          'id': 'user-other',
          'email': 'updated@example.test',
          'fullName': 'Updated Member',
          'isActive': false,
          'newPassword': 'synthetic-password',
        });
        expect(find.text('Account updated.'), findsOneWidget);
      },
    );
  });
}

Finder _adminField(String label) => find.byWidgetPredicate(
  (widget) => widget is TextField && widget.decoration?.labelText == label,
);

Future<void> _finishAccountTransition(WidgetTester tester) async {
  await tester.pump();
  await tester.pump(const Duration(seconds: 1));
  await tester.pumpAndSettle();
  await tester.pump(const Duration(milliseconds: 200));
  await tester.pumpAndSettle();
}
