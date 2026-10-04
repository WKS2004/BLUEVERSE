import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/experience_models.dart';
import '../../ui/feedback/loading_screen_controller.dart';
import 'api_gateway_config.dart';
import 'auth_credential_store.dart';

class ExperienceApiException implements Exception {
  const ExperienceApiException(this.statusCode, this.message, [this.errorData]);

  final int statusCode;
  final String message;
  final Object? errorData;

  @override
  String toString() => message;
}

class ExperienceApiService {
  ExperienceApiService({
    http.Client? client,
    AuthCredentialStore? storage,
    ApiGatewayConfig? gateway,
  })  : _transport = client ?? http.Client(),
        _storage = storage ?? const SecureAuthCredentialStore(),
        _gateway = gateway ?? const ApiGatewayConfig();

  static const _activeAccountIdKey = 'blueverse.active_account_id';
  static const _accessTokenKey = 'blueverse.access_token';

  final http.Client _transport;
  final AuthCredentialStore _storage;
  final ApiGatewayConfig _gateway;
  Uri? _preferredBaseUri;

  static const _jsonHeaders = {
    'Accept': 'application/json',
    'Content-Type': 'application/json',
  };

  // ----------------- Destinations -----------------

  Future<PagedResult<DestinationDto>> getDestinations({
    String? query,
    String? region,
    String? status,
    int? page,
    int? pageSize,
  }) async {
    final queryParams = <String, String>{
      if (query != null && query.isNotEmpty) 'query': query,
      if (region != null && region.isNotEmpty) 'region': region,
      if (status != null && status.isNotEmpty) 'status': status,
      if (page != null) 'page': page.toString(),
      if (pageSize != null) 'pageSize': pageSize.toString(),
    };

    final response = await _request(
      '/api/experiences/destinations',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      queryParameters: queryParams,
    );

    return _decode(response, (json) => PagedResult.fromJson(json, DestinationDto.fromJson));
  }

  Future<DestinationDto> getDestinationById(String id) async {
    final response = await _request(
      '/api/experiences/destinations/${Uri.encodeComponent(id)}',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, DestinationDto.fromJson);
  }

  Future<DestinationDto> createDestination(CreateDestinationRequest request) async {
    final response = await _request(
      '/api/experiences/destinations',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, DestinationDto.fromJson);
  }

  Future<DestinationDto> updateDestination(String id, UpdateDestinationRequest request) async {
    final response = await _request(
      '/api/experiences/destinations/${Uri.encodeComponent(id)}',
      (uri) async => _transport.put(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, DestinationDto.fromJson);
  }

  Future<void> deleteDestination(String id) async {
    final response = await _request(
      '/api/experiences/destinations/${Uri.encodeComponent(id)}',
      (uri) async => _transport.delete(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
  }

  Future<PublicationEvaluationResponse> evaluateDestinationPublication(
    String id,
    String status,
  ) async {
    final response = await _request(
      '/api/experiences/destinations/${Uri.encodeComponent(id)}/publication-evaluations',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'status': status}),
      ),
    );
    return _decode(response, PublicationEvaluationResponse.fromJson);
  }

