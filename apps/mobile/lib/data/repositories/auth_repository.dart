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

  Future<AuthUser> register({
    required String fullName,
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    final response = await apiService.register(
      fullName: fullName,
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

  Future<AuthUser> updateCurrentUser({required String fullName}) =>
      apiService.updateCurrentUser(fullName: fullName);

  Future<List<AuthAccountSummary>> accounts() => apiService.accounts();
  Future<void> rememberAccount(AuthUser user) =>
      apiService.rememberAccount(user);
  Future<AuthUser> switchAccount(String userId) =>
      apiService.switchAccount(userId);
  Future<void> removeAccountFromDevice(String userId) =>
      apiService.removeAccountFromDevice(userId);
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) => apiService.changePassword(
    currentPassword: currentPassword,
    newPassword: newPassword,
  );
  Future<void> revokeSession(String sessionId, {String currentPassword = ''}) =>
      apiService.revokeSession(sessionId, currentPassword: currentPassword);
  Future<void> forgetAccountLocally(String userId) =>
      apiService.forgetAccountLocally(userId);
  Future<void> deleteCurrentUser() => apiService.deleteCurrentUser();
  Future<List<AuthSession>> sessions() => apiService.sessions();
  Future<List<Map<String, dynamic>>> adminPermissions() =>
      apiService.adminPermissions();
  Future<List<Map<String, dynamic>>> adminRoles() => apiService.adminRoles();
  Future<Map<String, dynamic>> adminCreateRole({
    required String name,
    required String description,
  }) => apiService.adminCreateRole(name: name, description: description);
  Future<Map<String, dynamic>> adminUpdateRole({
    required String id,
    required String name,
    required String description,
  }) =>
      apiService.adminUpdateRole(id: id, name: name, description: description);
  Future<Map<String, dynamic>> adminSetRolePermissions({
    required String id,
    required List<String> permissionCodes,
  }) => apiService.adminSetRolePermissions(
    id: id,
    permissionCodes: permissionCodes,
  );
  Future<void> adminDeleteRole(String id) => apiService.adminDeleteRole(id);
  Future<List<Map<String, dynamic>>> adminUsers() => apiService.adminUsers();
  Future<Map<String, dynamic>> adminCreateUser({
    required String email,
    required String password,
    required String fullName,
    required List<String> roleNames,
  }) => apiService.adminCreateUser(
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
  }) => apiService.adminUpdateUser(
    id: id,
    email: email,
    fullName: fullName,
    isActive: isActive,
    newPassword: newPassword,
  );
  Future<Map<String, dynamic>> adminSetUserRoles({
    required String id,
    required List<String> roleNames,
  }) => apiService.adminSetUserRoles(id: id, roleNames: roleNames);
  Future<void> adminDeleteUser(String id) => apiService.adminDeleteUser(id);
  Future<void> logoutCurrentDevice() => apiService.logoutCurrentDevice();
  Future<void> logoutAllDevices(String currentPassword) =>
      apiService.logoutAllDevices(currentPassword);
}
