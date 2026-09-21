import 'package:flutter/foundation.dart';

import '../data/models/auth_models.dart';
import '../data/repositories/auth_repository.dart';

class AuthViewModel extends ChangeNotifier {
  AuthViewModel({required this.repository});

  final AuthRepository repository;
  AuthUser? user;
  List<AuthSession> sessions = const [];
  bool isLoading = false;
  String? errorMessage;

  Future<void> signIn({
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    await _run(() async {
      user = await repository.login(
        email: email,
        password: password,
        rememberMe: rememberMe,
      );
      sessions = await repository.sessions();
    });
  }

  Future<void> restore() async {
    await _run(() async {
      user = await repository.restore();
      sessions = await repository.sessions();
    });
  }

  Future<void> logoutCurrentDevice() async {
    await _run(() async {
      await repository.logoutCurrentDevice();
      user = null;
      sessions = const [];
    });
  }

  Future<void> logoutAllDevices() async {
    await _run(() async {
      await repository.logoutAllDevices();
      user = null;
      sessions = const [];
    });
  }

  Future<void> _run(Future<void> Function() action) async {
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
    }
  }
}
