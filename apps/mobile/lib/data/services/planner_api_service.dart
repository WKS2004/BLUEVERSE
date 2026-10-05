import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/planner_models.dart';
import 'api_gateway_config.dart';
import 'auth_credential_store.dart';

class PlannerApiException implements Exception {
  const PlannerApiException(this.statusCode, this.message);

  final int statusCode;
  final String message;

  @override
  String toString() => message;
}

/// Client for the Smart Coastal Planner public API behind the shared
/// gateway. Follows the same gateway-candidate, timeout and scoped-token
/// conventions as AuthApiService.
class PlannerApiService {
  PlannerApiService({
    http.Client? client,
    AuthCredentialStore? storage,
    ApiGatewayConfig? gateway,
  }) : _transport = client ?? http.Client(),
       _storage = storage ?? const SecureAuthCredentialStore(),
       _gateway = gateway ?? const ApiGatewayConfig();

  static const _accessTokenKey = 'blueverse.access_token';
  static const _activeAccountIdKey = 'blueverse.active_account_id';

  final http.Client _transport;
  final AuthCredentialStore _storage;
  final ApiGatewayConfig _gateway;
  Uri? _preferredBaseUri;

  Future<PlannerRecommendationResult> createRecommendations({
    required String targetDestinationId,
    required DateTime startsAt,
    required DateTime endsAt,
    required int durationHours,
    required List<String> preferredActivityIds,
    String? experienceLevel,
    bool includeBiodiversityContext = true,
  }) async {
    final response = await _request(
      '/api/planner/recommendations',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({
          'targetDestinationId': targetDestinationId,
          'startsAt': startsAt.toUtc().toIso8601String(),
          'endsAt': endsAt.toUtc().toIso8601String(),
          'durationHours': durationHours,
          'preferredActivityIds': preferredActivityIds,
          'experienceLevel': experienceLevel,
          'includeBiodiversityContext': includeBiodiversityContext,
        }),
      ),
    );
    return _decode(response, PlannerRecommendationResult.fromJson);
  }

  Future<PlannerRecommendationResult> recommendation(
    String recommendationId,
  ) async {
    final response = await _request(
      '/api/planner/recommendations/${Uri.encodeComponent(recommendationId)}',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, PlannerRecommendationResult.fromJson);
  }

  Future<PlannerWorkflowStatus> workflow(String workflowId) async {
    final response = await _request(
      '/api/planner/workflows/${Uri.encodeComponent(workflowId)}',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, PlannerWorkflowStatus.fromJson);
  }

  Future<PlannerBiodiversityPrediction> biodiversityPredictions({
    required String destinationId,
    String? activityId,
  }) async {
    final response = await _request(
      '/api/planner/biodiversity/predictions',
      (uri) async => _transport.get(
        uri.replace(queryParameters: {
          'destinationId': destinationId,
          'activityId': ?activityId,
        }),
        headers: await _authHeaders(),
      ),
    );
    return _decode(response, PlannerBiodiversityPrediction.fromJson);
  }

  Future<Map<String, String>> _authHeaders() async {
    final activeAccountId = await _storage.read(_activeAccountIdKey);
    final scopedToken = activeAccountId == null
        ? null
        : await _storage.read('$_accessTokenKey.$activeAccountId');
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

  void _ensureSuccess(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return;
    }
    var message = 'The planning request failed (${response.statusCode}).';
    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map && decoded['detail'] is String) {
        message = decoded['detail'] as String;
      }
    } on Object {
      // Keep the status-based message when the gateway returns malformed JSON.
    }
    throw PlannerApiException(response.statusCode, message);
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

    throw PlannerApiException(0, _gateway.connectionFailureMessage(candidates));
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
