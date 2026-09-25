import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/models/auth_models.dart';
import 'package:mobile/data/repositories/auth_repository.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/auth_credential_store.dart';

class MemoryAuthCredentialStore implements AuthCredentialStore {
  final values = <String, String>{};

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async {
    values[key] = value;
  }

  @override
  Future<void> delete(String key) async {
    values.remove(key);
  }
}

AuthUser mobileTestUser({
  String id = 'user-current',
  String fullName = 'Coastal Member',
  String email = 'member@example.test',
  List<String> roles = const ['User'],
  List<String> permissions = const [],
}) => AuthUser(
  id: id,
  fullName: fullName,
  email: email,
  isActive: true,
  createdAt: DateTime.utc(2026, 9, 1),
  roles: roles,
  permissions: permissions,
);

AuthSession mobileTestSession({
  String id = 'session-current',
  bool isCurrent = true,
  bool rememberMe = false,
}) => AuthSession(
  id: id,
  deviceId: 'device-$id',
  createdAt: DateTime.utc(2026, 9, 20),
  lastSeenAt: DateTime.utc(2026, 9, 24, 12),
  expiresAt: DateTime.utc(2026, 9, 25),
  rememberMe: rememberMe,
  isCurrent: isCurrent,
);

Map<String, dynamic> mobileTestUserJson({
  String id = 'user-current',
  String fullName = 'Coastal Member',
  String email = 'member@example.test',
  List<String> roles = const ['User'],
  List<String> permissions = const [],
}) => {
  'id': id,
  'email': email,
  'fullName': fullName,
  'isActive': true,
  'createdAt': '2026-09-01T00:00:00Z',
  'roles': roles,
  'permissions': permissions,
};

Map<String, dynamic> mobileTestAuthResponse({
  String id = 'user-current',
  String fullName = 'Coastal Member',
  String email = 'member@example.test',
  String token = 'test-access-token',
  String refreshToken = 'test-refresh-token',
}) => {
  'token': token,
  'expiresAt': '2026-09-25T12:00:00Z',
  'deviceId': 'test-device',
  'deviceKey': 'test-device-proof',
  'refreshToken': refreshToken,
  'sessionExpiresAt': '2026-10-25T12:00:00Z',
  'rememberMe': false,
  'user': mobileTestUserJson(id: id, fullName: fullName, email: email),
};

/// Repository fake for view-model and widget tests. API-boundary tests use
/// [AuthApiService] directly with MockClient so these tests can focus on UI
/// outcomes without bypassing the real public API adapter in production.
class FakeAuthRepository extends AuthRepository {
  factory FakeAuthRepository({
    AuthUser? user,
    List<AuthSession>? sessions,
    List<AuthAccountSummary>? accounts,
  }) {
    final client = MockClient((_) async => http.Response('{}', 500));
    final storage = MemoryAuthCredentialStore();
    return FakeAuthRepository._(
      AuthApiService(client: client, storage: storage),
      client: client,
      initialUser: user,
      initialSessions: sessions,
      initialAccounts: accounts,
    );
  }

  FakeAuthRepository._(
    AuthApiService apiService, {
    required this.client,
    AuthUser? initialUser,
    List<AuthSession>? initialSessions,
    List<AuthAccountSummary>? initialAccounts,
  }) : super(apiService: apiService) {
    user = initialUser;
    if (initialUser != null) usersById[initialUser.id] = initialUser;
    sessionsValue = initialSessions ?? [mobileTestSession()];
    accountsValue =
        initialAccounts ??
        (initialUser == null
            ? []
            : [
                AuthAccountSummary(
                  id: initialUser.id,
                  fullName: initialUser.fullName,
                  email: initialUser.email,
                ),
              ]);
    for (final account in accountsValue) {
      usersById.putIfAbsent(
        account.id,
        () => mobileTestUser(
          id: account.id,
          fullName: account.fullName,
          email: account.email,
        ),
      );
    }
  }

  final http.Client client;
  AuthUser? user;
  final usersById = <String, AuthUser>{};
  List<AuthSession> sessionsValue = [];
  List<AuthAccountSummary> accountsValue = [];
  List<Map<String, dynamic>> permissionsValue = [];
  List<Map<String, dynamic>> rolesValue = [];
  List<Map<String, dynamic>> adminUsersValue = [];
  final events = <String>[];
  final calls = <String, List<Map<String, dynamic>>>{};
  final switchFailures = <String, Object>{};
  Object? loginFailure;
  Object? registrationFailure;
  Object? restoreFailure;
  Object? profileFailure;
  Object? passwordFailure;
  Object? sessionFailure;
  Object? logoutFailure;
  Object? deleteFailure;
  Object? adminFailure;

