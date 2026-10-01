import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';

import '../models/coastal_operations_models.dart';
import 'api_gateway_config.dart';
import 'auth_api_service.dart';

class CoastalOperationsApiException implements Exception {
  const CoastalOperationsApiException(this.statusCode, this.message);

  final int statusCode;
  final String message;

  @override
  String toString() => message;
}

typedef _RequestSender = Future<http.Response> Function(
  Uri uri,
  Map<String, String> authorizationHeaders,
);

class CoastalOperationsApiService {
  CoastalOperationsApiService({
    required this.authApiService,
    http.Client? client,
    ApiGatewayConfig? gateway,
  }) : _transport = client ?? http.Client(),
       _ownsTransport = client == null,
       _gateway = gateway ?? const ApiGatewayConfig();

  final AuthApiService authApiService;
  final http.Client _transport;
  final bool _ownsTransport;
  final ApiGatewayConfig _gateway;
  Uri? _preferredBaseUri;

  Future<CoastalPage<CoastalAssessment>> listAssessments() async {
    final json = await _jsonRequest('/api/operations/assessments?pageSize=100');
    return _parse(() => CoastalPage.fromJson(json, CoastalAssessment.fromJson));
  }

  Future<CoastalAssessment> createAssessment({
    required String targetType,
    required String targetId,
    String? sourceWorkflowId,
    required String periodStartsAt,
    required String periodEndsAt,
    required String objective,
  }) async {
    final json = await _jsonRequest(
      '/api/operations/assessments',
      method: 'POST',
      body: {
        'targetType': targetType,
        'targetId': targetId,
        'sourceWorkflowId': sourceWorkflowId,
        'periodStartsAt': periodStartsAt,
        'periodEndsAt': periodEndsAt,
        'objective': objective,
      },
      idempotencyKey: _newIdempotencyKey(),
    );
    return _parse(() => CoastalAssessment.fromJson(json));
  }

  Future<CoastalAssessment> updateAssessmentDraft({
    required String assessmentId,
    required int expectedVersion,
    required String targetType,
    required String targetId,
    String? sourceWorkflowId,
    required String periodStartsAt,
    required String periodEndsAt,
    required String objective,
  }) async {
    final json = await _jsonRequest(
      '/api/operations/assessments/' + Uri.encodeComponent(assessmentId),
      method: 'PATCH',
      body: {
        'expectedVersion': expectedVersion,
        'targetType': targetType,
        'targetId': targetId,
        'sourceWorkflowId': sourceWorkflowId,
        'periodStartsAt': periodStartsAt,
        'periodEndsAt': periodEndsAt,
        'objective': objective,
      },
    );
    return _parse(() => CoastalAssessment.fromJson(json));
  }

  Future<CoastalAssessment> cancelAssessmentDraft({
    required String assessmentId,
    required int expectedVersion,
  }) async {
    final json = await _jsonRequest(
      '/api/operations/assessments/' + Uri.encodeComponent(assessmentId),
      method: 'DELETE',
      body: {'expectedVersion': expectedVersion},
      idempotencyKey: _newIdempotencyKey(),
    );
    return _parse(() => CoastalAssessment.fromJson(json));
  }

  Future<CoastalAssessment> submitAssessmentDraft({
    required String assessmentId,
    required int expectedVersion,
  }) async {
    final json = await _jsonRequest(
      '/api/operations/assessments/' +
          Uri.encodeComponent(assessmentId) +
          '/submit',
      method: 'POST',
      body: {'expectedVersion': expectedVersion},
      idempotencyKey: _newIdempotencyKey(),
    );
    return _parse(() => CoastalAssessment.fromJson(json));
  }

  Future<CoastalAssessmentDetail> getAssessmentDetail(
    String assessmentId,
  ) async {
    final json = await _jsonRequest(
      '/api/operations/assessments/${Uri.encodeComponent(assessmentId)}',
    );
    return _parse(() => CoastalAssessmentDetail.fromJson(json));
  }

  Future<CoastalEvidence> uploadAssessmentEvidence({
    required String assessmentId,
    required String fileName,
    required Uint8List bytes,
  }) async {
    final path =
        '/api/operations/assessments/${Uri.encodeComponent(assessmentId)}/evidence';
    final response = await _send(path, (uri, authorization) async {
      final request = http.MultipartRequest('POST', uri)
        ..headers.addAll(authorization)
        ..headers[HttpHeaders.acceptHeader] = 'application/json'
        ..files.add(
          http.MultipartFile.fromBytes(
            'Image',
            bytes,
            filename: fileName,
            contentType: MediaType('image', 'png'),
          ),
        );
      return http.Response.fromStream(await _transport.send(request));
    });
    return _parse(() => CoastalEvidence.fromJson(_decodeObject(response)));
  }

