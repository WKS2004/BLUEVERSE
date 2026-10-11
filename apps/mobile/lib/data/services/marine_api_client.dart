import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/marine_models.dart';
import 'api_gateway_config.dart';

/// Exception exposed by the marine API client.
class MarineApiException implements Exception {
  const MarineApiException(
    this.statusCode,
    this.message, {
    this.dependencyAvailability,
  });

  final int statusCode;
  final String message;
  final MarineDependencyAvailability? dependencyAvailability;

  @override
  String toString() => message;
}

/// Minimal, fixed-forward-HTTP analog of the React-based marine API surface.
///
/// Every path is a registered public route reachable only through
/// the public gateway. It never targets a Docker hostname, PostgreSQL, Auth or
/// Agentic AI directly.
class MarineApiClient {
  MarineApiClient({
    required this._client,
    required this._gateway,
    this.authenticatedHeaders,
    this.beginRequest,
    this.requestTimeout = const Duration(seconds: 45),
  }) {
    if (requestTimeout <= Duration.zero) {
      throw ArgumentError.value(requestTimeout, 'requestTimeout');
    }
  }

  /// Covers Auth/catalogue checks and the provider's two ten-second attempts.
  final Duration requestTimeout;

  final http.Client _client;
  final ApiGatewayConfig _gateway;
  final Future<Map<String, String>> Function()? authenticatedHeaders;
  final void Function() Function(String method)? beginRequest;
  Uri? _preferredBaseUri;

  /// Base URI for all requests, resolved by [ApiGatewayConfig].
  Uri get baseUri => _gateway.baseUris.first;

