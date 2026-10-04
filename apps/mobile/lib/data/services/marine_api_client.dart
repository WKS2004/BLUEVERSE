import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/marine_models.dart';
import 'api_gateway_config.dart';

/// Exception exposed by the marine API client.
class MarineApiException implements Exception {
  const MarineApiException(this.statusCode, this.message);

  final int statusCode;
  final String message;

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
    required http.Client client,
    required ApiGatewayConfig gateway,
  }) : _client = client,
       _gateway = gateway;

  final http.Client _client;
  final ApiGatewayConfig _gateway;

  /// Base URI for all requests, resolved by [ApiGatewayConfig].
  Uri get baseUri => _gateway.baseUris.first;

  /// GET /api/marine/current
  Future<ConditionSnapshotDto> getConditions({
    required double latitude,
    required double longitude,
    DateTime? timeUtc,
  }) async {
    final uri = _gateway.endpoint(
      baseUri,
      '/api/marine/current',
    );
    final query = <String, String>{
      'latitude': latitude.toStringAsFixed(5),
      'longitude': longitude.toStringAsFixed(5),
      if (timeUtc != null) 'time': timeUtc.toIso8601String(),
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
      if (fromUtc != null) 'from': fromUtc.toIso8601String(),
      if (toUtc != null) 'to': toUtc.toIso8601String(),
    };

    final response = await _request('GET', uri, query: query);
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('The API returned an invalid history body.');
    }
    return decoded
        .whereType<Map>()
        .map((item) => ConditionSnapshotDto.fromJson(Map<String, dynamic>.from(item)))
        .toList(growable: false);
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
      if (timeUtc != null) 'dateTime': timeUtc.toIso8601String(),
    };

    final response = await _request('POST', uri, body: body);
    return SuitabilityResultDto.fromJson(jsonDecode(response.body));
  }

  /// GET /api/marine/safety-profiles
  Future<List<SafetyProfileDto>> getSafetyProfiles() async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles');
    final response = await _request('GET', uri);
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('The API returned an invalid profiles body.');
    }
    return decoded
        .whereType<Map>()
        .map((item) => SafetyProfileDto.fromJson(Map<String, dynamic>.from(item)))
        .toList(growable: false);
  }

  /// GET /api/marine/safety-profiles/{id}
  Future<SafetyProfileDto?> getSafetyProfile(String id) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles/$id');
    final response = await _request('GET', uri);
    if (response.statusCode == 404) return null;
    return SafetyProfileDto.fromJson(jsonDecode(response.body));
  }

  /// POST /api/marine/safety-profiles
  Future<SafetyProfileDto> createSafetyProfile({
    required String activityId,
    required double maxWindSpeed,
    required double maxWaveHeight,
    required double maxSwellHeight,
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
      if (cautionWindSpeed != null) 'cautionWindSpeed': cautionWindSpeed,
      if (cautionWaveHeight != null) 'cautionWaveHeight': cautionWaveHeight,
      if (cautionSwellHeight != null) 'cautionSwellHeight': cautionSwellHeight,
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
    double? cautionWindSpeed,
    double? cautionWaveHeight,
    double? cautionSwellHeight,
    required bool isActive,
  }) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles/$id');
    final body = <String, dynamic>{
      'maxWindSpeed': maxWindSpeed,
      'maxWaveHeight': maxWaveHeight,
      'maxSwellHeight': maxSwellHeight,
      'isActive': isActive,
      if (cautionWindSpeed != null) 'cautionWindSpeed': cautionWindSpeed,
      if (cautionWaveHeight != null) 'cautionWaveHeight': cautionWaveHeight,
      if (cautionSwellHeight != null) 'cautionSwellHeight': cautionSwellHeight,
    };

    final response = await _request('PUT', uri, body: body);
    return SafetyProfileDto.fromJson(jsonDecode(response.body));
  }

  /// DELETE /api/marine/safety-profiles/{id}
  Future<void> deactivateSafetyProfile(String id) async {
    final uri = _gateway.endpoint(baseUri, '/api/marine/safety-profiles/$id');
    final response = await _request('DELETE', uri);
    if (response.statusCode == 404) {
      throw const MarineApiException(404, 'Safety profile not found.');
    }
    if (!response.statusCodeIsSuccess()) {
      throw MarineApiException(
        response.statusCode,
        _errorMessage(response),
      );
    }
  }

  Future<http.Response> _request(
    String method,
    Uri uri, {
    Object? body,
    Map<String, String>? query,
  }) async {
    final candidates = _gateway.baseUris;
    for (final baseUri in candidates) {
      final requestUri = query == null
          ? _gateway.endpoint(baseUri, uri.path)
          : _gateway.endpoint(baseUri, uri.path).replace(queryParameters: query);
      try {
        final response = await _send(requestUri, method, body);
        if (response.statusCodeIsSuccess()) return response;
        throw MarineApiException(response.statusCode, _errorMessage(response));
      } on SocketException catch (error) {
        if (error.message.contains('Certificate')) {
          throw const MarineApiException(
            0,
            'Certificate issue when reaching the gateway. Use a trusted local certificate or run only on a supported LAN stack.',
          );
        }
        rethrow;
      } on TimeoutException {
        throw const MarineApiException(0, 'The marine service is taking too long to respond.');
      } on http.ClientException catch (error) {
        if (error.message.contains('Failed host lookup') ||
            error.message.contains('is not reachable')) {
          throw const MarineApiException(0, 'Cannot reach the marine service. Check the gateway address and try again.');
        }
        rethrow;
      }
    }

    throw const MarineApiException(0, 'The marine service is unreachable. Start the stack and try again.');
  }

  Future<http.Response> _send(Uri uri, String method, Object? body) async {
    final headers = <String, String>{
      'Accept': 'application/json',
      'Content-Type': 'application/json; charset=UTF-8',
    };

    final request = http.Request(method, uri)..headers.addAll(headers);
    if (body != null) {
      request.body = jsonEncode(body);
    }

    final streamed = await _client.send(request);
    final response = await http.Response.fromStream(streamed);
    return response;
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
}

extension _StatusCode on http.Response {
  bool statusCodeIsSuccess() => statusCode >= 200 && statusCode < 300;
}
