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
    this.requestTimeout = const Duration(seconds: 55),
    this.onRequestStarted,
    this.onSessionRefreshRequired,
  }) : _transport = client ?? http.Client(),
       _storage = storage ?? const SecureAuthCredentialStore(),
       _gateway = gateway ?? const ApiGatewayConfig();

  static const _accessTokenKey = 'blueverse.access_token';
  static const _activeAccountIdKey = 'blueverse.active_account_id';

  final http.Client _transport;
  final AuthCredentialStore _storage;
  final ApiGatewayConfig _gateway;
  final Duration requestTimeout;
  final void Function() Function()? onRequestStarted;
  final Future<void> Function(String userId)? onSessionRefreshRequired;
  Uri? _preferredBaseUri;

  Future<PlannerCatalogue> catalogue({bool showLoadingScreen = true}) async {
    final response = await _request(
      '/api/planner/catalogue',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      showLoadingScreen: showLoadingScreen,
    );
    return _decode(response, PlannerCatalogue.fromJson);
  }

  Future<List<PlannerHighlight>> highlights() async {
    final response = await _request(
      '/api/planner/recommendations/highlights',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      showLoadingScreen: false,
    );
    _ensureSuccess(response);
    final decoded = jsonDecode(response.body);
    if (decoded is! List || decoded.any((item) => item is! Map)) {
      throw const FormatException('The highlights response is not a list.');
    }
    return decoded
        .whereType<Map>()
        .map(
          (item) => PlannerHighlight.fromJson(Map<String, dynamic>.from(item)),
        )
        .toList(growable: false);
  }

  Future<List<PlannerItinerary>> itineraries({
    int page = 1,
    int pageSize = 20,
    bool showLoadingScreen = true,
  }) async {
    if (page < 1 || pageSize < 1 || pageSize > 100) {
      throw ArgumentError('Choose a valid trip page.');
    }
    final response = await _request(
      '/api/planner/itineraries?page=$page&pageSize=$pageSize',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      showLoadingScreen: showLoadingScreen,
    );
    _ensureSuccess(response);
    final decoded = jsonDecode(response.body);
    if (decoded is! List || decoded.any((item) => item is! Map)) {
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

  Future<List<Map<String, dynamic>>> evaluationHistory(String id) async {
    final response = await _request(
      '/api/planner/itineraries/${Uri.encodeComponent(id)}/re-evaluations',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
    final value = jsonDecode(response.body);
    if (value is! List || value.any((item) => item is! Map)) {
      throw const FormatException('The condition history could not be read.');
    }
    return value.map((item) => Map<String, dynamic>.from(item as Map)).toList();
  }

  Future<PlannerItinerary> updateItinerary(
    PlannerItinerary trip, {
    required String title,
    required String description,
    String? startsAt,
    String? endsAt,
    List<Map<String, dynamic>>? items,
    String? recommendationId,
    bool confirmPlan = false,
  }) async {
    final response = await _request(
      '/api/planner/itineraries/${Uri.encodeComponent(trip.id)}',
      (uri) async => _transport.put(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({
          'title': title,
          'description': description.isEmpty ? null : description,
          'startsAt': startsAt ?? trip.startsAt,
          'endsAt': endsAt ?? trip.endsAt,
          'recommendationId': recommendationId,
          'confirmPlan': confirmPlan,
          'concurrencyVersion': trip.concurrencyVersion,
          'timeZone': trip.timeZone,
          'items':
              items ??
              trip.items
                  .asMap()
                  .entries
                  .map(
                    (entry) => {
                      'itemId': entry.value.id,
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

  static Map<String, dynamic> itemRequest(
    PlannerItineraryItem item,
    int index,
  ) => {
    'itemId': item.id,
    'destinationId': item.destinationId,
    'activityId': item.activityId,
    'offeringId': item.offeringId,
    'title': item.title,
    'orderIndex': index,
    'scheduledStart': item.startsAt,
    'scheduledEnd': item.endsAt,
    'timeZone': item.timeZone,
  };

  Future<PlannerItinerary> addCandidate(
    PlannerItinerary trip,
    PlannerCandidate candidate,
    String recommendationId,
  ) {
    final items = [
      for (var i = 0; i < trip.items.length; i++) itemRequest(trip.items[i], i),
      {
        'destinationId': candidate.destinationId,
        'activityId': candidate.activityId,
        'offeringId': candidate.offeringId,
        'title': candidate.title,
        'orderIndex': trip.items.length,
        'scheduledStart': candidate.scheduledStart,
        'scheduledEnd': candidate.scheduledEnd,
        'timeZone': candidate.timeZone,
      },
    ];
    final start =
        DateTime.parse(candidate.scheduledStart)
            .isBefore(DateTime.parse(trip.startsAt))
        ? candidate.scheduledStart
        : trip.startsAt;
    final end =
        DateTime.parse(candidate.scheduledEnd)
            .isAfter(DateTime.parse(trip.endsAt))
        ? candidate.scheduledEnd
        : trip.endsAt;
    return updateItinerary(
      trip,
      title: trip.title,
      description: trip.description ?? '',
      items: items,
      startsAt: start,
      endsAt: end,
      recommendationId: recommendationId,
    );
  }

  Future<PlannerItinerary> saveDraft({
    required String title,
    required PlannerCandidate candidate,
    String? recommendationId,
    bool confirmPlan = false,
    String? description,
  }) async {
    final response = await _request(
      '/api/planner/itineraries',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({
          'title': title,
          'description': description?.trim().isEmpty == true
              ? null
              : description?.trim(),
          'recommendationId': recommendationId,
          'confirmPlan': confirmPlan,
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
    final prediction = _decode(
      response,
      PlannerBiodiversityPrediction.fromJson,
    );
    if (prediction.status == 'available' &&
        (prediction.destinationId != destinationId ||
            prediction.activityId != activityId)) {
      throw const PlannerApiException(
        502,
        'The wildlife result could not be verified for this location.',
      );
    }
    return prediction;
  }

  Future<Map<String, String>> _authHeaders() async {
    final activeAccountId = await _storage.read(_activeAccountIdKey);
    var token = await _accessToken(activeAccountId);
    if (token != null &&
        onSessionRefreshRequired != null &&
        _isAccessTokenExpiring(token)) {
      final userId = activeAccountId ?? _jwtSubject(token);
      if (userId != null) {
        await onSessionRefreshRequired!(userId);
        final refreshedAccountId = await _storage.read(_activeAccountIdKey);
        if (refreshedAccountId != activeAccountId) {
          throw const PlannerApiException(
            409,
            'Your active account changed. Please try the request again.',
          );
        }
        token = await _accessToken(refreshedAccountId);
      }
    }
    return {
      ..._jsonHeaders,
      if (token != null && token.isNotEmpty)
        HttpHeaders.authorizationHeader: 'Bearer $token',
    };
  }

  Future<String?> _accessToken(String? activeAccountId) async {
    final scopedToken = activeAccountId == null
        ? null
        : await _storage.read('$_accessTokenKey.$activeAccountId');
    final legacyToken = await _storage.read(_accessTokenKey);
    return activeAccountId == null
        ? legacyToken
        : (scopedToken ??
              (_jwtSubject(legacyToken) == activeAccountId
                  ? legacyToken
                  : null));
  }

  bool _isAccessTokenExpiring(String token) {
    try {
      final parts = token.split('.');
      if (parts.length != 3) return false;
      final payload = jsonDecode(
        utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))),
      );
      if (payload is! Map || payload['exp'] is! num) return false;
      final expiry = DateTime.fromMillisecondsSinceEpoch(
        ((payload['exp'] as num) * 1000).round(),
        isUtc: true,
      );
      return !expiry.isAfter(
        DateTime.now().toUtc().add(const Duration(seconds: 60)),
      );
    } on Object {
      return false;
    }
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
    Future<http.Response> Function(Uri uri) request, {
    bool showLoadingScreen = true,
  }) async {
    final finish = showLoadingScreen ? onRequestStarted?.call() : null;
    try {
      return await _sendRequest(path, request);
    } finally {
      finish?.call();
    }
  }

  Future<http.Response> _sendRequest(
    String path,
    Future<http.Response> Function(Uri uri) request,
  ) async {
    final candidates = _orderedBaseUris();
    for (final baseUri in candidates) {
      try {
        final target = Uri.parse(path);
        final uri = _gateway
            .endpoint(baseUri, target.path)
            .replace(query: target.hasQuery ? target.query : null);
        final response = await request(uri).timeout(requestTimeout);
        _preferredBaseUri = baseUri;
        return response;
      } on Object catch (error) {
        if (!_isConnectivityError(error)) {
          rethrow;
        }
        // A timed-out write may already have succeeded. Replaying it against
        // another gateway can create duplicate plans or condition reviews.
        if (path == '/api/planner/recommendations' ||
            path == '/api/planner/itineraries' ||
            path.startsWith('/api/planner/itineraries/')) {
          throw const PlannerApiException(
            0,
            'The request could not be completed. Refresh your saved trips before trying again.',
          );
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