  Future<Uint8List> getEvidenceImage({
    required String assessmentId,
    required String evidenceId,
  }) async {
    final path =
        '/api/operations/assessments/${Uri.encodeComponent(assessmentId)}/evidence/${Uri.encodeComponent(evidenceId)}';
    final response = await _send(path, (uri, authorization) async {
      return _transport.get(
        uri,
        headers: {...authorization, HttpHeaders.acceptHeader: 'image/png'},
      );
    });
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw _failure(response.statusCode);
    }
    if (!(response.headers[HttpHeaders.contentTypeHeader] ?? '')
        .toLowerCase()
        .startsWith('image/png')) {
      throw const CoastalOperationsApiException(
        HttpStatus.badGateway,
        'The evidence image could not be displayed safely.',
      );
    }
    return response.bodyBytes;
  }

  Future<CoastalPage<CoastalAlert>> listAlerts() async {
    final json = await _jsonRequest('/api/operations/alerts?pageSize=100');
    return _parse(() => CoastalPage.fromJson(json, CoastalAlert.fromJson));
  }

  Future<CoastalAlert> createAlertDraft({
    required String targetType,
    required String targetId,
    String? assessmentId,
    required String title,
    required String description,
    required String severity,
    required String visibility,
    required String validFrom,
    required String validUntil,
  }) async {
    final json = await _jsonRequest(
      '/api/operations/alerts',
      method: 'POST',
      body: {
        'targetType': targetType,
        'targetId': targetId,
        'assessmentId': assessmentId,
        'title': title,
        'description': description,
        'severity': severity,
        'visibility': visibility,
        'validFrom': validFrom,
        'validUntil': validUntil,
      },
    );
    return _parse(() => CoastalAlert.fromJson(json));
  }

  Future<CoastalAlert> updateAlertDraft({
    required String alertId,
    required int expectedVersion,
    required String title,
    required String description,
    required String severity,
    required String visibility,
    required String validFrom,
    required String validUntil,
  }) async {
    final json = await _jsonRequest(
      '/api/operations/alerts/${Uri.encodeComponent(alertId)}',
      method: 'PATCH',
      body: {
        'expectedVersion': expectedVersion,
        'title': title,
        'description': description,
        'severity': severity,
        'visibility': visibility,
        'validFrom': validFrom,
        'validUntil': validUntil,
      },
    );
    return _parse(() => CoastalAlert.fromJson(json));
  }

  Future<void> decideAlert({
    required String alertId,
    required String decision,
    required int expectedVersion,
  }) async {
    await _jsonRequest(
      '/api/operations/alerts/${Uri.encodeComponent(alertId)}/decisions',
      method: 'POST',
      body: {'decision': decision, 'expectedVersion': expectedVersion},
      idempotencyKey: _newIdempotencyKey(),
    );
  }

  Future<CoastalAlert> withdrawAlertDraft({
    required String alertId,
    required int expectedVersion,
  }) async {
    final json = await _jsonRequest(
      '/api/operations/alerts/' + Uri.encodeComponent(alertId),
      method: 'DELETE',
      body: {'expectedVersion': expectedVersion},
      idempotencyKey: _newIdempotencyKey(),
    );
    return _parse(() => CoastalAlert.fromJson(json));
  }

  Future<CoastalOperationalStatus> getTargetStatus({
    required String targetType,
    required String targetId,
  }) async {
    final json = await _jsonRequest(
      '/api/operations/targets/${Uri.encodeComponent(targetType)}/${Uri.encodeComponent(targetId)}/status',
    );
    return _parse(() => CoastalOperationalStatus.fromJson(json));
  }

  Future<CoastalPage<CoastalHistoryItem>> getTargetHistory({
    required String targetType,
    required String targetId,
  }) async {
    final path = _withQuery(
      '/api/operations/targets/${Uri.encodeComponent(targetType)}/${Uri.encodeComponent(targetId)}/history',
      const {'pageSize': '25'},
    );
    final json = await _jsonRequest(path);
    return _parse(
      () => CoastalPage.fromJson(json, CoastalHistoryItem.fromJson),
    );
  }

  T _parse<T>(T Function() parse) {
    try {
      return parse();
    } on FormatException {
      throw const CoastalOperationsApiException(
        HttpStatus.badGateway,
        'The service returned information that could not be read. Refresh and try again.',
      );
    }
  }

  Future<JsonMap> _jsonRequest(
    String path, {
    String method = 'GET',
    JsonMap? body,
    String? idempotencyKey,
  }) async {
    final response = await _send(path, (uri, authorization) async {
      final headers = <String, String>{
        ...authorization,
        HttpHeaders.acceptHeader: 'application/json',
        if (body != null)
          HttpHeaders.contentTypeHeader: 'application/json; charset=UTF-8',
        ...?(idempotencyKey == null
            ? null
            : {'Idempotency-Key': idempotencyKey}),
      };
      final encoded = body == null ? null : jsonEncode(body);
      return switch (method) {
        'GET' => _transport.get(uri, headers: headers),
        'POST' => _transport.post(uri, headers: headers, body: encoded),
        'PATCH' => _transport.patch(uri, headers: headers, body: encoded),
        'DELETE' => _transport.delete(uri, headers: headers, body: encoded),
        _ => throw ArgumentError.value(
          method,
          'method',
          'Unsupported operation.',
        ),
      };
    });
    return _decodeObject(response);
  }

  Future<http.Response> _send(String path, _RequestSender send) async {
    final parsed = Uri.parse(path);
    final candidates = _orderedBaseUris();
    for (final baseUri in candidates) {
      try {
        final uri = _gateway
            .endpoint(baseUri, parsed.path)
            .replace(query: parsed.hasQuery ? parsed.query : null);
        var response = await send(
          uri,
          await authApiService.publicApiAuthorizationHeaders(),
        ).timeout(const Duration(seconds: 12));
        if (response.statusCode == HttpStatus.unauthorized) {
          try {
            await authApiService.refresh();
            response = await send(
              uri,
              await authApiService.publicApiAuthorizationHeaders(),
            ).timeout(const Duration(seconds: 12));
          } on Object {
            // Keep the safe 401 recovery message when refresh cannot complete.
          }
        }
        _preferredBaseUri = baseUri;
        return response;
      } on Object catch (error) {
        if (!_isConnectivityError(error)) rethrow;
      }
    }
    throw CoastalOperationsApiException(
      0,
      _gateway.connectionFailureMessage(candidates),
    );
  }

  JsonMap _decodeObject(http.Response response) {
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw _failure(response.statusCode);
    }
    try {
      final payload = jsonDecode(response.body);
      if (payload is Map<String, dynamic>) return payload;
    } on FormatException {
      // The response is reported below without exposing its body.
    }
    throw const CoastalOperationsApiException(
      HttpStatus.badGateway,
      'The service returned information that could not be read. Refresh and try again.',
    );
  }

  CoastalOperationsApiException _failure(int status) {
    final message = switch (status) {
      HttpStatus.unauthorized =>
        'Your session needs to be refreshed. Sign in again, then retry.',
      HttpStatus.forbidden =>
        'Your current permissions do not allow this action.',
      HttpStatus.notFound =>
        'This coastal record is unavailable or outside your access.',
      HttpStatus.conflict =>
        'This record changed while you were viewing it. Refresh and try again.',
      HttpStatus.requestEntityTooLarge =>
        'That image is larger than the 5 MiB limit.',
      HttpStatus.unsupportedMediaType =>
        'Choose a PNG image to attach as evidence.',
      HttpStatus.unprocessableEntity => 'The service could not accept those details. Review the form and try again.',
      HttpStatus.serviceUnavailable =>
        'Coastal operations is temporarily unavailable. Try again shortly.',
      _ => 'We could not complete that coastal operations request. Please try again.',
    };
    return CoastalOperationsApiException(status, message);
  }

  List<Uri> _orderedBaseUris() {
    final baseUris = _gateway.baseUris;
    final preferred = _preferredBaseUri;
    return preferred == null
        ? baseUris
        : [preferred, ...baseUris.where((uri) => uri != preferred)];
  }

  static String _withQuery(String path, Map<String, String> parameters) {
    final query = Uri(queryParameters: parameters).query;
    return '$path?$query';
  }

  static String _newIdempotencyKey() {
    final random = Random.secure();
    return 'mobile-${DateTime.now().microsecondsSinceEpoch}-${random.nextInt(1 << 32)}';
  }

  static bool _isConnectivityError(Object error) =>
      error is SocketException ||
      error is TimeoutException ||
      error is http.ClientException;

  void close() {
    if (_ownsTransport) _transport.close();
  }
}