  Future<List<Map<String, dynamic>>> getActivities() async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/activities');
    final response = await _request('GET', uri);
    try {
      final payload = jsonDecode(response.body);
      if (payload is! List) throw const FormatException();
      final rows = payload
          .map((row) => Map<String, dynamic>.from(row as Map))
          .toList();
      final ids = <String>{};
      final guid = RegExp(
        r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
      );
      for (final row in rows) {
        final id = row['id'];
        if (id is! String ||
            !guid.hasMatch(id) ||
            id == '00000000-0000-0000-0000-000000000000' ||
            !ids.add(id) ||
            row['name'] is! String ||
            (row['name'] as String).trim().isEmpty ||
            row['activityType'] is! String ||
            row['isActive'] != true) {
          throw const FormatException();
        }
      }
      return rows;
    } catch (_) {
      throw const MarineApiException(
        502,
        'The activity list could not be read. Please retry.',
      );
    }
  }

  /// GET /api/marine/current
  Future<ConditionSnapshotDto> getConditions({
    required double latitude,
    required double longitude,
    DateTime? timeUtc,
  }) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/current');
    final query = <String, String>{
      'latitude': latitude.toStringAsFixed(5),
      'longitude': longitude.toStringAsFixed(5),
      if (timeUtc != null) 'time': timeUtc.toUtc().toIso8601String(),
    };

    final response = await _request('GET', uri, query: query);
    return ConditionSnapshotDto.fromJson(jsonDecode(response.body));
  }

  /// GET /api/marine/history
  Future<List<ConditionSnapshotDto>> getHistory({
    double? latitude,
    double? longitude,
    DateTime? fromUtc,
    DateTime? toUtc,
  }) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/history');
    final query = <String, String>{
      if (latitude != null) 'latitude': latitude.toStringAsFixed(5),
      if (longitude != null) 'longitude': longitude.toStringAsFixed(5),
      if (fromUtc != null) 'from': fromUtc.toUtc().toIso8601String(),
      if (toUtc != null) 'to': toUtc.toUtc().toIso8601String(),
    };

    final response = await _request('GET', uri, query: query);
    return _decodeRows(response, ConditionSnapshotDto.fromJson);
  }

  /// GET /api/marine/assessments
  Future<List<MarineAssessmentHistoryDto>> getAssessments({
    String? activityId,
    String? result,
    DateTime? fromUtc,
    DateTime? toUtc,
  }) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/assessments');
    final query = <String, String>{
      if (activityId != null && activityId.isNotEmpty) 'activityId': activityId,
      if (result != null && result.isNotEmpty) 'result': result,
      if (fromUtc != null) 'from': fromUtc.toUtc().toIso8601String(),
      if (toUtc != null) 'to': toUtc.toUtc().toIso8601String(),
    };
    final response = await _request('GET', uri, query: query);
    return _decodeRows(response, MarineAssessmentHistoryDto.fromJson);
  }

  /// POST /api/marine/evaluate
  Future<SuitabilityResultDto> evaluateSuitability({
    required String activityId,
    required double latitude,
    required double longitude,
    DateTime? timeUtc,
  }) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/evaluate');
    final body = <String, dynamic>{
      'activityId': activityId,
      'latitude': latitude.toStringAsFixed(5),
      'longitude': longitude.toStringAsFixed(5),
      if (timeUtc != null) 'dateTime': timeUtc.toUtc().toIso8601String(),
    };

    final response = await _request('POST', uri, body: body);
    return SuitabilityResultDto.fromJson(jsonDecode(response.body));
  }

  /// GET /api/marine/safety-profiles
  Future<List<SafetyProfileDto>> getSafetyProfiles() async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles');
    final response = await _request('GET', uri);
    return _decodeRows(response, SafetyProfileDto.fromJson);
  }

  /// GET /api/marine/safety-profiles/{id}
  Future<SafetyProfileDto?> getSafetyProfile(String id) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles/$id');
    try {
      final response = await _request('GET', uri);
      return SafetyProfileDto.fromJson(jsonDecode(response.body));
    } on MarineApiException catch (error) {
      if (error.statusCode == 404) return null;
      rethrow;
    }
  }

  /// POST /api/marine/safety-profiles
  Future<SafetyProfileDto> createSafetyProfile({
    required String activityId,
    required double maxWindSpeed,
    required double maxWaveHeight,
    required double maxSwellHeight,
    required String windCriteriaSource,
    required String windCriteriaRationale,
    required String waveCriteriaSource,
    required String waveCriteriaRationale,
    required String swellCriteriaSource,
    required String swellCriteriaRationale,
    double? cautionWindSpeed,
    double? cautionWaveHeight,
    double? cautionSwellHeight,
  }) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles');
    final body = <String, dynamic>{
      'activityId': activityId,
      'maxWindSpeed': maxWindSpeed,
      'maxWaveHeight': maxWaveHeight,
      'maxSwellHeight': maxSwellHeight,
      'windCriteriaSource': windCriteriaSource.trim(),
      'windCriteriaRationale': windCriteriaRationale.trim(),
      'waveCriteriaSource': waveCriteriaSource.trim(),
      'waveCriteriaRationale': waveCriteriaRationale.trim(),
      'swellCriteriaSource': swellCriteriaSource.trim(),
      'swellCriteriaRationale': swellCriteriaRationale.trim(),
      'cautionWindSpeed': ?cautionWindSpeed,
      'cautionWaveHeight': ?cautionWaveHeight,
      'cautionSwellHeight': ?cautionSwellHeight,
    };

    final response = await _request('POST', uri, body: body);
    return SafetyProfileDto.fromJson(jsonDecode(response.body));
  }

  /// PUT /api/marine/safety-profiles/{id}
  Future<SafetyProfileDto> updateSafetyProfile({
    required String id,
    required double maxWindSpeed,
    required double maxWaveHeight,
    required double maxSwellHeight,
    required String windCriteriaSource,
    required String windCriteriaRationale,
    required String waveCriteriaSource,
    required String waveCriteriaRationale,
    required String swellCriteriaSource,
    required String swellCriteriaRationale,
    double? cautionWindSpeed,
    double? cautionWaveHeight,
    double? cautionSwellHeight,
  }) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles/$id');
    final body = <String, dynamic>{
      'maxWindSpeed': maxWindSpeed,
      'maxWaveHeight': maxWaveHeight,
      'maxSwellHeight': maxSwellHeight,
      'windCriteriaSource': windCriteriaSource.trim(),
      'windCriteriaRationale': windCriteriaRationale.trim(),
      'waveCriteriaSource': waveCriteriaSource.trim(),
      'waveCriteriaRationale': waveCriteriaRationale.trim(),
      'swellCriteriaSource': swellCriteriaSource.trim(),
      'swellCriteriaRationale': swellCriteriaRationale.trim(),
      'cautionWindSpeed': ?cautionWindSpeed,
      'cautionWaveHeight': ?cautionWaveHeight,
      'cautionSwellHeight': ?cautionSwellHeight,
    };

    final response = await _request('PUT', uri, body: body);
    return SafetyProfileDto.fromJson(jsonDecode(response.body));
  }

  /// POST /api/marine/safety-profiles/{id}/review
  Future<SafetyProfileDto> reviewSafetyProfile(String id) async {
    final uri = _gateway.endpoint(
      baseUri,
      '/api/marine/safety-profiles/$id/review',
    );
    final response = await _request('POST', uri);
    return SafetyProfileDto.fromJson(jsonDecode(response.body));
  }

  /// DELETE /api/marine/safety-profiles/{id}
  Future<void> deactivateSafetyProfile(String id) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles/$id');
    try {
      await _request('DELETE', uri);
    } on MarineApiException catch (error) {
      if (error.statusCode == 404) {
        throw const MarineApiException(404, 'Safety profile not found.');
      }
      rethrow;
    }
  }

  Future<http.Response> _request(
    String method,
    Uri uri, {
    Object? body,
    Map<String, String>? query,
  }) async {
    final finish = beginRequest?.call(method);
    try {
      return await _executeRequest(method, uri, body: body, query: query);
    } finally {
      finish?.call();
    }
  }

  Future<http.Response> _executeRequest(
    String method,
    Uri uri, {
    Object? body,
    Map<String, String>? query,
  }) async {
    final candidates = _orderedBaseUris();
    final mayRetry = method == 'GET' || method == 'HEAD';
    for (final baseUri in candidates) {
      final requestUri = query == null
          ? _gateway.endpoint(baseUri, uri.path)
          : _gateway
                .endpoint(baseUri, uri.path)
                .replace(queryParameters: query);
      try {
        final response = await _send(
          requestUri,
          method,
          body,
        ).timeout(requestTimeout);
        _preferredBaseUri = baseUri;
        if (response.statusCodeIsSuccess()) return response;
        throw MarineApiException(
          response.statusCode,
          _errorMessage(response),
          dependencyAvailability: _dependencyAvailability(response),
        );
      } on SocketException catch (error) {
        if (error.message.contains('Certificate')) {
          throw const MarineApiException(
            0,
            'Certificate issue when reaching the gateway. Use a trusted local certificate or run only on a supported LAN stack.',
          );
        }
        if (!mayRetry) throw _unconfirmedMutation();
        continue;
      } on TimeoutException {
        if (!mayRetry) throw _unconfirmedMutation();
        continue;
      } on http.ClientException {
        if (!mayRetry) throw _unconfirmedMutation();
        continue;
      }
    }

    throw MarineApiException(0, _gateway.connectionFailureMessage(candidates));
  }

  MarineApiException _unconfirmedMutation() => const MarineApiException(
    0,
    'The action outcome could not be confirmed. Check history or the safety profile before trying again.',
  );

  List<Uri> _orderedBaseUris() {
    final baseUris = _gateway.baseUris;
    final preferred = _preferredBaseUri;
    if (preferred == null) return baseUris;
    return [preferred, ...baseUris.where((uri) => uri != preferred)];
  }

  Future<http.Response> _send(Uri uri, String method, Object? body) async {
    final accountHeaders =
        await authenticatedHeaders?.call() ?? const <String, String>{};
    if (authenticatedHeaders != null &&
        !accountHeaders.entries.any(
          (entry) =>
              entry.key.toLowerCase() == 'authorization' &&
              entry.value.startsWith('Bearer ') &&
              entry.value.substring(7).trim().isNotEmpty,
        )) {
      throw const MarineApiException(
        401,
        'Sign in again to access Marine & safety.',
      );
    }
    final headers = <String, String>{
      'Accept': 'application/json',
      'Content-Type': 'application/json; charset=UTF-8',
      ...accountHeaders,
    };

    final request = http.Request(method, uri)..headers.addAll(headers);
    if (body != null) {
      request.body = jsonEncode(body);
    }

    final streamed = await _client.send(request);
    final response = await http.Response.fromStream(streamed);
    return response;
  }

  List<T> _decodeRows<T>(
    http.Response response,
    T Function(Map<String, dynamic>) decode,
  ) {
    try {
      final rows = jsonDecode(response.body);
      if (rows is! List) throw const FormatException();
      return [
        for (final row in rows) decode(Map<String, dynamic>.from(row as Map)),
      ];
    } catch (_) {
      throw const MarineApiException(
        502,
        'Saved marine data could not be read. Please retry.',
      );
    }
  }

  String _errorMessage(http.Response response) {
    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map && decoded['detail'] is String) {
        return decoded['detail'] as String;
      }
      if (decoded is Map && decoded['title'] is String) {
        return decoded['title'] as String;
      }
    } catch (_) {
      // Return the status when the body is not readable JSON.
    }
    return 'The marine service returned an unexpected response (${response.statusCode}).';
  }

  MarineDependencyAvailability? _dependencyAvailability(
    http.Response response,
  ) {
    try {
      final decoded = jsonDecode(response.body);
      return decoded is Map<String, dynamic>
          ? MarineDependencyAvailability.tryParse(decoded)
          : null;
    } catch (_) {
      return null;
    }
  }
}

extension _StatusCode on http.Response {
  bool statusCodeIsSuccess() => statusCode >= 200 && statusCode < 300;
}
