import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

import '../models/auth_models.dart';
import 'api_gateway_config.dart';

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
    FlutterSecureStorage? storage,
    ApiGatewayConfig? gateway,
  }) : _transport = client ?? http.Client(),
       _storage = storage ?? const FlutterSecureStorage(),
       _gateway = gateway ?? const ApiGatewayConfig();

  static const _deviceIdKey = 'blueverse.device_id';
  static const _deviceKeyKey = 'blueverse.device_key';
  static const _accessTokenKey = 'blueverse.access_token';
  static const _refreshTokenKey = 'blueverse.refresh_token';

  final http.Client _transport;
  final FlutterSecureStorage _storage;
  final ApiGatewayConfig _gateway;
  Uri? _preferredBaseUri;

  Future<AuthResponse> login({
    required String email,
    required String password,
    required bool rememberMe,
  }) async {
    final deviceId = await _storage.read(key: _deviceIdKey);
    final deviceKey = await _storage.read(key: _deviceKeyKey);
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

  Future<AuthResponse> refresh() async {
    final refreshToken = await _storage.read(key: _refreshTokenKey);
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
          'deviceId': await _storage.read(key: _deviceIdKey),
          'deviceKey': await _storage.read(key: _deviceKeyKey),
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

  Future<void> logoutCurrentDevice() async {
    final response = await _request(
      '/api/auth/logout',
      (uri) async => _transport.post(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
    await clearStoredSession();
  }

  Future<void> logoutAllDevices() async {
    final response = await _request(
      '/api/auth/logout-all-devices',
      (uri) async => _transport.post(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
    await clearStoredSession();
  }

  Future<void> clearStoredSession() async {
    await Future.wait([
      _storage.delete(key: _deviceIdKey),
      _storage.delete(key: _deviceKeyKey),
      _storage.delete(key: _accessTokenKey),
      _storage.delete(key: _refreshTokenKey),
    ]);
  }

  Future<Map<String, String>> _authHeaders() async {
    final token = await _storage.read(key: _accessTokenKey);
    return {
      ..._jsonHeaders,
      if (token != null && token.isNotEmpty)
        HttpHeaders.authorizationHeader: 'Bearer $token',
    };
  }

  Future<void> _persist(AuthResponse auth) async {
    await _storage.write(key: _deviceIdKey, value: auth.deviceId);
    if (auth.deviceKey != null) {
      await _storage.write(key: _deviceKeyKey, value: auth.deviceKey);
    }
    if (auth.token != null) {
      await _storage.write(key: _accessTokenKey, value: auth.token);
    }
    if (auth.refreshToken != null) {
      await _storage.write(key: _refreshTokenKey, value: auth.refreshToken);
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
        .whereType<Map>()
        .map(Map<String, dynamic>.from)
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
