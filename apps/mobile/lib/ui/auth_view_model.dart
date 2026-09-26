import 'package:flutter/foundation.dart';

import '../data/models/auth_models.dart';
import '../data/repositories/auth_repository.dart';
import '../data/services/auth_api_service.dart' show AuthApiException;
import 'feedback/loading_screen_controller.dart';

class AuthViewModel extends ChangeNotifier {
  AuthViewModel({required this.repository});

  final AuthRepository repository;
  AuthUser? user;
  List<AuthAccountSummary> accounts = const [];
  List<AuthSession> sessions = const [];
  bool isLoading = false;
  bool hasRestoredSession = false;
  bool _isRestoringSession = false;
  String? errorMessage;

  Future<void> signIn({
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    await _run(() async {
      final previousUser = user;
      if (previousUser != null) {
        await repository.rememberAccount(previousUser);
      }
      user = await repository.login(
        email: email,
        password: password,
        rememberMe: rememberMe,
      );
      sessions = await repository.sessions();
      accounts = await repository.accounts();
    }, message: const LoadingScreenMessage.signIn());
  }

  Future<bool> createAccount({
    required String fullName,
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    final finishLoading = blueverseLoadingScreenController.begin(
      message: const LoadingScreenMessage.registration(),
    );
    isLoading = true;
    errorMessage = null;
    notifyListeners();
    try {
      user = await repository.register(
        fullName: fullName,
        email: email,
        password: password,
        rememberMe: rememberMe,
      );
      sessions = const [];
      accounts = await repository.accounts();
      try {
        sessions = await repository.sessions();
      } on Object {
        errorMessage = 'Your account was created, but session details could not be loaded.';
      }
      return true;
    } on Object catch (error) {
      errorMessage = error.toString();
      return false;
    } finally {
      isLoading = false;
      notifyListeners();
      finishLoading();
    }
  }

  Future<void> restore() async {
    if (hasRestoredSession || _isRestoringSession) return;
    _isRestoringSession = true;
    try {
      await _run(() async {
        try {
          user = await repository.restore();
          sessions = await repository.sessions();
          accounts = await repository.accounts();
        } on AuthApiException catch (error) {
          if (error.statusCode != 401) rethrow;
          accounts = await repository.accounts();
          user = null;
          sessions = const [];
          for (final account in accounts) {
            try {
              user = await repository.switchAccount(account.id);
              sessions = await repository.sessions();
              accounts = await repository.accounts();
              break;
            } on AuthApiException catch (switchError) {
              if (switchError.statusCode != 401) rethrow;
            }
          }
        }
      }, message: const LoadingScreenMessage.restoreSession());
    } finally {
      _isRestoringSession = false;
      hasRestoredSession = true;
      notifyListeners();
    }
  }

  Future<void> reloadSessions() async {
    await _run(() async {
      sessions = await repository.sessions();
    }, message: const LoadingScreenMessage.sessions());
  }

  Future<List<Map<String, dynamic>>> adminPermissions() =>
      repository.adminPermissions();
  Future<List<Map<String, dynamic>>> adminRoles() => repository.adminRoles();
  Future<Map<String, dynamic>> adminCreateRole({
    required String name,
    required String description,
  }) => repository.adminCreateRole(name: name, description: description);
  Future<Map<String, dynamic>> adminUpdateRole({
    required String id,
    required String name,
    required String description,
  }) =>
      repository.adminUpdateRole(id: id, name: name, description: description);
  Future<Map<String, dynamic>> adminSetRolePermissions({
    required String id,
    required List<String> permissionCodes,
  }) => repository.adminSetRolePermissions(
    id: id,
    permissionCodes: permissionCodes,
  );
  Future<void> adminDeleteRole(String id) => repository.adminDeleteRole(id);
  Future<List<Map<String, dynamic>>> adminUsers() => repository.adminUsers();
  Future<Map<String, dynamic>> adminCreateUser({
    required String email,
    required String password,
    required String fullName,
    required List<String> roleNames,
  }) => repository.adminCreateUser(
    email: email,
    password: password,
    fullName: fullName,
    roleNames: roleNames,
  );
  Future<Map<String, dynamic>> adminUpdateUser({
    required String id,
    required String email,
    required String fullName,
    required bool isActive,
    String? newPassword,
  }) => repository.adminUpdateUser(
    id: id,
    email: email,
    fullName: fullName,
    isActive: isActive,
    newPassword: newPassword,
  );
  Future<Map<String, dynamic>> adminSetUserRoles({
    required String id,
    required List<String> roleNames,
  }) => repository.adminSetUserRoles(id: id, roleNames: roleNames);
  Future<void> adminDeleteUser(String id) => repository.adminDeleteUser(id);

  Future<bool> updateProfile({required String fullName}) async {
    var updated = false;
    await _run(() async {
      user = await repository.updateCurrentUser(fullName: fullName);
      updated = true;
    }, message: const LoadingScreenMessage.profile());
    return updated;
  }

  Future<void> switchAccount(String userId) async {
    await _run(() async {
      user = await repository.switchAccount(userId);
      sessions = await repository.sessions();
      accounts = await repository.accounts();
    }, message: const LoadingScreenMessage.switchAccount());
  }

  Future<void> removeAccountFromDevice(String userId) async {
    await _run(() async {
      final wasActive = user?.id == userId;
      await repository.removeAccountFromDevice(userId);
      accounts = await repository.accounts();
      if (wasActive) {
        if (accounts.isEmpty) {
          user = null;
          sessions = const [];
        } else {
          user = await repository.switchAccount(accounts.first.id);
          sessions = await repository.sessions();
        }
      }
    }, message: const LoadingScreenMessage.signOut());
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    await _run(() async {
      await repository.changePassword(
        currentPassword: currentPassword,
        newPassword: newPassword,
      );
      accounts = await repository.accounts();
      if (accounts.isEmpty) {
        user = null;
        sessions = const [];
      } else {
        user = await repository.switchAccount(accounts.first.id);
        sessions = await repository.sessions();
      }
    }, message: const LoadingScreenMessage.password());
  }

  Future<void> endSession(
    AuthSession session, {
    String currentPassword = '',
  }) async {
    await _run(() async {
      await repository.revokeSession(
        session.id,
        currentPassword: currentPassword,
      );
      if (session.isCurrent && user != null) {
        await repository.forgetAccountLocally(user!.id);
        accounts = await repository.accounts();
        if (accounts.isEmpty) {
          user = null;
          sessions = const [];
        } else {
          user = await repository.switchAccount(accounts.first.id);
          sessions = await repository.sessions();
        }
      } else {
        sessions = sessions.where((item) => item.id != session.id).toList();
      }
    }, message: const LoadingScreenMessage.endSession());
  }

  Future<void> logoutCurrentDevice() async {
    await _run(() async {
      final currentUserId = user?.id;
      if (currentUserId == null) return;
      await repository.removeAccountFromDevice(currentUserId);
      accounts = await repository.accounts();
      if (accounts.isEmpty) {
        user = null;
        sessions = const [];
      } else {
        user = await repository.switchAccount(accounts.first.id);
        sessions = await repository.sessions();
      }
    }, message: const LoadingScreenMessage.signOut());
  }

  Future<void> logoutAllDevices({required String currentPassword}) async {
    await _run(() async {
      await repository.logoutAllDevices(currentPassword);
      accounts = await repository.accounts();
      if (accounts.isEmpty) {
        user = null;
        sessions = const [];
      } else {
        user = await repository.switchAccount(accounts.first.id);
        sessions = await repository.sessions();
      }
    }, message: const LoadingScreenMessage.signOut());
  }

  Future<bool> deleteAccount() async {
    final currentUserId = user?.id;
    if (currentUserId == null) return false;
    var deleted = false;
    await _run(() async {
      await repository.deleteCurrentUser();
      deleted = true;
      await repository.forgetAccountLocally(currentUserId);
      user = null;
      sessions = const [];
      accounts = await repository.accounts();
      if (accounts.isEmpty) {
        return;
      } else {
        user = await repository.switchAccount(accounts.first.id);
        sessions = await repository.sessions();
      }
    }, message: const LoadingScreenMessage.deleteAccount());
    return deleted;
  }

  Future<void> _run(
    Future<void> Function() action, {
    LoadingScreenMessage message = const LoadingScreenMessage.restoreSession(),
  }) async {
    final finishLoading = blueverseLoadingScreenController.begin(
      message: message,
    );
    isLoading = true;
    errorMessage = null;
    notifyListeners();
    try {
      await action();
    } on Object catch (error) {
      errorMessage = error.toString();
    } finally {
      isLoading = false;
      notifyListeners();
      finishLoading();
    }
  }
}
