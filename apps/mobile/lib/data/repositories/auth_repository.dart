import '../models/auth_models.dart';
import '../services/auth_api_service.dart';

class AuthRepository {
  AuthRepository({required this.apiService});

  final AuthApiService apiService;

  Future<AuthUser> login({
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    final response = await apiService.login(
      email: email,
      password: password,
      rememberMe: rememberMe,
    );
    return response.user;
  }

  Future<AuthUser> restore() async {
    try {
      return await apiService.currentUser();
    } on AuthApiException catch (error) {
      if (error.statusCode != 401) rethrow;
      final response = await apiService.refresh();
      return response.user;
    }
  }

  Future<List<AuthSession>> sessions() => apiService.sessions();
  Future<void> logoutCurrentDevice() => apiService.logoutCurrentDevice();
  Future<void> logoutAllDevices() => apiService.logoutAllDevices();
}