  Future<DestinationDto> updateDestinationPublication(String id, String status) async {
    final response = await _request(
      '/api/experiences/destinations/${Uri.encodeComponent(id)}/publication',
      (uri) async => _transport.patch(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'status': status}),
      ),
    );
    return _decode(response, DestinationDto.fromJson);
  }

  Future<MarineConditionsContextDto> getDestinationMarineConditions(String id) async {
    final response = await _request(
      '/api/experiences/destinations/${Uri.encodeComponent(id)}/marine-conditions',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, MarineConditionsContextDto.fromJson);
  }

  Future<OperationalAdvisoriesResponseDto> getDestinationOperationalAdvisories(String id) async {
    final response = await _request(
      '/api/experiences/destinations/${Uri.encodeComponent(id)}/operational-advisories',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, OperationalAdvisoriesResponseDto.fromJson);
  }

  Future<BiodiversityContextResponseDto> getDestinationBiodiversity(String id) async {
    final response = await _request(
      '/api/experiences/destinations/${Uri.encodeComponent(id)}/biodiversity',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, BiodiversityContextResponseDto.fromJson);
  }

  // ----------------- Activities -----------------

  Future<PagedResult<ActivityDto>> getActivities({
    String? destinationId,
    String? category,
    String? query,
    String? status,
    int? page,
    int? pageSize,
  }) async {
    final queryParams = <String, String>{
      if (destinationId != null && destinationId.isNotEmpty) 'destinationId': destinationId,
      if (category != null && category.isNotEmpty) 'category': category,
      if (query != null && query.isNotEmpty) 'query': query,
      if (status != null && status.isNotEmpty) 'status': status,
      if (page != null) 'page': page.toString(),
      if (pageSize != null) 'pageSize': pageSize.toString(),
    };

    final response = await _request(
      '/api/experiences/activities',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      queryParameters: queryParams,
    );

    _ensureSuccess(response);
    final decoded = jsonDecode(response.body);
    if (decoded is List) {
      final items = decoded
          .whereType<Map>()
          .map((item) => ActivityDto.fromJson(Map<String, dynamic>.from(item)))
          .toList(growable: false);
      return PagedResult(
        total: items.length,
        page: 1,
        pageSize: items.length,
        items: items,
      );
    }
    return PagedResult.fromJson(
      Map<String, dynamic>.from(decoded as Map),
      ActivityDto.fromJson,
    );
  }

  Future<ActivityDto> getActivityById(String id) async {
    final response = await _request(
      '/api/experiences/activities/${Uri.encodeComponent(id)}',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, ActivityDto.fromJson);
  }

  Future<ActivityDto> createActivity(CreateActivityRequest request) async {
    final response = await _request(
      '/api/experiences/activities',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, ActivityDto.fromJson);
  }

  Future<ActivityDto> updateActivity(String id, UpdateActivityRequest request) async {
    final response = await _request(
      '/api/experiences/activities/${Uri.encodeComponent(id)}',
      (uri) async => _transport.put(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, ActivityDto.fromJson);
  }

  Future<void> deleteActivity(String id) async {
    final response = await _request(
      '/api/experiences/activities/${Uri.encodeComponent(id)}',
      (uri) async => _transport.delete(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
  }

  Future<PublicationEvaluationResponse> evaluateActivityPublication(
    String id,
    String status,
  ) async {
    final response = await _request(
      '/api/experiences/activities/${Uri.encodeComponent(id)}/publication-evaluations',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'status': status}),
      ),
    );
    return _decode(response, PublicationEvaluationResponse.fromJson);
  }

  Future<ActivityDto> updateActivityPublication(String id, String status) async {
    final response = await _request(
      '/api/experiences/activities/${Uri.encodeComponent(id)}/publication',
      (uri) async => _transport.patch(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'status': status}),
      ),
    );
    return _decode(response, ActivityDto.fromJson);
  }

  // ----------------- Offerings -----------------

  Future<PagedResult<OfferingDto>> getOfferings({
    String? activityId,
    String? destinationId,
    String? status,
    int? page,
    int? pageSize,
  }) async {
    final queryParams = <String, String>{
      if (activityId != null && activityId.isNotEmpty) 'activityId': activityId,
      if (destinationId != null && destinationId.isNotEmpty) 'destinationId': destinationId,
      if (status != null && status.isNotEmpty) 'status': status,
      if (page != null) 'page': page.toString(),
      if (pageSize != null) 'pageSize': pageSize.toString(),
    };

    final response = await _request(
      '/api/experiences/offerings',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      queryParameters: queryParams,
    );

    return _decode(response, (json) => PagedResult.fromJson(json, OfferingDto.fromJson));
  }

  Future<OfferingDto> getOfferingById(String id) async {
    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, OfferingDto.fromJson);
  }

  Future<OfferingDto> createOffering(CreateOfferingRequest request) async {
    final response = await _request(
      '/api/experiences/offerings',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, OfferingDto.fromJson);
  }

  Future<OfferingDto> updateOffering(String id, UpdateOfferingRequest request) async {
    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}',
      (uri) async => _transport.put(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, OfferingDto.fromJson);
  }

  Future<void> deleteOffering(String id) async {
    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}',
      (uri) async => _transport.delete(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
  }

  Future<PublicationEvaluationResponse> evaluateOfferingPublication(
    String id,
    String status,
  ) async {
    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}/publication-evaluations',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'status': status}),
      ),
    );
    return _decode(response, PublicationEvaluationResponse.fromJson);
  }

  Future<OfferingDto> updateOfferingPublication(String id, String status) async {
    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}/publication',
      (uri) async => _transport.patch(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode({'status': status}),
      ),
    );
    return _decode(response, OfferingDto.fromJson);
  }

  Future<List<ScheduleDto>> getOfferingSchedules(
    String id, {
    String? from,
    String? to,
  }) async {
    final queryParams = <String, String>{
      if (from != null && from.isNotEmpty) 'from': from,
      if (to != null && to.isNotEmpty) 'to': to,
    };

    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}/schedules',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      queryParameters: queryParams,
    );
    return _decodeList(response).map(ScheduleDto.fromJson).toList(growable: false);
  }

  Future<ScheduleDto> addOfferingSchedule(String id, CreateScheduleRequest request) async {
    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}/schedules',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, ScheduleDto.fromJson);
  }

  Future<ScheduleDto> updateOfferingSchedule(
    String id,
    String scheduleId,
    UpdateScheduleRequest request,
  ) async {
    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}/schedules/${Uri.encodeComponent(scheduleId)}',
      (uri) async => _transport.put(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, ScheduleDto.fromJson);
  }

  Future<void> deleteOfferingSchedule(String id, String scheduleId) async {
    final response = await _request(
      '/api/experiences/offerings/${Uri.encodeComponent(id)}/schedules/${Uri.encodeComponent(scheduleId)}',
      (uri) async => _transport.delete(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
  }

  // ----------------- Availability Evaluation -----------------

  Future<AvailabilityEvaluationResponse> evaluateAvailability(
    AvailabilityEvaluationRequest request,
  ) async {
    final response = await _request(
      '/api/experiences/availability/evaluations',
      (uri) async => _transport.post(
        uri,
        headers: await _authHeaders(),
        body: jsonEncode(request.toJson()),
      ),
    );
    return _decode(response, AvailabilityEvaluationResponse.fromJson);
  }

  // ----------------- Favourites -----------------

  Future<List<FavouriteDto>> getUserFavourites() async {
    final response = await _request(
      '/api/experiences/favourites',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decodeList(response).map(FavouriteDto.fromJson).toList(growable: false);
  }

  Future<FavouriteDto> addFavourite(String targetType, String targetId) async {
    final response = await _request(
      '/api/experiences/favourites/${Uri.encodeComponent(targetType)}/${Uri.encodeComponent(targetId)}',
      (uri) async => _transport.put(uri, headers: await _authHeaders()),
    );
    return _decode(response, FavouriteDto.fromJson);
  }

  Future<void> removeFavourite(String targetType, String targetId) async {
    final response = await _request(
      '/api/experiences/favourites/${Uri.encodeComponent(targetType)}/${Uri.encodeComponent(targetId)}',
      (uri) async => _transport.delete(uri, headers: await _authHeaders()),
    );
    _ensureSuccess(response);
  }

  // ----------------- Map & Nearby Proximity -----------------

  Future<MapConfigDto> getMapConfig() async {
    final response = await _request(
      '/api/experiences/map/config',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, MapConfigDto.fromJson);
  }

  Future<MapSearchResponseDto> searchMapPlaces(String query) async {
    final response = await _request(
      '/api/experiences/map/search',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      queryParameters: {'q': query},
    );
    return _decode(response, MapSearchResponseDto.fromJson);
  }

  Future<NearbyResponse> getNearbyExperiences({
    String? query,
    double? latitude,
    double? longitude,
    double radiusMeters = 50000,
    int limit = 10,
  }) async {
    final queryParams = <String, String>{
      if (query != null && query.isNotEmpty) 'q': query,
      if (latitude != null) 'latitude': latitude.toString(),
      if (longitude != null) 'longitude': longitude.toString(),
      'radiusMeters': radiusMeters.toString(),
      'limit': limit.toString(),
    };

    final response = await _request(
      '/api/experiences/nearby',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
      queryParameters: queryParams,
    );
    return _decode(response, NearbyResponse.fromJson);
  }

  // ----------------- Diagnostics & Agent Seam -----------------

  Future<DependenciesStatusResponseDto> getDependenciesStatus() async {
    final response = await _request(
      '/api/experiences/dependencies/status',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, DependenciesStatusResponseDto.fromJson);
  }

  Future<AgentContextResponseDto> getAgentContext() async {
    final response = await _request(
      '/api/experiences/agent/context',
      (uri) async => _transport.get(uri, headers: await _authHeaders()),
    );
    return _decode(response, AgentContextResponseDto.fromJson);
  }

  // ----------------- Internal Helpers -----------------

  Future<Map<String, String>> _authHeaders() async {
    final activeAccountId = await _storage.read(_activeAccountIdKey);
    final scopedToken = activeAccountId == null
        ? null
        : await _storage.read('$_accessTokenKey.$activeAccountId');
    final legacyToken = await _storage.read(_accessTokenKey);
    final token = activeAccountId == null ? legacyToken : (scopedToken ?? legacyToken);

    return {
      ..._jsonHeaders,
      if (token != null && token.isNotEmpty)
        HttpHeaders.authorizationHeader: 'Bearer $token',
    };
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
    var message = 'The coastal experience request failed (${response.statusCode}).';
    Object? errorData;
    try {
      final decoded = jsonDecode(response.body);
      errorData = decoded;
      if (decoded is Map && decoded['detail'] is String) {
        message = decoded['detail'] as String;
      }
    } on Object {
      // Keep generic message
    }
    throw ExperienceApiException(response.statusCode, message, errorData);
  }

  Future<http.Response> _request(
    String path,
    Future<http.Response> Function(Uri uri) request, {
    Map<String, String>? queryParameters,
  }) async {
    final candidates = _orderedBaseUris();
    for (final baseUri in candidates) {
      try {
        var targetUri = _gateway.endpoint(baseUri, path);
        if (queryParameters != null && queryParameters.isNotEmpty) {
          targetUri = targetUri.replace(queryParameters: queryParameters);
        }
        final response = await request(targetUri).timeout(const Duration(seconds: 4));
        _preferredBaseUri = baseUri;
        return response;
      } on Object catch (error) {
        if (!_isConnectivityError(error)) {
          rethrow;
        }
      }
    }

    throw ExperienceApiException(0, _gateway.connectionFailureMessage(candidates));
  }

  List<Uri> _orderedBaseUris() {
    final baseUris = _gateway.baseUris;
    final preferred = _preferredBaseUri;
    if (preferred == null) return baseUris;
    return [preferred, ...baseUris.where((uri) => uri != preferred)];
  }

  bool _isConnectivityError(Object error) =>
      error is SocketException ||
      error is TimeoutException ||
      error is http.ClientException;
}
