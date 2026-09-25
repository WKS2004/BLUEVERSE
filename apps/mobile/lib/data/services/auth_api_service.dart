import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/auth_models.dart';
import 'api_gateway_config.dart';
import 'auth_credential_store.dart';

class AuthApiException implements Exception {
  const AuthApiException(this.statusCode, this.message);

  final int statusCode;
  final String message;

  @override
  String toString() => message;
}

class AuthApiService {
  AuthApiService({
    http.Client? client,
    AuthCredentialStore? storage,
    ApiGatewayConfig? gateway,
  }) : _transport = client ?? http.Client(),
       _storage = storage ?? const SecureAuthCredentialStore(),
       _gateway = gateway ?? const ApiGatewayConfig();

  static const _deviceIdKey = 'blueverse.device_id';
  static const _deviceKeyKey = 'blueverse.device_key';
  static const _accessTokenKey = 'blueverse.access_token';
  static const _refreshTokenKey = 'blueverse.refresh_token';
  static const _accountIdsKey = 'blueverse.account_ids';
  static const _activeAccountIdKey = 'blueverse.active_account_id';

  final http.Client _transport;
  final AuthCredentialStore _storage;
  final ApiGatewayConfig _gateway;
  Uri? _preferredBaseUri;

  Future<AuthResponse> login({
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    final deviceId = await _storage.read(_deviceIdKey);
    final deviceKey = await _storage.read(_deviceKeyKey);
    final response = await _request(
      '/api/auth/login',
      (uri) async => _transport.post(
        uri,
        headers: _jsonHeaders,
        body: jsonEncode({
          'email': email,
          'password': password,
          'deviceId': deviceId,
          'deviceKey': deviceKey,
          'rememberMe': rememberMe,
          'useCookies': false,
        }),
      ),
    );
    final auth = _decodeAuthResponse(response);
    await _persist(auth);
    return auth;
  }

  Future<AuthResponse> register({
    required String fullName,
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    final deviceId = await _storage.read(_deviceIdKey);
    final deviceKey = await _storage.read(_deviceKeyKey);
    final response = await _request(
      '/api/auth/register',
      (uri) async => _transport.post(
        uri,
        headers: _jsonHeaders,
        body: jsonEncode({
          'fullName': fullName,
          'email': email,
          'password': password,
          'deviceId': deviceId,
          'deviceKey': deviceKey,
          'rememberMe': rememberMe,
          'useCookies': false,
        }),
      ),
    );

    try {
      final auth = _decodeAuthResponse(response);
      await _persist(auth);
      return auth;
    } on AuthApiException catch (error) {
      if (error.statusCode == HttpStatus.badRequest &&
          error.message.toLowerCase().contains('already exists')) {
        throw const AuthApiException(
          HttpStatus.badRequest,
          'We could not create an account with those details. If you already have an account, sign in instead.',
        );
      }
      rethrow;
    }
  }

  Future<AuthResponse> refresh() async {
    final activeAccountId = await _storage.read(_activeAccountIdKey);
    final scopedRefresh = activeAccountId == null
        ? null
        : await _storage.read(_refreshTokenKeyFor(activeAccountId));
    final legacyAccess = await _storage.read(_accessTokenKey);
    final legacyRefresh = await _storage.read(_refreshTokenKey);
    final refreshToken = activeAccountId == null
        ? legacyRefresh
        : (scopedRefresh ??
              (_jwtSubject(legacyAccess) == activeAccountId
                  ? legacyRefresh
                  : null));
    if (refreshToken == null || refreshToken.isEmpty) {
      throw const AuthApiException(
        HttpStatus.unauthorized,
        'No refresh token is stored.',
      );
    }

    final response = await _request(
      '/api/auth/refresh',
      (uri) async => _transport.post(
        uri,
        headers: _jsonHeaders,
        body: jsonEncode({
          'refreshToken': refreshToken,
          'deviceId': await _storage.read(_deviceIdKey),
          'deviceKey': await _storage.read(_deviceKeyKey),
          'useCookies': false,
        }),
      ),
    );
    final auth = _decodeAuthResponse(response);
    await _persist(auth);
    return auth;
  }

  Future<AuthUser> currentUser() async {
    final response = await _request(
      '/api/auth/me',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    final user = _decode(response, AuthUser.fromJson);
    await _ensureAccountSaved(user);
    return user;
  }

  Future<AuthUser> updateCurrentUser({required String fullName}) async {
    final response = await _request(
      '/api/auth/me',
      (uri) async => _transport.put(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'fullName': fullName}),
      ),
    );
    return _decode(response, AuthUser.fromJson);
  }

  Future<List<AuthSession>> sessions() async {
    final response = await _request(
      '/api/auth/sessions',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    final decoded = _decodeList(response);
    return decoded.map(AuthSession.fromJson).toList(growable: false);
  }

  Future<List<AuthAccountSummary>> accounts() async => _readAccounts();

  Future<void> rememberAccount(AuthUser user) async =>
      _ensureAccountSaved(user);

  Future<AuthUser> switchAccount(String userId) async {
    final access = await _storage.read(_accessTokenKeyFor(userId));
    final scopedRefresh = await _storage.read(_refreshTokenKeyFor(userId));
    final legacyAccess = await _storage.read(_accessTokenKey);
    final legacyRefresh = await _storage.read(_refreshTokenKey);
    final hasLegacyAccount = _jwtSubject(legacyAccess) == userId;
    final hasScopedCredentials =
        (access != null && access.isNotEmpty) ||
        (scopedRefresh != null && scopedRefresh.isNotEmpty);
    final hasLegacyCredentials =
        hasLegacyAccount && legacyRefresh != null && legacyRefresh.isNotEmpty;
    if (!hasScopedCredentials && !hasLegacyCredentials) {
      throw const AuthApiException(
        HttpStatus.unauthorized,
        'This account needs you to sign in again.',
      );
    }

    final previousAccountId = await _storage.read(_activeAccountIdKey);
    await _storage.write(_activeAccountIdKey, userId);
    try {
      try {
        final user = await currentUser();
        if (user.id != userId) {
          throw const AuthApiException(
            HttpStatus.unauthorized,
            'This account needs you to sign in again.',
          );
        }
        return user;
      } on AuthApiException catch (error) {
        if (error.statusCode != HttpStatus.unauthorized) rethrow;
        await refresh();
        final user = await currentUser();
        if (user.id != userId) {
          throw const AuthApiException(
            HttpStatus.unauthorized,
            'This account needs you to sign in again.',
          );
        }
        return user;
      }
    } on Object {
      if (previousAccountId != null) {
        await _storage.write(_activeAccountIdKey, previousAccountId);
      } else {
        await _storage.delete(_activeAccountIdKey);
      }
      rethrow;
    }
  }

  Future<void> removeAccountFromDevice(String userId) async {
    final response = await _request(
      '/api/auth/logout-account',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'userId': userId}),
      ),
    );
    _ensureSuccess(response);
    await _forgetAccount(userId);
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    final response = await _request(
      '/api/auth/change-password',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({
          'currentPassword': currentPassword,
          'newPassword': newPassword,
        }),
      ),
    );
    _ensureSuccess(response);
    final activeAccountId = await _storage.read(_activeAccountIdKey);
    if (activeAccountId != null) await _forgetAccount(activeAccountId);
  }

  Future<void> revokeSession(
    String sessionId, {
    String currentPassword = '',
  }) async {
    final response = await _request(
      '/api/auth/sessions/$sessionId',
      (uri) async => _transport.delete(
        uri,
        headers: {...await _authHeaders(), 'Content-Type': 'application/json'},
        body: jsonEncode({'currentPassword': currentPassword}),
      ),
    );
    _ensureSuccess(response);
  }

  Future<void> forgetAccountLocally(String userId) => _forgetAccount(userId);

  Future<void> logoutCurrentDevice() async {
    final activeAccountId = await _storage.read(_activeAccountIdKey);
    if (activeAccountId == null) return;
    await removeAccountFromDevice(activeAccountId);
  }

  Future<void> logoutAllDevices(String currentPassword) async {
    final response = await _request(
      '/api/auth/logout-all-devices',
      (uri) async => _transport.post(
        uri,
        headers: {..._jsonHeaders, ...await _authHeaders()},
        body: jsonEncode({'currentPassword': currentPassword}),
      ),
    );
    _ensureSuccess(response);
    final activeAccountId = await _storage.read(_activeAccountIdKey);
    if (activeAccountId != null) await _forgetAccount(activeAccountId);
  }

  Future<void> deleteCurrentUser() async {
    final response = await _request(
      '/api/auth/me',
      (uri) async => _transport.delete(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
  }

  Future<List<Map<String, dynamic>>> adminPermissions() =>
      _adminList('/api/auth/permissions');

  Future<List<Map<String, dynamic>>> adminRoles() =>
      _adminList('/api/auth/roles');

  Future<Map<String, dynamic>> adminCreateRole({
    required String name,
    required String description,
  }) => _adminMap(
    '/api/auth/roles',
    method: 'POST',
    body: {'name': name, 'description': description},
  );

  Future<Map<String, dynamic>> adminUpdateRole({
    required String id,
    required String name,
    required String description,
  }) => _adminMap(
    '/api/auth/roles/${Uri.encodeComponent(id)}',
    method: 'PUT',
    body: {'name': name, 'description': description},
  );

  Future<Map<String, dynamic>> adminSetRolePermissions({
    required String id,
    required List<String> permissionCodes,
  }) => _adminMap(
    '/api/auth/roles/${Uri.encodeComponent(id)}/permissions',
    method: 'POST',
    body: {'permissionCodes': permissionCodes},
  );

  Future<void> adminDeleteRole(String id) async {
    await _adminRequest(
      '/api/auth/roles/${Uri.encodeComponent(id)}',
      method: 'DELETE',
    );
  }

  Future<List<Map<String, dynamic>>> adminUsers() =>
      _adminList('/api/auth/users');

  Future<Map<String, dynamic>> adminCreateUser({
    required String email,
    required String password,
    required String fullName,
    required List<String> roleNames,
  }) => _adminMap(
    '/api/auth/users',
    method: 'POST',
    body: {
      'email': email,
      'password': password,
      'fullName': fullName,
      'roleNames': roleNames,
    },
  );

  Future<Map<String, dynamic>> adminUpdateUser({
    required String id,
    required String email,
    required String fullName,
    required bool isActive,
    String? newPassword,
  }) => _adminMap(
    '/api/auth/users/${Uri.encodeComponent(id)}',
    method: 'PUT',
    body: {
      'email': email,
      'fullName': fullName,
      'isActive': isActive,
      if (newPassword != null && newPassword.isNotEmpty)
        'newPassword': newPassword,
    },
  );

  Future<Map<String, dynamic>> adminSetUserRoles({
    required String id,
    required List<String> roleNames,
  }) => _adminMap(
    '/api/auth/users/${Uri.encodeComponent(id)}/roles',
    method: 'POST',
    body: {'roleNames': roleNames},
  );

  Future<void> adminDeleteUser(String id) async {
    await _adminRequest(
      '/api/auth/users/${Uri.encodeComponent(id)}',
      method: 'DELETE',
    );
  }

  Future<List<Map<String, dynamic>>> _adminList(String path) async {
    final response = await _adminRequest(path);
    return _decodeList(response);
  }

  Future<Map<String, dynamic>> _adminMap(
    String path, {
    required String method,
    required Map<String, dynamic> body,
  }) async {
    final response = await _adminRequest(path, method: method, body: body);
    return _decode(response, (json) => json);
  }

  Future<http.Response> _adminRequest(
    String path, {
    String method = 'GET',
    Map<String, dynamic>? body,
  }) async {
    final response = await _request(path, (uri) async {
      final headers = await _authHeaders();
      return switch (method) {
        'GET' => _transport.get(uri, headers: headers),
        'POST' => _transport.post(
          uri,
          headers: headers,
          body: jsonEncode(body),
        ),
        'PUT' => _transport.put(uri, headers: headers, body: jsonEncode(body)),
        'DELETE' => _transport.delete(uri, headers: headers),
        _ => throw ArgumentError.value(
          method,
          'method',
          'Unsupported administration HTTP method.',
        ),
      };
    });
    _ensureSuccess(response);
    return response;
  }

  Future<Map<String, String>> _authHeaders() async {
    final activeAccountId = await _storage.read(_activeAccountIdKey);
    final scopedToken = activeAccountId == null
        ? null
        : await _storage.read(_accessTokenKeyFor(activeAccountId));
    final legacyToken = await _storage.read(_accessTokenKey);
    final token = activeAccountId == null
        ? legacyToken
        : (scopedToken ??
              (_jwtSubject(legacyToken) == activeAccountId
                  ? legacyToken
                  : null));
    return {
      ..._jsonHeaders,
      if (token != null && token.isNotEmpty)
        HttpHeaders.authorizationHeader: 'Bearer $token',
    };
  }

  Future<void> _persist(AuthResponse auth) async {
    await _storage.write(_deviceIdKey, auth.deviceId);
    if (auth.deviceKey != null) {
      await _storage.write(_deviceKeyKey, auth.deviceKey!);
    }
    final previousLegacyAccess = await _storage.read(_accessTokenKey);
    final previousLegacyRefresh = await _storage.read(_refreshTokenKey);
    final previousLegacyUserId = _jwtSubject(previousLegacyAccess);
    if (previousLegacyUserId != null &&
        previousLegacyUserId != auth.user.id &&
        (await _storage.read(_accessTokenKeyFor(previousLegacyUserId))) ==
            null) {
      await _storage.write(
        _accessTokenKeyFor(previousLegacyUserId),
        previousLegacyAccess!,
      );
      if (previousLegacyRefresh != null) {
        await _storage.write(
          _refreshTokenKeyFor(previousLegacyUserId),
          previousLegacyRefresh,
        );
      }
      await _rememberAccountSummary(
        AuthAccountSummary(
          id: previousLegacyUserId,
          fullName: 'BLUEVERSE account',
          email: 'Sign in to view details',
        ),
      );
    }

    await _storage.write(_accessTokenKeyFor(auth.user.id), auth.token);
    await _storage.write(_refreshTokenKeyFor(auth.user.id), auth.refreshToken);
    // Keep the previous single-account keys in sync with the selected account
    // for upgrades and clients that still read the legacy storage names.
    await _storage.write(_accessTokenKey, auth.token);
    await _storage.write(_refreshTokenKey, auth.refreshToken);
    await _storage.write(_activeAccountIdKey, auth.user.id);
    await _ensureAccountSaved(auth.user);
  }

  String _accessTokenKeyFor(String userId) => 'blueverse.access_token.$userId';

  String _refreshTokenKeyFor(String userId) =>
      'blueverse.refresh_token.$userId';

  String? _jwtSubject(String? token) {
    if (token == null || token.isEmpty) return null;
    try {
      final parts = token.split('.');
      if (parts.length != 3) return null;
      final payload = jsonDecode(
        utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))),
      );
      if (payload is! Map) return null;
      final subject =
          payload['sub'] ??
          payload['nameid'] ??
          payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
      return subject is String && subject.isNotEmpty ? subject : null;
    } on Object {
      return null;
    }
  }

  Future<void> _ensureAccountSaved(AuthUser user) async {
    final legacyAccess = await _storage.read(_accessTokenKey);
    final legacyRefresh = await _storage.read(_refreshTokenKey);
    final legacyUserId = _jwtSubject(legacyAccess);
    if (legacyUserId == user.id &&
        (await _storage.read(_accessTokenKeyFor(user.id))) == null) {
      await _storage.write(_accessTokenKeyFor(user.id), legacyAccess!);
      if (legacyRefresh != null) {
        await _storage.write(_refreshTokenKeyFor(user.id), legacyRefresh);
      }
      await _storage.delete(_accessTokenKey);
      await _storage.delete(_refreshTokenKey);
    }
    await _storage.write(_activeAccountIdKey, user.id);
    await _rememberAccountSummary(AuthAccountSummary.fromUser(user));
  }

  Future<List<AuthAccountSummary>> _readAccounts() async {
    final raw = await _storage.read(_accountIdsKey);
    if (raw == null || raw.isEmpty) return const [];
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! List) return const [];
      return decoded
          .whereType<Map>()
          .map(
            (item) =>
                AuthAccountSummary.fromJson(Map<String, dynamic>.from(item)),
          )
          .take(5)
          .toList(growable: false);
    } on Object {
      return const [];
    }
  }

  Future<void> _rememberAccountSummary(AuthAccountSummary account) async {
    final existing = await _readAccounts();
    final updated = [
      account,
      ...existing.where((item) => item.id != account.id),
    ].take(5).map((item) => item.toJson()).toList(growable: false);
    await _storage.write(_accountIdsKey, jsonEncode(updated));
  }

  Future<void> _forgetAccount(String userId) async {
    await Future.wait([
      _storage.delete(_accessTokenKeyFor(userId)),
      _storage.delete(_refreshTokenKeyFor(userId)),
    ]);
    if (_jwtSubject(await _storage.read(_accessTokenKey)) == userId) {
      await Future.wait([
        _storage.delete(_accessTokenKey),
        _storage.delete(_refreshTokenKey),
      ]);
    }
    final remaining = (await _readAccounts())
        .where((account) => account.id != userId)
        .toList(growable: false);
    await _storage.write(
      _accountIdsKey,
      jsonEncode(remaining.map((account) => account.toJson()).toList()),
    );
    if (await _storage.read(_activeAccountIdKey) == userId) {
      if (remaining.isEmpty) {
        await _storage.delete(_activeAccountIdKey);
      } else {
        await _storage.write(_activeAccountIdKey, remaining.first.id);
      }
    }
  }

  AuthResponse _decodeAuthResponse(http.Response response) =>
      _decode(response, AuthResponse.fromJson);

  T _decode<T>(
    http.Response response,
    T Function(Map<String, dynamic>) parser,
  ) {
    _ensureSuccess(response);
    final decoded = jsonDecode(response.body);
    if (decoded is! Map) {
      throw const FormatException('The API response is not an object.');
    }
    return parser(Map<String, dynamic>.from(decoded));
  }

  List<Map<String, dynamic>> _decodeList(http.Response response) {
    _ensureSuccess(response);
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('The API response is not a list.');
    }
    return decoded
        .map((item) {
          if (item is! Map) {
            throw const FormatException('An API list item is not an object.');
          }
          return Map<String, dynamic>.from(item);
        })
        .toList(growable: false);
  }

  void _ensureSuccess(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return;
    }
    var message = 'The authentication request failed (${response.statusCode}).';
    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map && decoded['detail'] is String) {
        message = decoded['detail'] as String;
      }
    } on Object {
      // Keep the status-based message when the dependency returns malformed JSON.
    }
    throw AuthApiException(response.statusCode, message);
  }

  Future<http.Response> _request(
    String path,
    Future<http.Response> Function(Uri uri) request,
  ) async {
    final candidates = _orderedBaseUris();
    for (final baseUri in candidates) {
      try {
        final response = await request(_gateway.endpoint(baseUri, path))
            .timeout(const Duration(seconds: 3));
        _preferredBaseUri = baseUri;
        return response;
      } on Object catch (error) {
        if (!_isConnectivityError(error)) {
          rethrow;
        }
      }
    }

    throw AuthApiException(0, _gateway.connectionFailureMessage(candidates));
  }

  List<Uri> _orderedBaseUris() {
    final baseUris = _gateway.baseUris;
    final preferred = _preferredBaseUri;
    if (preferred == null) {
      return baseUris;
    }

    return [preferred, ...baseUris.where((uri) => uri != preferred)];
  }

  bool _isConnectivityError(Object error) {
    return error is SocketException ||
        error is TimeoutException ||
        error is http.ClientException;
  }

  static const _jsonHeaders = <String, String>{
    HttpHeaders.acceptHeader: 'application/json',
    HttpHeaders.contentTypeHeader: 'application/json; charset=UTF-8',
  };
}
