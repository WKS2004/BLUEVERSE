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

  Future<PlannerCatalogue> catalogue() async {
    final response = await _request(
      '/api/planner/catalogue',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, PlannerCatalogue.fromJson);
  }

  Future<List<PlannerHighlight>> highlights() async {
    final response = await _request(
      '/api/planner/recommendations/highlights',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('The highlights response is not a list.');
    }
    return decoded
        .whereType<Map>()
        .map(
          (item) => PlannerHighlight.fromJson(Map<String, dynamic>.from(item)),
        )
        .toList(growable: false);
  }

  Future<List<PlannerItinerary>> itineraries() async {
    final response = await _request(
      '/api/planner/itineraries?page=1&pageSize=20',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('The trips response is not a list.');
    }
    return decoded
        .whereType<Map>()
        .map(
          (item) => PlannerItinerary.fromJson(Map<String, dynamic>.from(item)),
        )
        .toList(growable: false);
  }

  Future<PlannerItinerary> itinerary(String id) async {
    final response = await _request(
      '/api/planner/itineraries/${Uri.encodeComponent(id)}',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, PlannerItinerary.fromJson);
  }

  Future<PlannerItinerary> confirmItinerary(String id) async {
    final response = await _request(
      '/api/planner/itineraries/${Uri.encodeComponent(id)}/confirm',
      (uri) async => _transport.post(uri, headers: await _authHeaders()),
    );
    return _decode(response, PlannerItinerary.fromJson);
  }

  Future<void> deleteItinerary(String id) async {
    final response = await _request(
      '/api/planner/itineraries/${Uri.encodeComponent(id)}',
      (uri) async => _transport.delete(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
  }

  Future<Map<String, dynamic>> reEvaluateItinerary(String id) async {
    final response = await _request(
      '/api/planner/itineraries/${Uri.encodeComponent(id)}/re-evaluations',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'reEvaluationMode': 'FULL_ASSESSMENT'}),
      ),
    );
    _ensureSuccess(response);
    final decoded = jsonDecode(response.body);
    if (decoded is! Map) {
      throw const FormatException(
        'The condition review response is not an object.',
      );
    }
    return Map<String, dynamic>.from(decoded);
  }

  Future<PlannerItinerary> updateItinerary(
    PlannerItinerary trip, {
    required String title,
    required String description,
  }) async {
    final response = await _request(
      '/api/planner/itineraries/${Uri.encodeComponent(trip.id)}',
      (uri) async => _transport.put(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({
          'title': title,
          'description': description.isEmpty ? null : description,
          'startsAt': trip.startsAt,
          'endsAt': trip.endsAt,
          'concurrencyVersion': trip.concurrencyVersion,
          'timeZone': trip.timeZone,
          'items': trip.items
              .asMap()
              .entries
              .map(
                (entry) => {
                  'destinationId': entry.value.destinationId,
                  'activityId': entry.value.activityId,
                  'offeringId': entry.value.offeringId,
                  'title': entry.value.title,
                  'orderIndex': entry.key,
                  'scheduledStart': entry.value.startsAt,
                  'scheduledEnd': entry.value.endsAt,
                  'timeZone': entry.value.timeZone,
                },
              )
              .toList(),
        }),
      ),
    );
    return _decode(response, PlannerItinerary.fromJson);
  }

  Future<PlannerItinerary> saveDraft({
    required String title,
    required PlannerCandidate candidate,
  }) async {
    final response = await _request(
      '/api/planner/itineraries',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({
          'title': title,
          'description': null,
          'startsAt': candidate.scheduledStart,
          'endsAt': candidate.scheduledEnd,
          'items': [
            {
              'destinationId': candidate.destinationId,
              'activityId': candidate.activityId,
              'offeringId': candidate.offeringId,
              'title': candidate.title,
              'orderIndex': 0,
              'scheduledStart': candidate.scheduledStart,
              'scheduledEnd': candidate.scheduledEnd,
              'timeZone': candidate.timeZone,
            },
          ],
          'timeZone': candidate.timeZone,
        }),
      ),
    );
    return _decode(response, PlannerItinerary.fromJson);
  }

  Future<PlannerRecommendationResult> createRecommendations({
    required String targetDestinationId,
    required DateTime startsAt,
    required DateTime endsAt,
    required int durationHours,
    required List<String> preferredActivityIds,
    required String timeZone,
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
          'startsAt': _wallTime(startsAt),
          'endsAt': _wallTime(endsAt),
          'timeZone': timeZone,
          'durationHours': durationHours,
          'preferredActivityIds': preferredActivityIds,
          'experienceLevel': experienceLevel,
          'includeBiodiversityContext': includeBiodiversityContext,
        }),
      ),
    );
    return _decode(response, PlannerRecommendationResult.fromJson);
  }

  String _wallTime(DateTime value) =>
      '${value.year.toString().padLeft(4, '0')}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}T${value.hour.toString().padLeft(2, '0')}:${value.minute.toString().padLeft(2, '0')}:00';

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
        uri.replace(
          queryParameters: {
            'destinationId': destinationId,
            'activityId': ?activityId,
          },
        ),
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