  void close() => client.close();

  void record(String name, [Map<String, dynamic> arguments = const {}]) {
    events.add(name);
    calls.putIfAbsent(name, () => []).add(arguments);
  }

  Object? _take(Object? failure) => failure;

  Never _throw(Object error) =>
      Error.throwWithStackTrace(error, StackTrace.current);

  @override
  Future<AuthUser> login({
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    record('login', {
      'email': email,
      'password': password,
      'rememberMe': rememberMe,
    });
    final failure = _take(loginFailure);
    if (failure != null) _throw(failure);
    final result = mobileTestUser(email: email);
    user = result;
    usersById[result.id] = result;
    _remember(result);
    return result;
  }

  @override
  Future<AuthUser> register({
    required String fullName,
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    record('register', {
      'fullName': fullName,
      'email': email,
      'password': password,
      'rememberMe': rememberMe,
    });
    final failure = _take(registrationFailure);
    if (failure != null) _throw(failure);
    final result = mobileTestUser(fullName: fullName, email: email);
    user = result;
    usersById[result.id] = result;
    _remember(result);
    return result;
  }

  @override
  Future<AuthUser> restore() async {
    record('restore');
    final failure = _take(restoreFailure);
    if (failure != null) _throw(failure);
    final result = user;
    if (result == null) {
      throw const AuthApiException(401, 'No active account.');
    }
    return result;
  }

  @override
  Future<List<AuthSession>> sessions() async {
    record('sessions');
    final failure = _take(sessionFailure);
    if (failure != null) _throw(failure);
    return List.unmodifiable(sessionsValue);
  }

  @override
  Future<List<AuthAccountSummary>> accounts() async {
    record('accounts');
    return List.unmodifiable(accountsValue);
  }

  @override
  Future<void> rememberAccount(AuthUser account) async {
    record('rememberAccount', {'id': account.id});
    _remember(account);
  }

  @override
  Future<AuthUser> switchAccount(String userId) async {
    record('switchAccount', {'id': userId});
    final failure = switchFailures[userId];
    if (failure != null) _throw(failure);
    final next = usersById[userId] ?? mobileTestUser(id: userId);
    user = next;
    usersById[userId] = next;
    _remember(next);
    return next;
  }

  @override
  Future<void> removeAccountFromDevice(String userId) async {
    record('removeAccountFromDevice', {'id': userId});
    final failure = _take(logoutFailure);
    if (failure != null) _throw(failure);
    accountsValue = accountsValue
        .where((account) => account.id != userId)
        .toList();
  }

  @override
  Future<AuthUser> updateCurrentUser({required String fullName}) async {
    record('updateCurrentUser', {'fullName': fullName});
    final failure = _take(profileFailure);
    if (failure != null) _throw(failure);
    final current = user ?? mobileTestUser();
    final updated = mobileTestUser(
      id: current.id,
      fullName: fullName,
      email: current.email,
      roles: current.roles,
      permissions: current.permissions,
    );
    user = updated;
    usersById[updated.id] = updated;
    _remember(updated);
    return updated;
  }

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    record('changePassword', {
      'currentPassword': currentPassword,
      'newPassword': newPassword,
    });
    final failure = _take(passwordFailure);
    if (failure != null) _throw(failure);
    final currentId = user?.id;
    if (currentId != null) {
      accountsValue = accountsValue
          .where((account) => account.id != currentId)
          .toList();
    }
  }

  @override
  Future<void> revokeSession(
    String sessionId, {
    String currentPassword = '',
  }) async {
    record('revokeSession', {
      'sessionId': sessionId,
      'currentPassword': currentPassword,
    });
    final failure = _take(sessionFailure);
    if (failure != null) _throw(failure);
  }

  @override
  Future<void> forgetAccountLocally(String userId) async {
    record('forgetAccountLocally', {'id': userId});
    accountsValue = accountsValue
        .where((account) => account.id != userId)
        .toList();
  }

  @override
  Future<void> logoutAllDevices(String currentPassword) async {
    record('logoutAllDevices', {'currentPassword': currentPassword});
    final failure = _take(logoutFailure);
    if (failure != null) _throw(failure);
    final currentId = user?.id;
    if (currentId != null) {
      accountsValue = accountsValue
          .where((account) => account.id != currentId)
          .toList();
    }
  }

  @override
  Future<void> deleteCurrentUser() async {
    record('deleteCurrentUser');
    final failure = _take(deleteFailure);
    if (failure != null) _throw(failure);
  }

  @override
  Future<List<Map<String, dynamic>>> adminPermissions() async {
    record('adminPermissions');
    final failure = _take(adminFailure);
    if (failure != null) _throw(failure);
    return List.unmodifiable(permissionsValue);
  }

  @override
  Future<List<Map<String, dynamic>>> adminRoles() async {
    record('adminRoles');
    final failure = _take(adminFailure);
    if (failure != null) _throw(failure);
    return List.unmodifiable(rolesValue);
  }

  @override
  Future<Map<String, dynamic>> adminCreateRole({
    required String name,
    required String description,
  }) async {
    record('adminCreateRole', {'name': name, 'description': description});
    _checkAdminFailure();
    return {'id': 'role-new', 'name': name, 'description': description};
  }

  @override
  Future<Map<String, dynamic>> adminUpdateRole({
    required String id,
    required String name,
    required String description,
  }) async {
    record('adminUpdateRole', {
      'id': id,
      'name': name,
      'description': description,
    });
    _checkAdminFailure();
    return {'id': id, 'name': name, 'description': description};
  }

  @override
  Future<Map<String, dynamic>> adminSetRolePermissions({
    required String id,
    required List<String> permissionCodes,
  }) async {
    record('adminSetRolePermissions', {
      'id': id,
      'permissionCodes': permissionCodes,
    });
    _checkAdminFailure();
    return {'id': id, 'permissions': permissionCodes};
  }

  @override
  Future<void> adminDeleteRole(String id) async {
    record('adminDeleteRole', {'id': id});
    _checkAdminFailure();
  }

  @override
  Future<List<Map<String, dynamic>>> adminUsers() async {
    record('adminUsers');
    final failure = _take(adminFailure);
    if (failure != null) _throw(failure);
    return List.unmodifiable(adminUsersValue);
  }

  @override
  Future<Map<String, dynamic>> adminCreateUser({
    required String email,
    required String password,
    required String fullName,
    required List<String> roleNames,
  }) async {
    record('adminCreateUser', {
      'email': email,
      'password': password,
      'fullName': fullName,
      'roleNames': roleNames,
    });
    _checkAdminFailure();
    return {'id': 'user-new', 'email': email, 'fullName': fullName};
  }

  @override
  Future<Map<String, dynamic>> adminUpdateUser({
    required String id,
    required String email,
    required String fullName,
    required bool isActive,
    String? newPassword,
  }) async {
    record('adminUpdateUser', {
      'id': id,
      'email': email,
      'fullName': fullName,
      'isActive': isActive,
      'newPassword': newPassword,
    });
    _checkAdminFailure();
    return {
      'id': id,
      'email': email,
      'fullName': fullName,
      'isActive': isActive,
    };
  }

  @override
  Future<Map<String, dynamic>> adminSetUserRoles({
    required String id,
    required List<String> roleNames,
  }) async {
    record('adminSetUserRoles', {'id': id, 'roleNames': roleNames});
    _checkAdminFailure();
    return {'id': id, 'roles': roleNames};
  }

  @override
  Future<void> adminDeleteUser(String id) async {
    record('adminDeleteUser', {'id': id});
    _checkAdminFailure();
  }

  void _checkAdminFailure() {
    final failure = _take(adminFailure);
    if (failure != null) _throw(failure);
  }

  void _remember(AuthUser account) {
    usersById[account.id] = account;
    final summary = AuthAccountSummary(
      id: account.id,
      fullName: account.fullName,
      email: account.email,
    );
    accountsValue = [
      summary,
      ...accountsValue.where((saved) => saved.id != account.id),
    ].take(5).toList();
  }
}

Widget mobileTestApp({
  required Widget home,
  Map<String, WidgetBuilder> routes = const {},
}) => MaterialApp(
  theme: ThemeData(useMaterial3: true),
  home: home,
  routes: routes,
);
